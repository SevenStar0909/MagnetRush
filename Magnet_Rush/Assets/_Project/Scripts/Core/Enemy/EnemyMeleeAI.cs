using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 近接敵のAI。NavMeshAgentを経路計算のみに使い、
/// 実際の移動はEnemyBase.AccelerateToward()経由でEntityControllerが処理する。
/// </summary>
[RequireComponent(typeof(EnemyBase))]
[RequireComponent(typeof(EnemyMeleeAttack))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMeleeAI : MonoBehaviour
{
    private EnemyBase m_enemyBase;
    private EnemyMeleeAttack m_meleeAttack;
    private NavMeshAgent m_agent;
    private Transform m_player;
    private EnemySettings m_data;

    private WeaponStateController m_targetWeapon;
    private Vector3 m_lastDirection;

    private void Awake()
    {
        m_enemyBase = GetComponent<EnemyBase>();
        m_meleeAttack = GetComponent<EnemyMeleeAttack>();
        m_agent = GetComponent<NavMeshAgent>();

        // NavMeshは経路計算のみ。実際の移動はEntityControllerが行う
        if (m_agent != null)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
        }
    }

    private void Start()
    {
        m_player = m_enemyBase.Player;
        m_data = m_enemyBase.StatusData;

        if (m_agent != null && m_data != null)
        {
            m_agent.speed = m_data.moveSpeed;
            m_agent.stoppingDistance = m_data.stopDistance;
        }
    }

    private void Update()
    {
        if (m_enemyBase.IsMagnetControlled) { ChannelLogger.LogGuardReturn("Enemy", "磁力制御中"); return; }
        if (m_player == null || m_agent == null || m_data == null) { ChannelLogger.LogGuardReturn("Enemy", "プレイヤー/Agent/データ未取得"); return; }

        float dt = Time.deltaTime;

        TryRecoverAgent();

        // NavMesh外ではagent APIを呼ばず直線移動にフォールバック
        if (!m_agent.enabled || !m_agent.isOnNavMesh)
        {
            FallbackDirectMove(dt);
            return;
        }

        // EntityControllerが動かした位置をNavMeshAgentに同期し、内部シミュレーションを抑制
        m_agent.nextPosition = transform.position;
        m_agent.velocity = Vector3.zero;

        if (!m_enemyBase.HasWeapon)
        {
            bool isHandlingWeapon = HandleWeaponSearchAndPickup(dt);
            if (isHandlingWeapon) { ChannelLogger.LogGuardReturn("Enemy", "武器ピックアップ処理中"); return; }
        }

        float distance = Vector3.Distance(transform.position, m_player.position);

        if (distance > m_data.chaseRange)
        {
            ChannelLogger.LogGuardReturn("Enemy", "追跡範囲外");
            m_agent.ResetPath();
            m_enemyBase.SlowDown(dt);
            return;
        }

        if (distance <= m_data.attackRange)
        {
            m_agent.ResetPath();
            m_enemyBase.SlowDown(dt);

            if (!m_meleeAttack.IsAttacking)
            {
                Vector3 look = m_player.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.0001f)
                    m_enemyBase.FaceToward(look.normalized, dt);
            }

            m_meleeAttack.TryAttack();
        }
        else
        {
            m_agent.SetDestination(m_player.position);
            Vector3 dir = GetNavMeshDirection();
            m_enemyBase.AccelerateToward(dir, dt);
        }
    }

    /// <summary>
    /// NavMeshAgentの次のパスコーナーから水平方向を取得する。
    /// </summary>
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
        if (m_player == null) return Vector3.zero;
        Vector3 dir = m_player.position - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
    }

    /// <summary>
    /// NavMesh外での直線移動フォールバック。agent APIを一切使わない。
    /// </summary>
    private void FallbackDirectMove(float dt)
    {
        float dist = Vector3.Distance(transform.position, m_player.position);
        if (dist > m_data.chaseRange) { ChannelLogger.LogGuardReturn("Enemy", "追跡範囲外(フォールバック)"); return; }

        if (dist <= m_data.attackRange)
        {
            m_enemyBase.SlowDown(dt);
            if (!m_meleeAttack.IsAttacking)
            {
                Vector3 look = m_player.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.0001f)
                    m_enemyBase.FaceToward(look.normalized, dt);
            }
            m_meleeAttack.TryAttack();
            return;
        }

        m_enemyBase.AccelerateToward(GetDirectionToPlayer(), dt);
    }

    private bool HandleWeaponSearchAndPickup(float dt)
    {
        if (m_targetWeapon == null || !m_targetWeapon.CanBePickedUp || m_targetWeapon.IsMagnetAffected)
        {
            if (WeaponManager.Instance == null)
            {
                m_targetWeapon = null;
                return false;
            }
            m_targetWeapon = WeaponManager.Instance.FindNearestPickableWeapon(
                transform.position, m_data.chaseRange, true);
        }

        if (m_targetWeapon == null) return false;

        float distanceToWeapon = Vector3.Distance(transform.position, m_targetWeapon.transform.position);

        if (distanceToWeapon <= m_data.stopDistance + 1f)
        {
            if (!m_targetWeapon.CanBePickedUp || m_targetWeapon.IsMagnetAffected)
            {
                m_targetWeapon = null;
                return false;
            }

            m_agent.ResetPath();
            m_enemyBase.SlowDown(dt);

            bool equipped = m_enemyBase.TryEquipWeapon(m_targetWeapon);
            if (equipped) m_targetWeapon = null;
            return true;
        }

        m_agent.SetDestination(m_targetWeapon.transform.position);
        m_enemyBase.AccelerateToward(GetNavMeshDirection(), dt);
        return true;
    }

    /// <summary>
    /// additive読み込みでNavMeshが後からロードされた場合にagentを再配置する。
    /// </summary>
    private void TryRecoverAgent()
    {
        if (m_agent == null) { ChannelLogger.LogGuardReturn("Enemy", "NavMeshAgentなし"); return; }
        if (m_agent.enabled && m_agent.isOnNavMesh) { ChannelLogger.LogGuardReturn("Enemy", "Agent既に有効"); return; }

        if (!NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
        {
            ChannelLogger.LogGuardReturn("Enemy", "NavMeshサンプル失敗");
            return;
        }

        // agentが有効なら一度無効化してリセット
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
