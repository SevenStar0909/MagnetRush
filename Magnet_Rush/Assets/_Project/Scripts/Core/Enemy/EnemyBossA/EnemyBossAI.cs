using System;
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

    [Tooltip("ONで2発目のアニメイベントをアーク弾(上げてから狙う)にする。OFFで両方とも通常弾。発射数は変わらない(計4発)")]
    [SerializeField] private bool m_fireLobMissiles = true;

    [Tooltip("アーク弾の打ち上げ角度(度)。前方から上へ傾ける。大きいほど高く上がる")]
    [SerializeField] private float m_missileLobAngle = 45f;

    [Tooltip("アーク弾が上昇してからプレイヤーへ向き直すまでの時間(秒)。長いほど高く上げてから落ちる")]
    [SerializeField] private float m_missileLobRiseTime = 0.45f;

    [Tooltip("発射してからこの秒数はミサイルがボス本体に当たらない(発射直後の自爆防止)。経過後はボスにも当たる=磁力で撃ち返せる。0で即当たる")]
    [SerializeField] private float m_missileCollisionGrace = 3f;

    // 次の OnMissileFireEvent でアーク弾を撃つか。アニメの2イベントで 通常→アーク と交互に切り替える
    private bool m_nextMissileIsLob;

    [Header("Debug")]
    [SerializeField] private bool m_logStateChange = true;

    private EnemyBossBase m_boss;
    private NavMeshAgent m_agent;
    private Transform m_player;
    private EnemyBossSettings m_settings;
    private Stamina m_stamina;
    private Health m_health;

    // ボス本体（各ボーン）の Hitbox。物理オブジェクト接触でスタンゲージを溜める（機構1）。
    private Hitbox[] m_bodyHitboxes;

    private BossState m_state = BossState.Idle;
    private float m_cooldownTimer;
    private float m_staminaBreakTimer;
    private Vector3 m_lastDirection;

    private Vector3 m_rushTargetPosition;
    // Rush 突入時に確定する固定方向。direction を毎フレーム再計算するとターゲット通過時に 180°反転して回転が暴れるため
    private Vector3 m_rushDirection;

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

    public event Action OnStabHitSucceeded;   // スタブが成功したときに発火

    public BossState State => m_state;

    public EnemyBossSettings Settings => m_settings;

    public Stamina Stamina => m_stamina;

    void Awake()
    {
        m_boss = GetComponent<EnemyBossBase>();
        m_agent = GetComponent<NavMeshAgent>();
        m_stamina = GetComponent<Stamina>();
        m_health = GetComponent<Health>();

        // 右手の ArmStunHitbox は Hitbox 派生ではないので含まれない（＝カウンター経路と分離される）。
        m_bodyHitboxes = GetComponentsInChildren<Hitbox>(true);

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

        if (m_bodyHitboxes != null)
            foreach (var hb in m_bodyHitboxes)
                if (hb != null) hb.OnHitEvent += OnBodyHit;
    }

    void OnDisable()
    {
        if (m_stamina != null)
            m_stamina.OnBreak -= HandleStaminaBreak;

        if (m_bodyHitboxes != null)
            foreach (var hb in m_bodyHitboxes)
                if (hb != null) hb.OnHitEvent -= OnBodyHit;
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
        if (m_player == null || m_settings == null || m_animator == null)
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

    // スタンゲージが満タン（＝Stamina 0）になった時に Stamina.OnBreak から呼ばれる。
    // よろけ（Stagger）を1回だけ発火する。OnBreak はゲージが 0 に落ちた瞬間に1度だけ発火するのでループしない。
    // よろけ中もスタブ可（CanReceiveStab が Stagger を含む）。スタン（振り上げカウンター=ArmStunHitbox）でも同様にスタブできる。
    private void HandleStaminaBreak()
    {
        if (m_animator == null) return;

        m_animator.SetIsStaggerTrue();
        m_animator.TriggerBeInterrupted();
    }

    // ボス本体（ボーンの Hitbox）に物理オブジェクトが当たった時に呼ばれる。スタンゲージを蓄積する（機構1）。
    // 弾など磁化体でないものは無視。満タンになると Stamina.OnBreak → HandleStaminaBreak でよろけが発火する。
    private void OnBodyHit(HitData hit)
    {
        if (m_stamina == null) return;
        if (m_state == BossState.Stunned || m_state == BossState.Stagger) return; // 崩れ中は無視
        if (m_stamina.IsBroken) return;                                      // 満タン到達済み（崩れ処理中）。崩れ終了時にリセットされる
        if (hit.source == null) return;
        if (hit.source.transform.IsChildOf(transform)) return;               // 自分由来は無視

        int percent = ResolveStunPercent(hit.source);
        if (percent <= 0) return;                                            // スタン値を持たない物（弾など）は無視

        int max = m_stamina.MaxStamina;
        int amount = Mathf.Max(1, Mathf.RoundToInt(max * percent / 100f));
        m_stamina.Consume(amount);

        ChannelLogger.Log("EnemyBossA",
            $"[StunGauge] 本体ヒット +{percent}% (+{amount}) 蓄積={max - m_stamina.CurrentStamina}/{max} src={hit.source.name}");
    }

    // ぶつかった物のスタン値蓄積率（％）を返す。箱=MagneticContactDamage（小10/大30）、誘導ミサイル=EnemyMissile（30）、
    // その他の磁化体は既定値、磁化体でなければ（弾など）0。
    private int ResolveStunPercent(GameObject source)
    {
        var contact = source.GetComponentInParent<MagneticContactDamage>();
        if (contact != null) return contact.StunGaugePercent;

        var missile = source.GetComponentInParent<EnemyMissile>();
        if (missile != null) return missile.StunGaugePercent;

        if (source.GetComponentInParent<Magnetizable>() != null)
            return m_settings != null ? m_settings.stunGaugePercentPerBodyHit : 10;

        return 0;
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
            // StunAnim に入ったら IsStunned bool を即落とす。AnyState→StunAnim は IsStunned==true で遷移するため、
            // true のままだと StunkeepAnim から AnyState 経由で StunAnim へ戻り続けてループする。
            // 状態保持は StunAnim→StunkeepAnim→(StunEnd)→Idle が担うので、bool は入場トリガーとして1回使えば十分。
            m_animator.SetIsStunnedFalse();
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
            // よろけ（蓄積ルート）は専用の継続時間を使う。仕様＝10秒。スタン（カウンタールート）は staminaBreakDuration＝5秒。
            m_staminaBreakTimer = Mathf.Max(0f, m_settings.staggerDuration);
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
        {
            ClearStaminaFlags();
            // Idle に戻る時は Wind/Dust を必ず止める。Rush の DisableWindEffectEvent が
            // 中断（被弾→Stagger 等）で発火しないまま Idle へ戻ると Wind が出続けるため、保険として停止する。
            // Wind/Dust は Rush/Stun 中しか点かないので Idle で消すのは常に正しい。
            if (m_animator != null)
            {
                m_animator.DisableWindEffectEvent();
                m_animator.DisableDustEffectEvent();
            }
        }

        // ミサイル攻撃の入り口でトグルをリセット（必ず 1発目=通常波 から始める）
        if (next == BossState.Missile)
            m_nextMissileIsLob = false;

        // Rush 中に Stun/Stagger で割り込まれると Rush 側の Disable AnimEvent が発火せず
        // Wind/Dust が出続けるので、ブレイク入り口でも明示停止する
        if (next == BossState.Stunned || next == BossState.Stagger)
        {
            if (m_animator != null)
            {
                m_animator.DisableWindEffectEvent();
                m_animator.DisableDustEffectEvent();
            }
        }

        if (prev == BossState.AttackMotion || prev == BossState.Rush || prev == BossState.Missile)
            m_cooldownTimer = m_settings.attackInterval;
    }

    private void ClearStaminaFlags()
    {
        if (m_animator == null) return;

        m_animator.SetIsStaggerFalse();
        m_animator.SetIsStunnedFalse();
        // Animator のトランジション遅延中はまだ Stun/Stagger ステートに残っている。
        // ここで false 固定すると、次フレームで TickStunEntry/TickStaggerEntry が立ち上がりエッジを
        // 誤検出して再入場し、Stagger(Stun) がループする。実ステートに同期させてエッジ誤検出を防ぐ。
        m_wasInStunAnim = m_animator.IsStunned;
        m_wasInStaggerAnim = m_animator.IsInStagger;
        m_staminaBreakEndRequested = false;

        // ここではスタン値（Stamina）をリセットしない。スタン値はスタブを当てるまで減らない仕様。
        // リセットは OnStabHit（スタブ成功時）の EndBreakAfterStab でのみ行う。
    }

    // === 各状態の Tick ===

    private void TickIdle(float dt)
    {
        // Stunned/Stagger の入場検知は TickStunEntry/TickStaggerEntry が一元担当する。
        // ここで再検出すると Animator のトランジション遅延中に二重発火してループする

        m_boss.SlowDown(dt);

        float distance = DistanceToPlayer();

        // プレイヤーが起動範囲の外なら、向き直りも攻撃もせず Idle のまま待機する
        if (distance > m_settings.activationRange)
            return;

        FacePlayer(dt, m_settings.faceDeadZoneDeg);

        if (m_cooldownTimer > 0f)
            return;

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
            // 遠距離攻撃が実際に発動したときだけ Rush ↔ Missile を反転させる
            m_nextLongRangeAttackIsRush = !m_nextLongRangeAttackIsRush;
        }
    }

    // 使わない20260511
    private void TickChase(float dt)
    {
        if (m_animator.IsStunned) { ChangeState(BossState.Stunned); return; }

        if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
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
            {
                FacePlayer(dt, m_settings.faceDeadZoneDeg);
                // 入り transition 中は live のプレイヤー位置でターゲットを更新する
                m_rushTargetPosition = m_player.position;
            }
            return;
        }

        if (!m_rushHasStarted)
        {
            // Rush 突入の瞬間に方向を確定（以降このまま直進、毎フレーム再計算しない）
            Vector3 toTarget = m_rushTargetPosition - transform.position;
            toTarget.y = 0f;
            m_rushDirection = toTarget.sqrMagnitude > 0.0001f
                ? toTarget.normalized
                : transform.forward;
            m_rushHasStarted = true;
        }

        // 固定方向に直進。NavMesh path や targetPosition との距離は参照しない（オーバーシュート時の180°反転を防ぐ）
        m_boss.AccelerateToward(m_rushDirection, dt, m_settings.rushSpeedMultiplier);
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
        // Stagger 中はプレイヤーに向き直らない（Stunned と同じ挙動）
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

        // アニメの2イベントで 1発目=通常波 / 2発目=アーク波 と交互に撃つ（発射数は元のまま 計4発）
        bool lob = m_fireLobMissiles && m_nextMissileIsLob;
        FireMissileWave(lob);
        m_nextMissileIsLob = !m_nextMissileIsLob;
    }

    /// <summary>1波分。全発射点から通常弾 or アーク弾を1発ずつ撃つ。</summary>
    private void FireMissileWave(bool lob)
    {
        if (m_missilePrefab == null) return;

        Vector3 direction = lob ? ComputeLobDirection() : transform.forward;
        float seekDelay = lob ? m_missileLobRiseTime : -1f;

        if (m_missileSpawnPoints == null || m_missileSpawnPoints.Length == 0)
        {
            SpawnMissileAt(this.transform, Vector3.zero, direction, seekDelay);
            return;
        }

        for (int i = 0; i < m_missileSpawnPoints.Length; i++)
        {
            Transform spawnPoint = m_missileSpawnPoints[i];
            if (spawnPoint == null) continue;

            Vector3 offset = Vector3.zero;
            if (m_missileSpawnOffsets != null && i < m_missileSpawnOffsets.Length)
                offset = m_missileSpawnOffsets[i];

            SpawnMissileAt(spawnPoint, offset, direction, seekDelay);
        }
    }

    /// <summary>アーク弾の初期方向。ボス正面を上へ m_missileLobAngle 度だけ傾ける。</summary>
    private Vector3 ComputeLobDirection()
    {
        Vector3 fwd = transform.forward;
        if (fwd.sqrMagnitude <= 0.0001f) fwd = Vector3.forward;
        return Vector3.RotateTowards(fwd, Vector3.up, m_missileLobAngle * Mathf.Deg2Rad, 0f).normalized;
    }

    private void SpawnMissileAt(Transform spawnPoint, Vector3 localOffset, Vector3 direction, float seekDelayOverride)
    {
        Vector3 spawnPos = spawnPoint.position + spawnPoint.TransformDirection(localOffset);

        if (direction.sqrMagnitude <= 0.0001f)
            direction = spawnPoint.forward;
        direction = direction.normalized;

        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        EnemyMissile missile = Instantiate(m_missilePrefab, spawnPos, rotation);
        missile.Initialize(m_player, direction, seekDelayOverride);
        // ミサイルは PhysicsObject なので発射元ボスの Pushbox 等と衝突してしまう。spawn 即爆発・自傷を防ぐ。
        missile.IgnoreCollisionsWith(gameObject, m_missileCollisionGrace);
    }

    // === IStabReceiver 実装 (Player → Boss スタブ受信) ===

    /// <summary>
    /// スタブを受け付ける条件。崩れ中 (Stunned=振り上げカウンター / Stagger=スタンゲージ満タン) に加え、
    /// スタン値が満タン (Stamina.IsBroken) の間も true。満タンはスタブを当てるまで維持されるので、
    /// 崩れアニメが終わって取り逃しても、近づいてスタブを決めれば成立する（ソフトロック防止）。
    /// </summary>
    public bool CanReceiveStab => m_state == BossState.Stunned || m_state == BossState.Stagger
        || (m_stamina != null && m_stamina.IsBroken);

    /// <summary>
    /// プレイヤーのスタブAnimEventから呼ばれる。HPバー1本分を一気に削る（クールダウン無視）。
    /// data.damage は無視し、EnemyBossSettings.healthBarSegments と MaxHealth からバー境界HPを算出する。
    /// 死亡判定は Health 側で発火する OnDie に任せる。
    /// </summary>
    public void OnStabHit(StabHitData data)
    {
        if (!CanReceiveStab)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Stunned/Stagger 以外のため Stab 無効"); return; }

        if (m_health == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Health 未取得"); return; }

        if (m_settings == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "EnemyBossSettings 未取得"); return; }

        int segments = Mathf.Max(1, m_settings.healthBarSegments);
        int maxHp = m_health.MaxHealth;
        int curHp = m_health.CurrentHealth;

        // 現在残っているバー本数 → 1本減らした残数まで HP を一気に落とす
        int currentBarsRemaining = Mathf.CeilToInt((float)curHp * segments / maxHp);
        int targetBarsRemaining = Mathf.Max(0, currentBarsRemaining - 1);
        int targetHp = Mathf.FloorToInt((float)targetBarsRemaining * maxHp / segments);
        int damage = Mathf.Max(0, curHp - targetHp);

        if (damage <= 0)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Stab 算出ダメージ0"); return; }

        m_health.DamageIgnoreCooldown(damage);
        OnStabHitSucceeded?.Invoke();
        ChannelLogger.Log("EnemyBossA", $"[Stab] bar {currentBarsRemaining}→{targetBarsRemaining} dmg={damage} src={(data.source != null ? data.source.name : "null")} hp={m_health.CurrentHealth}/{maxHp}");

        // スタブを当てたらスタン値を0にリセットし、崩れを終了させる（1回のスタンにつきスタブ1回）。
        EndBreakAfterStab();
    }

    // スタブ成功時：スタン値を0に戻し、スタン/よろけを終了して Idle へ。これ以上スタブできない＝1回のスタンにつき1回。
    private void EndBreakAfterStab()
    {
        if (m_stamina != null)
            m_stamina.ResetStamina();

        if (m_animator != null)
        {
            if (m_state == BossState.Stunned)
            {
                m_animator.TriggerStunEnd();
                m_animator.SetIsStunnedFalse();
                m_resetStunEndTrigger = true;
            }
            else if (m_state == BossState.Stagger)
            {
                m_animator.TriggerStaggerEnd();
                m_animator.SetIsStaggerFalse();
                m_resetStaggerEndTrigger = true;
            }
        }

        m_staminaBreakEndRequested = true;
        ChangeState(BossState.Idle);
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
        if (m_agent == null || !m_agent.enabled || !m_agent.isOnNavMesh)
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

    private Vector3 GetNavMeshDirection()
    {
        if (m_agent == null)
            return GetDirectionToPlayer();

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

    private Vector3 GetDirectionToPlayer()
    {
        Vector3 dir = m_player.position - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
    }

    private void SyncAgentToBody()
    {
        if (m_agent == null) return;
        if (!m_agent.enabled || !m_agent.isOnNavMesh) return;
        // EntityController が動かした位置を NavMeshAgent に同期し、内部シミュレーションを抑制
        m_agent.nextPosition = transform.position;
        m_agent.velocity = Vector3.zero;
    }

    private void TryRecoverAgent()
    {
        if (m_agent == null) return;
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

        // 突進終了地点。Agent は NavMesh 上でないと有効化できないので一旦サンプル点へ移すが、
        // 最後にここへ戻す。これをしないとボスが NavMesh 最寄り点へ瞬間移動する（突進終了時のワープの原因）。
        Vector3 endPosition = transform.position;

        if (m_agent.enabled)
            m_agent.enabled = false;

        transform.position = hit.position;
        m_agent.enabled = true;

        if (m_agent.isOnNavMesh)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
            m_agent.velocity = Vector3.zero;

            // updatePosition=false なので Agent は内部位置(NavMesh上)を保ったまま、ボスの transform だけ
            // 突進終了地点へ戻す。以降は通常の追従移動で滑らかに NavMesh 上へ戻る（瞬間移動しない）。
            transform.position = endPosition;
            m_agent.nextPosition = endPosition;

            ChannelLogger.Log("EnemyBossA", $"Agent復帰成功 pos={transform.position} hit={hit.position}");
        }
        else
        {
            // 復帰できなかった場合もボスは飛ばさず元の位置へ戻す
            transform.position = endPosition;
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