using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ボスのチェイサー型 AI。NavMeshAgent を経路計算のみに使い、
/// 実際の移動は EnemyBossBase.AccelerateToward() 経由で EntityController が処理する。
/// 状態は明示 enum FSM で管理し、ChangeState() で遷移ログを出す。
/// 依存: EnemyBossBase, EnemyBossBaseA_Animator, NavMeshAgent
/// </summary>
[RequireComponent(typeof(EnemyBossBase))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBossAI : MonoBehaviour
{
    public enum BossState { Idle, Chase, AttackStance, AttackMotion, Stunned, Stagger }

    [Header("References")]
    [SerializeField] private EnemyBossBase m_boss;
    [SerializeField] private EnemyBossBaseA_Animator m_animator;
    [SerializeField] private NavMeshAgent m_agent;

    [Header("Debug")]
    [SerializeField] private bool m_logStateChanges = true;

    private Transform m_player;
    private EnemyBossSettings m_settings;
    private BossState m_state = BossState.Idle;
    private float m_cooldownTimer;
    private float m_staggerTimer;
    private Vector3 m_lastDirection;

    public BossState State => m_state;

    void Awake()
    {
        if (m_boss == null) m_boss = GetComponent<EnemyBossBase>();
        if (m_animator == null) m_animator = GetComponentInChildren<EnemyBossBaseA_Animator>();
        if (m_agent == null) m_agent = GetComponent<NavMeshAgent>();

        if (m_agent != null)
        {
            // EntityController が動かすので NavMeshAgent の自動移動は無効化
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
        }
    }

    void Start()
    {
        m_player = m_boss != null ? m_boss.Player : null;
        m_settings = m_boss != null ? m_boss.StatusData : null;

        if (m_agent != null && m_settings != null)
        {
            m_agent.speed = m_settings.moveSpeed;
            m_agent.stoppingDistance = m_settings.stopDistance;
        }
    }

    void Update()
    {
        if (m_player == null || m_settings == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", "プレイヤー/設定未取得"); return;
        }
        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", "Animator 未設定"); return;
        }

        float dt = Time.deltaTime;
        m_cooldownTimer = Mathf.Max(0f, m_cooldownTimer - dt);
        m_staggerTimer = Mathf.Max(0f, m_staggerTimer - dt);

        TryRecoverAgent();
        SyncAgentToBody();

        switch (m_state)
        {
            case BossState.Idle: TickIdle(dt); break;
            case BossState.Chase: TickChase(dt); break;
            case BossState.AttackStance: TickAttackStance(dt); break;
            case BossState.AttackMotion: TickAttackMotion(dt); break;
            case BossState.Stunned: TickStunned(dt); break;
            case BossState.Stagger: TickStagger(dt); break;
        }
    }

    private void TickIdle(float dt)
    {
        // β では即 Chase
        ChangeState(BossState.Chase);
    }

    private void TickChase(float dt)
    {
        float distance = DistanceToPlayer();

        if (distance > m_settings.chaseRange)
        {
            ChannelLogger.LogGuardReturn("Enemy", "追跡範囲外");
            if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh) m_agent.ResetPath();
            m_boss.SlowDown(dt);
            return;
        }

        if (distance <= m_settings.attackRange && m_cooldownTimer <= 0f)
        {
            if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh) m_agent.ResetPath();
            m_boss.SlowDown(dt);
            m_animator.TriggerAttack();
            ChangeState(BossState.AttackStance);
            return;
        }

        // 通常追跡
        if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
        {
            m_agent.SetDestination(m_player.position);
            m_boss.AccelerateToward(GetNavMeshDirection(), dt);
        }
        else
        {
            m_boss.AccelerateToward(GetDirectionToPlayer(), dt);
        }
    }

    private void TickAttackStance(float dt)
    {
        Vector3 look = GetDirectionToPlayer();
        if (look.sqrMagnitude > 0.0001f) m_boss.FaceToward(look, dt);
        m_boss.SlowDown(dt);

        if (m_animator.IsStunned)
        {
            ChangeState(BossState.Stunned);
            return;
        }

        if (m_animator.IsInAttackMotion)
        {
            ChangeState(BossState.AttackMotion);
        }
    }

    private void TickAttackMotion(float dt)
    {
        m_boss.SlowDown(dt);
        // OnAttackFinished AnimEvent 待ち
    }

    private void TickStunned(float dt)
    {
        m_boss.SlowDown(dt);
        // OnStunEnd AnimEvent 待ち
    }

    private void TickStagger(float dt)
    {
        float distance = DistanceToPlayer();

        if (distance > m_settings.chaseRange)
        {
            m_boss.SlowDown(dt);
        }
        else if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
        {
            m_agent.SetDestination(m_player.position);
            Vector3 dir = GetNavMeshDirection() * m_settings.staggerMoveMultiplier;
            m_boss.AccelerateToward(dir, dt);
        }
        else
        {
            Vector3 dir = GetDirectionToPlayer() * m_settings.staggerMoveMultiplier;
            m_boss.AccelerateToward(dir, dt);
        }

        if (m_staggerTimer <= 0f)
        {
            ChangeState(BossState.Chase);
        }
    }

    private void ChangeState(BossState next)
    {
        if (next == m_state) return;
        var prev = m_state;
        if (m_logStateChanges) Debug.Log($"[EnemyBossAI] {prev} → {next}");
        m_state = next;

        if (prev == BossState.AttackMotion)
            m_cooldownTimer = m_settings.attackInterval;

        if (next == BossState.Stagger)
            m_staggerTimer = m_settings.staggerDuration;
    }

    private void TryRecoverAgent()
    {
        if (m_agent == null) { ChannelLogger.LogGuardReturn("Enemy", "NavMeshAgent 未設定"); return; }
        if (m_agent.enabled && m_agent.isOnNavMesh) return;

        if (!NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
        {
            ChannelLogger.LogGuardReturn("Enemy", "NavMesh サンプル失敗"); return;
        }

        if (m_agent.enabled) m_agent.enabled = false;
        transform.position = hit.position;
        m_agent.enabled = true;
        if (m_agent.isOnNavMesh)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
            m_agent.velocity = Vector3.zero;
        }
    }

    private void SyncAgentToBody()
    {
        if (m_agent == null || !m_agent.enabled || !m_agent.isOnNavMesh) return;
        m_agent.nextPosition = transform.position;
        m_agent.velocity = Vector3.zero;
    }

    private float DistanceToPlayer()
    {
        return m_player == null ? float.PositiveInfinity
            : Vector3.Distance(transform.position, m_player.position);
    }

    private Vector3 GetNavMeshDirection()
    {
        if (m_agent == null || !m_agent.enabled || !m_agent.isOnNavMesh) return GetDirectionToPlayer();
        if (!m_agent.hasPath && !m_agent.pathPending) return GetDirectionToPlayer();
        if (m_agent.pathPending) return m_lastDirection;

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
        if (m_player == null) return Vector3.zero;
        Vector3 dir = m_player.position - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
    }

    /// <summary>AnimEvent (AttackMotion 末尾) から呼ばれる。
    /// Animator のデフォルト遷移で AttackMotion が勝手に再生された場合の誤発火を防ぐため、
    /// AI 側が AttackMotion 状態でなければ無視する。</summary>
    public void OnAttackFinished()
    {
        if (m_state != BossState.AttackMotion)
        {
            ChannelLogger.LogGuardReturn("Enemy", $"OnAttackFinished 無視 (state={m_state})");
            return;
        }
        ChangeState(BossState.Stagger);
    }

    /// <summary>AnimEvent (AttackStun 末尾) から呼ばれる</summary>
    public void OnStunEnd()
    {
        if (m_state != BossState.Stunned)
        {
            ChannelLogger.LogGuardReturn("Enemy", $"OnStunEnd 無視 (state={m_state})");
            return;
        }
        ChangeState(BossState.Stagger);
    }
}
