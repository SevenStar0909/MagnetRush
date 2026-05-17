using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ボスAI。6状態FSM (Idle/Chase/AttackStance/AttackMotion/Stunned/Stagger)。
/// NavMeshAgent は経路計算のみ (updatePosition=false, updateRotation=false)、実移動は EnemyBossBase.AccelerateToward 経由で EntityController が処理する。
/// AI → Animator: 状態読み取り (IsAttacking/IsInAttackMotion/IsStunned) のみ。書き込みは Animator が自分で行う。
/// Animator → AI: AnimEvent 経由の OnAttackFinished/OnStunEnd コールバック。
/// 依存: EnemyBossBase, NavMeshAgent, EnemyBossBaseA_Animator
/// </summary>
[RequireComponent(typeof(EnemyBossBase))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBossAI : MonoBehaviour, IStabReceiver
{
    public enum BossState { Idle, Chase, AttackStance, AttackMotion, Rush, Missile, Stunned, Stagger }

    [Header("References")]
    [SerializeField] private EnemyBossBaseA_Animator m_animator;

    [Header("Missile")]
    [Tooltip("生成するミサイルPrefab")]
    [SerializeField] private EnemyMissile m_missilePrefab;

    [Tooltip("ミサイル生成位置。未設定ならこのオブジェクト位置を使用")]
    [SerializeField] private Transform[] m_missileSpawnPoints;

    [Tooltip("各生成位置のローカルオフセット。m_missileSpawnPoints と同じ順番で指定")]
    [SerializeField] private Vector3[] m_missileSpawnOffsets;

    [Header("Debug")]
    [SerializeField] private bool m_logStateChange = true;

    private EnemyBossBase m_boss;
    private NavMeshAgent m_agent;
    private Transform m_player;
    private EnemyBossSettings m_settings;
    private Stamina m_stamina;
    private Health m_health;

    private BossState m_state = BossState.Idle;
    private float m_cooldownTimer;
    private float m_staminaBreakTimer;
    private Vector3 m_lastDirection;

    private Vector3 m_rushTargetPosition;

    [Header("Rush or missile")]
    [SerializeField] private bool m_nextLongRangeAttackIsRush = true; // rushとmissileを交互に行うためのフラグ

    private bool m_wasInStunAnim;
    private bool m_wasInStaggerAnim;
    private bool m_staminaBreakEndRequested;

    private bool m_resetStunEndTrigger;
    private bool m_resetStaggerEndTrigger;

    // Rush 中に Animator が一度でも IsInRush=true になったか。
    // 入り transition と exit transition を区別し、exit 時の player 追尾回転を抑制する。
    private bool m_rushHasStarted;

    public BossState State => m_state;

    void Awake()
    {
        m_boss = GetComponent<EnemyBossBase>();
        m_agent = GetComponent<NavMeshAgent>();
        m_stamina = GetComponent<Stamina>();
        m_health = GetComponent<Health>();

        if (m_animator == null)
            m_animator = GetComponentInChildren<EnemyBossBaseA_Animator>();

        if (m_agent != null)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
        }
    }

    void OnEnable()
    {
        if (m_stamina != null)
            m_stamina.OnBreak += HandleStaminaBreak;
    }

    void OnDisable()
    {
        if (m_stamina != null)
            m_stamina.OnBreak -= HandleStaminaBreak;
    }

    void Start()
    {
        m_player = m_boss.Player;
        m_settings = m_boss.StatusData;

        if (m_agent != null && m_settings != null)
        {
            m_agent.speed = m_settings.moveSpeed;
            m_agent.stoppingDistance = m_settings.stopDistance;
            m_agent.acceleration = m_settings.acceleration;
        }

        if (m_animator == null)
            ChannelLogger.LogError("Enemy", $"[EnemyBossAI] {name}: EnemyBossBaseA_Animator 未設定");
    }

    void Update()
    {
        if (m_player == null || m_settings == null || m_agent == null || m_animator == null)
        { ChannelLogger.LogGuardReturn("Enemy", "プレイヤー/Settings/Agent/Animator未取得"); return; }

        float dt = Time.deltaTime;
        m_cooldownTimer = Mathf.Max(0f, m_cooldownTimer - dt);

        TickStunEntry(); // Stunアニメーションの開始を検知してStunned状態に入り、回復タイマーを開始する
        TickStaggerEntry(); // Staggerアニメーションの開始を検知してStagger状態に入り、回復タイマーを開始する
        TickStaminaBreakTimer(dt); // Stunned/Staggered 共通の回復タイマー

        TryRecoverAgent();
        SyncAgentToBody();

        switch (m_state)
        {
            case BossState.Idle: TickIdle(dt); break;
            case BossState.Chase: TickChase(dt); break;
            case BossState.AttackStance: TickAttackStance(dt); break;
            case BossState.AttackMotion: TickAttackMotion(dt); break;
            case BossState.Rush: TickRush(dt); break;
            case BossState.Missile: TickMissile(dt); break;
            case BossState.Stunned: TickStunned(dt); break;
            case BossState.Stagger: TickStagger(dt); break;
        }
    }

    private void HandleStaminaBreak()
    {
        if (m_animator == null) return;

        m_animator.SetIsStunnedTrue();
        m_animator.SetIsStaggerFalse();
    }

    private void TickStunEntry()
    {
        bool inStunAnim = m_animator.IsStunned;
        // 既に Stunned/Stagger 中は再入場禁止。Animator のトランジション遅延中に IsStunned が true のまま残ると、
        // ChangeState(Idle) 後の次フレームで再検出されてループする
        bool alreadyInBreak = m_state == BossState.Stunned || m_state == BossState.Stagger;

        if (!alreadyInBreak && inStunAnim && !m_wasInStunAnim)
        {
            m_staminaBreakTimer = Mathf.Max(0f, m_settings.staminaBreakDuration);
            m_staminaBreakEndRequested = false;
            ChangeState(BossState.Stunned);
        }

        m_wasInStunAnim = inStunAnim;
    }

    private void TickStaggerEntry()
    {
        bool inStaggerAnim = m_animator.IsInStagger;
        // Stun と同じ理由でループ防止
        bool alreadyInBreak = m_state == BossState.Stagger || m_state == BossState.Stunned;

        if (!alreadyInBreak && inStaggerAnim && !m_wasInStaggerAnim && !m_animator.IsStunned)
        {
            m_staminaBreakTimer = Mathf.Max(0f, m_settings.staminaBreakDuration);
            m_staminaBreakEndRequested = false;
            ChangeState(BossState.Stagger);
        }

        m_wasInStaggerAnim = inStaggerAnim;
    }

    private void TickStaminaBreakTimer(float dt)
    {
        if (m_state != BossState.Stunned && m_state != BossState.Stagger) return;
        if (m_staminaBreakEndRequested) return;

        m_staminaBreakTimer = Mathf.Max(0f, m_staminaBreakTimer - dt);
        if (m_staminaBreakTimer > 0f) return;

        m_staminaBreakEndRequested = true;

        if (m_state == BossState.Stunned)
        {
            m_animator.TriggerStunEnd();
            m_animator.SetIsStunnedFalse();
            m_resetStunEndTrigger = true;
        }
        else
        {
            m_animator.TriggerStaggerEnd();
            m_animator.SetIsStaggerFalse();
            m_resetStaggerEndTrigger = true;
        }

        ChangeState(BossState.Idle);
    }

    // === 状態遷移 ===

    private void ChangeState(BossState next)
    {
        if (next == m_state) return;
        if (m_logStateChange)
            ChannelLogger.Log("EnemyBossA", $"[EnemyBossAI] {m_state} → {next}");

        var prev = m_state;
        m_state = next;

        if (next == BossState.Rush)
        {
            m_rushTargetPosition = m_player.position;
            m_boss.lateralVelocity = Vector3.zero;
            m_rushHasStarted = false; // 入り transition フェーズへ。Animator が IsInRush=true に入った時点で true 化
        }

        if (next == BossState.Idle)
            ClearStaminaFlags();

        if (prev == BossState.AttackMotion || prev == BossState.Rush || prev == BossState.Missile)
            m_cooldownTimer = m_settings.attackInterval;
    }

    private void ClearStaminaFlags()
    {
        if (m_animator == null) return;

        m_animator.SetIsStaggerFalse();
        m_animator.SetIsStunnedFalse();
        m_wasInStunAnim = false;
        m_wasInStaggerAnim = false;
        m_staminaBreakEndRequested = false;
    }

    // === 各状態の Tick ===

    private void TickIdle(float dt)
    {
        // Stunned/Stagger の入場検知は TickStunEntry/TickStaggerEntry が一元担当する。
        // ここで再検出すると Animator のトランジション遅延中に二重発火してループする

        m_boss.SlowDown(dt);
        FacePlayer(dt, m_settings.faceDeadZoneDeg);

        if (m_cooldownTimer > 0f)
            return;

        float distance = DistanceToPlayer();
        ChannelLogger.Log("EnemyBossA", $"distanceToPlayer = {distance}");

        if (distance <= m_settings.attackRange)
        {
            m_animator.TriggerAttack();
            ChangeState(BossState.AttackStance);
            return;
        }

        if (m_settings.rushAttackRange < distance && distance <= m_settings.missileAttackRange)
        {
            m_animator.TriggerAttackRush();
            ChangeState(BossState.Rush);
            return;
        }

        if (m_settings.missileAttackRange < distance)
        {
            if (m_nextLongRangeAttackIsRush)
                m_animator.TriggerAttackRush();
            else
                m_animator.TriggerMissile();
            ChangeState(m_nextLongRangeAttackIsRush ? BossState.Rush : BossState.Missile);
        }

        m_nextLongRangeAttackIsRush = !m_nextLongRangeAttackIsRush;
    }

    // 使わない20260511
    private void TickChase(float dt)
    {
        if (m_animator.IsStunned) { ChangeState(BossState.Stunned); return; }

        if (m_agent.enabled && m_agent.isOnNavMesh)
            m_agent.ResetPath();

        m_boss.SlowDown(dt);
        FacePlayer(dt, m_settings.faceDeadZoneDeg);

        float distance = DistanceToPlayer();
        if (distance <= m_settings.attackRange && m_cooldownTimer <= 0f)
        {
            m_animator.TriggerAttack();
            ChangeState(BossState.AttackStance);
        }
    }

    private void TickAttackStance(float dt)
    {
        FacePlayer(dt, m_settings.faceDeadZoneDeg);
        m_boss.SlowDown(dt);

        if (m_animator.IsStunned) { ChangeState(BossState.Stunned); return; }
        if (m_animator.IsInAttackMotion) { ChangeState(BossState.AttackMotion); return; }
    }

    private void TickAttackMotion(float dt)
    {
        // 普通攻擊移動なし
        m_boss.SlowDown(dt);
        FacePlayer(dt, m_settings.attackMotionFaceDeadZoneDeg);
    }

    private void TickRush(float dt)
    {
        if (!m_animator.IsInRush)
        {
            m_boss.lateralVelocity = Vector3.zero;
            // 入り transition フェーズだけ player 追尾。
            // exit transition フェーズ（rush 後）は回転固定 → rush 方向のまま Idle へ抜ける。
            if (!m_rushHasStarted)
                FacePlayer(dt, m_settings.faceDeadZoneDeg);
            return;
        }
        m_rushHasStarted = true;
        MoveTowardPlayerLastLocation(dt, m_settings.rushSpeedMultiplier);
    }

    private void TickMissile(float dt)
    {
        m_boss.SlowDown(dt);
        FacePlayer(dt, m_settings.faceDeadZoneDeg);
    }

    private void TickStunned(float dt)
    {
        m_boss.SlowDown(dt);
    }

    private void TickStagger(float dt)
    {
        m_boss.SlowDown(dt);
        FacePlayer(dt, m_settings.faceDeadZoneDeg);
    }

    // === 公開コールバック (Animator → AI) ===

    /// <summary>AttackMotion clip 末尾の AnimEvent から呼ばれる。</summary>
    public void OnAttackFinished()
    {
        Debug.Log("[EnemyBossAI] OnAttackFinished called");
        if (m_state == BossState.AttackMotion)
            ChangeState(BossState.Idle);
    }

    /// <summary>AttackStun clip 末尾の AnimEvent から呼ばれる。</summary>
    public void OnStunEnd()
    {
        if (m_state == BossState.Stunned)
            ChangeState(BossState.Idle);
    }

    /// <summary>AttackStun clip 末尾の AnimEvent から呼ばれる。</summary>
    public void OnRushFinished()
    {
        Debug.Log("[EnemyBossAI] OnRushFinished called");
        if (m_state == BossState.Rush)
            ChangeState(BossState.Idle);
    }

    /// <summary>AttackStun clip 末尾の AnimEvent から呼ばれる。</summary>
    public void OnMissileFinished()
    {
        if (m_state == BossState.Missile)
            ChangeState(BossState.Idle);
    }

    /// <summary>Missile 発射イベント専用。AnimationEvent から呼ばれ、各 SpawnPoint からミサイルを生成する。</summary>
    public void OnMissileFireEvent()
    {
        if (m_missilePrefab == null)
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "EnemyBossAI.m_missilePrefab が未アサインです");
            return;
        }

        if (m_missileSpawnPoints == null || m_missileSpawnPoints.Length == 0)
        {
            SpawnMissileAt(this.transform);
            return;
        }

        for (int i = 0; i < m_missileSpawnPoints.Length; i++)
        {
            Transform spawnPoint = m_missileSpawnPoints[i];
            if (spawnPoint == null) continue;

            Vector3 offset = Vector3.zero;
            if (m_missileSpawnOffsets != null && i < m_missileSpawnOffsets.Length)
                offset = m_missileSpawnOffsets[i];

            SpawnMissileAt(spawnPoint, offset);
        }
    }

    private void SpawnMissileAt(Transform spawnPoint, Vector3 localOffset)
    {
        Vector3 spawnPos = spawnPoint.position + spawnPoint.TransformDirection(localOffset);

        Vector3 direction = transform.forward;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = spawnPoint.forward;

        direction = direction.normalized;
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

        EnemyMissile missile = Instantiate(m_missilePrefab, spawnPos, rotation);
        missile.Initialize(m_player, direction);
    }

    private void SpawnMissileAt(Transform spawnPoint)
    {
        SpawnMissileAt(spawnPoint, Vector3.zero);
    }

    // === IStabReceiver 実装 (Player → Boss スタブ受信) ===

    /// <summary>体幹ブレイク (Stunned) 中のみ true。</summary>
    public bool CanReceiveStab => m_state == BossState.Stunned;

    /// <summary>
    /// プレイヤーのスタブAnimEventから呼ばれる。クールダウン無視でHPを削る。
    /// 死亡判定は Health 側で発火する OnDie に任せる。
    /// </summary>
    public void OnStabHit(StabHitData data)
    {
        if (!CanReceiveStab)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Stunned 以外のため Stab 無効"); return; }

        if (m_health == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Health 未取得"); return; }

        m_health.DamageIgnoreCooldown(data.damage);
        ChannelLogger.Log("EnemyBossA", $"[Stab] dmg={data.damage} src={(data.source != null ? data.source.name : "null")} hp={m_health.CurrentHealth}");
    }

    // === ヘルパ ===

    private float DistanceToPlayer()
    {
        return Vector3.Distance(transform.position, m_player.position);
    }

    private bool PlayerInChaseRange()
    {
        // 今使わないけど、将来の拡張で Idle → Chase 遷移条件にするかもなので残しておく
        // 廃棄したいですけど。蘇
        return DistanceToPlayer() <= m_settings.chaseRange;
    }

    private void FacePlayer(float dt, float deadZoneDeg = 0f)
    {
        Vector3 look = m_player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            m_boss.FaceToward(look.normalized, dt, deadZoneDeg);
    }

    private void MoveTowardPlayer(float dt, float speedMultiplier)
    {
        if (!m_agent.enabled || !m_agent.isOnNavMesh)
        {
            // フォールバック直線追跡
            Vector3 dir = GetDirectionToPlayer();
            if (dir.sqrMagnitude > 0.0001f)
                m_boss.AccelerateToward(dir * speedMultiplier, dt);
            return;
        }

        m_agent.SetDestination(m_player.position);
        Vector3 navDir = GetNavMeshDirection();
        if (navDir.sqrMagnitude > 0.0001f)
            m_boss.AccelerateToward(navDir * speedMultiplier, dt);
    }

    private void MoveTowardPlayerLastLocation(float dt, float speedMultiplier)
    {
        // Accelerate 内で direction が normalize されるため、倍率は topSpeed 側に乗せる必要がある
        if (!m_agent.enabled || !m_agent.isOnNavMesh)
        {
            Vector3 dir = GetDirectionToPosition(m_rushTargetPosition);
            if (dir.sqrMagnitude > 0.0001f)
                m_boss.AccelerateToward(dir, dt, speedMultiplier);
            return;
        }

        m_agent.SetDestination(m_rushTargetPosition);
        Vector3 navDir = GetNavMeshDirection(m_rushTargetPosition);
        if (navDir.sqrMagnitude > 0.0001f)
            m_boss.AccelerateToward(navDir, dt, speedMultiplier);
    }

    private Vector3 GetNavMeshDirection()
    {
        if (!m_agent.hasPath && !m_agent.pathPending)
            return GetDirectionToPlayer();

        if (m_agent.pathPending)
            return m_lastDirection;

        Vector3 dir = m_agent.steeringTarget - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
        {
            m_lastDirection = dir.normalized;
            return m_lastDirection;
        }

        return m_lastDirection;
    }

    private Vector3 GetNavMeshDirection(Vector3 targetPosition)
    {
        if (!m_agent.hasPath && !m_agent.pathPending)
            return GetDirectionToPosition(targetPosition);

        if (m_agent.pathPending)
            return m_lastDirection;

        Vector3 dir = targetPosition - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
        {
            m_lastDirection = dir.normalized;
            return m_lastDirection;
        }

        return m_lastDirection;
    }

    private Vector3 GetDirectionToPlayer()
    {
        Vector3 dir = m_player.position - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
    }

    private Vector3 GetDirectionToPosition(Vector3 targetPosition)
    {
        Vector3 dir = targetPosition - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
    }

    private void SyncAgentToBody()
    {
        if (!m_agent.enabled || !m_agent.isOnNavMesh) return;
        // EntityController が動かした位置を NavMeshAgent に同期し、内部シミュレーションを抑制
        m_agent.nextPosition = transform.position;
        m_agent.velocity = Vector3.zero;
    }

    private void TryRecoverAgent()
    {
        if (m_agent == null) { ChannelLogger.LogGuardReturn("EnemyBossA", "NavMeshAgentなし"); return; }
        if (m_agent.enabled && m_agent.isOnNavMesh) { ChannelLogger.LogGuardReturn("EnemyBossA", "Agent既に有効"); return; }

        const float sampleRadius = 20;

        Vector3 sourcePosition = transform.position;
        if (!NavMesh.SamplePosition(sourcePosition, out var hit, sampleRadius, NavMesh.AllAreas))
        {
            if (m_player != null)
            {
                sourcePosition = m_player.position;
                if (!NavMesh.SamplePosition(sourcePosition, out hit, sampleRadius, NavMesh.AllAreas))
                {
                    ChannelLogger.LogGuardReturn("EnemyBossA",
                        $"NavMeshサンプル失敗 pos={transform.position} player={m_player.position} radius={sampleRadius}");
                    return;
                }
            }
            else
            {
                ChannelLogger.LogGuardReturn("EnemyBossA",
                    $"NavMeshサンプル失敗 pos={transform.position} radius={sampleRadius}");
                return;
            }
        }

        if (m_agent.enabled)
            m_agent.enabled = false;

        transform.position = hit.position;
        m_agent.enabled = true;

        if (m_agent.isOnNavMesh)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
            m_agent.velocity = Vector3.zero;

            ChannelLogger.Log("EnemyBossA", $"Agent復帰成功 pos={transform.position} hit={hit.position}");
        }
        else
        {
            ChannelLogger.LogWarning("EnemyBossA", $"Agent復帰失敗 pos={transform.position}");
        }
    }

    void LateUpdate()
    {
        if (m_animator == null) return;

        if (m_resetStunEndTrigger)
        {
            m_animator.ResetStunEnd();
            m_resetStunEndTrigger = false;
        }

        if (m_resetStaggerEndTrigger)
        {
            m_animator.ResetStaggerEnd();
            m_resetStaggerEndTrigger = false;
        }
    }
}