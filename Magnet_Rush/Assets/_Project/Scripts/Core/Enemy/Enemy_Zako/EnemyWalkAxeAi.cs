using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyWalkBase))]
[RequireComponent(typeof(EnemyWalkAxeAttack))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyWalkAxeAi : MonoBehaviour
{
    private EnemyWalkBase m_enemyBase;
    private EnemyWalkAxeAttack m_attack;
    private NavMeshAgent m_agent;
    private EnemySettings m_data;
    private Vector3 m_lastDirection;

    private void Awake()
    {
        m_enemyBase = GetComponent<EnemyWalkBase>();
        m_attack = GetComponent<EnemyWalkAxeAttack>();
        m_agent = GetComponent<NavMeshAgent>();

        if (m_agent != null)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
        }
    }

    private void Start()
    {
        m_data = m_enemyBase.StatusData;

        if (m_agent != null && m_data != null)
        {
            m_agent.speed = m_data.moveSpeed;
            m_agent.acceleration = m_data.acceleration;
            m_agent.stoppingDistance = m_data.stopDistance;
        }
    }

    private void Update()
    {
        if (m_enemyBase == null || m_data == null)
            return;

        if (m_enemyBase.IsMagnetControlled)
            return;

        Transform player = m_enemyBase.Player;
        if (player == null)
        {
            m_enemyBase.SlowDown(Time.deltaTime);
            return;
        }

        float dt = Time.deltaTime;
        TryRecoverAgent();

        if (!m_agent.enabled || !m_agent.isOnNavMesh)
        {
            TickDirectMove(player, dt);
            return;
        }

        m_agent.nextPosition = transform.position;
        m_agent.velocity = Vector3.zero;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > m_data.chaseRange)
        {
            m_agent.ResetPath();
            m_enemyBase.SlowDown(dt);
            return;
        }

        if (distance <= m_data.attackRange)
        {
            m_agent.ResetPath();
            m_enemyBase.SlowDown(dt);
            FacePlayer(player, dt);
            m_attack.TryAttack();
            return;
        }

        m_agent.SetDestination(player.position);
        m_enemyBase.AccelerateToward(GetNavMeshDirection(player), dt);
    }

    private void TickDirectMove(Transform player, float dt)
    {
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > m_data.chaseRange)
        {
            m_enemyBase.SlowDown(dt);
            return;
        }

        if (distance <= m_data.attackRange)
        {
            m_enemyBase.SlowDown(dt);
            FacePlayer(player, dt);
            m_attack.TryAttack();
            return;
        }

        m_enemyBase.AccelerateToward(GetDirectionToPlayer(player), dt);
    }

    private Vector3 GetNavMeshDirection(Transform player)
    {
        if (m_agent == null || !m_agent.hasPath && !m_agent.pathPending)
            return GetDirectionToPlayer(player);

        if (m_agent.pathPending)
            return m_lastDirection;

        Vector3 direction = m_agent.steeringTarget - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            m_lastDirection = direction.normalized;
            return m_lastDirection;
        }

        return m_lastDirection;
    }

    private Vector3 GetDirectionToPlayer(Transform player)
    {
        if (player == null)
            return Vector3.zero;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private void FacePlayer(Transform player, float dt)
    {
        if (m_attack != null && m_attack.IsAttacking)
            return;

        m_enemyBase.FaceToward(GetDirectionToPlayer(player), dt);
    }

    private void TryRecoverAgent()
    {
        if (m_agent == null)
            return;

        if (m_agent.enabled && m_agent.isOnNavMesh)
            return;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            return;

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
