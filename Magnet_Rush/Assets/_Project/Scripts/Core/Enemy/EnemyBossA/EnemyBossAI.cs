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
public class EnemyBossAI : MonoBehaviour
{
    public enum BossState { Idle, Chase, AttackStance, AttackMotion, Stunned, Stagger }

    [Header("References")]
    [SerializeField] private EnemyBossBaseA_Animator m_animator;

    [Header("Debug")]
    [SerializeField] private bool m_logStateChange = true;

    private EnemyBossBase m_boss;
    private NavMeshAgent m_agent;
    private Transform m_player;
    private EnemyBossSettings m_settings;

    private BossState m_state = BossState.Idle;
    private float m_cooldownTimer;
    private float m_staggerTimer;
    private Vector3 m_lastDirection;

    public BossState State => m_state;

    void Awake()
    {
        m_boss = GetComponent<EnemyBossBase>();
        m_agent = GetComponent<NavMeshAgent>();

        if (m_animator == null)
            m_animator = GetComponentInChildren<EnemyBossBaseA_Animator>();

        if (m_agent != null)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
        }
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
        m_staggerTimer  = Mathf.Max(0f, m_staggerTimer  - dt);

        TryRecoverAgent();
        SyncAgentToBody();

        switch (m_state)
        {
            case BossState.Idle:         TickIdle(dt);         break;
            case BossState.Chase:        TickChase(dt);        break;
            case BossState.AttackStance: TickAttackStance(dt); break;
            case BossState.AttackMotion: TickAttackMotion(dt); break;
            case BossState.Stunned:      TickStunned(dt);      break;
            case BossState.Stagger:      TickStagger(dt);      break;
        }
    }

    // === 状態遷移 ===

    private void ChangeState(BossState next)
    {
        if (next == m_state) return;
        if (m_logStateChange)
            ChannelLogger.Log("Enemy", $"[EnemyBossAI] {m_state} → {next}");

        var prev = m_state;
        m_state = next;

        if (prev == BossState.AttackMotion) m_cooldownTimer = m_settings.attackInterval;
        if (prev == BossState.Stunned)      m_staggerTimer  = m_settings.staggerDuration;
        if (prev == BossState.AttackMotion && next == BossState.Stagger)
            m_staggerTimer = m_settings.staggerDuration;
    }

    // === 各状態の Tick ===

    private void TickIdle(float dt)
    {
        if (PlayerInChaseRange())
            ChangeState(BossState.Chase);
        else
            m_boss.SlowDown(dt);
    }

    private void TickChase(float dt)
    {
        // Animator 起点の被弾 → Stunned 化を検知
        if (m_animator.IsStunned) { ChangeState(BossState.Stunned); return; }

        float distance = DistanceToPlayer();

        if (distance > m_settings.chaseRange)
        {
            if (m_agent.enabled && m_agent.isOnNavMesh) m_agent.ResetPath();
            m_boss.SlowDown(dt);
            return;
        }

        if (distance <= m_settings.attackRange && m_cooldownTimer <= 0f)
        {
            if (m_agent.enabled && m_agent.isOnNavMesh) m_agent.ResetPath();
            m_boss.SlowDown(dt);
            FacePlayer(dt);
            // Animator にトリガを送り、State が AttackStance に遷移するのを次フレームで検出して状態を切り替える
            m_animator.TriggerAttack();
            ChangeState(BossState.AttackStance);
            return;
        }

        // 接近停止
        if (distance <= m_settings.stopDistance)
        {
            if (m_agent.enabled && m_agent.isOnNavMesh) m_agent.ResetPath();
            m_boss.SlowDown(dt);
            FacePlayer(dt);
            return;
        }

        MoveTowardPlayer(dt, 1f);
    }

    private void TickAttackStance(float dt)
    {
        // 構え中も向きは追従させる
        FacePlayer(dt);
        m_boss.SlowDown(dt);

        // Animator が BeInterrupted トリガを既に処理して Stun に飛んだ場合
        if (m_animator.IsStunned) { ChangeState(BossState.Stunned); return; }

        // Animator が AttackMotion State に遷移した
        if (m_animator.IsInAttackMotion) { ChangeState(BossState.AttackMotion); return; }
    }

    private void TickAttackMotion(float dt)
    {
        // 振り中は静止 + 向きを保つ
        m_boss.SlowDown(dt);
        FacePlayer(dt);
        // 終了は AnimEvent 経由 OnAttackFinished() でハンドル
    }

    private void TickStunned(float dt)
    {
        m_boss.SlowDown(dt);
        // 復帰は AnimEvent 経由 OnStunEnd() でハンドル
    }

    private void TickStagger(float dt)
    {
        if (m_staggerTimer <= 0f)
        {
            ChangeState(BossState.Chase);
            return;
        }

        // 50% 速度で追跡継続（攻撃判定はしない）
        if (PlayerInChaseRange())
            MoveTowardPlayer(dt, m_settings.staggerMoveMultiplier);
        else
            m_boss.SlowDown(dt);
    }

    // === 公開コールバック (Animator → AI) ===

    /// <summary>AttackMotion clip 末尾の AnimEvent から呼ばれる。Stagger に遷移。</summary>
    public void OnAttackFinished()
    {
        if (m_state == BossState.AttackMotion)
            ChangeState(BossState.Stagger);
    }

    /// <summary>AttackStun clip 末尾の AnimEvent から呼ばれる。Stagger に遷移。</summary>
    public void OnStunEnd()
    {
        if (m_state == BossState.Stunned)
            ChangeState(BossState.Stagger);
    }

    // === ヘルパ ===

    private float DistanceToPlayer()
    {
        return Vector3.Distance(transform.position, m_player.position);
    }

    private bool PlayerInChaseRange()
    {
        return DistanceToPlayer() <= m_settings.chaseRange;
    }

    private void FacePlayer(float dt)
    {
        Vector3 look = m_player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            m_boss.FaceToward(look.normalized, dt);
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

    private Vector3 GetDirectionToPlayer()
    {
        Vector3 dir = m_player.position - transform.position;
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
        if (m_agent == null) { ChannelLogger.LogGuardReturn("Enemy", "NavMeshAgentなし"); return; }
        if (m_agent.enabled && m_agent.isOnNavMesh) { ChannelLogger.LogGuardReturn("Enemy", "Agent既に有効"); return; }

        if (!NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
        { ChannelLogger.LogGuardReturn("Enemy", "NavMeshサンプル失敗"); return; }

        if (m_agent.enabled)
            m_agent.enabled = false;

        transform.position = hit.position;
        m_agent.enabled = true;

        if (m_agent.isOnNavMesh)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
            m_agent.velocity = Vector3.zero;
        }
    }
}
