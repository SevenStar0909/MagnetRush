using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyWalkBase))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyWalkAxeAi : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private CapsuleCollider m_attackBox;
    [SerializeField] private MeshRenderer m_attackBoxMeshRenderer;

    private EnemyWalkBase m_enemyBase;
    private NavMeshAgent m_agent;
    private EnemySettings m_data;
    private Vector3 m_lastDirection;
    private float m_attackTimer;
    private bool m_isAttacking;

    private readonly HashSet<Health> m_hitTargets = new();
    private readonly Collider[] m_overlapResults = new Collider[16];

    private void Awake()
    {
        m_enemyBase = GetComponent<EnemyWalkBase>();
        m_agent = GetComponent<NavMeshAgent>();

        if (m_agent != null)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
        }

        if (m_attackBox == null)
            m_attackBox = FindAttackBox();

        if (m_attackBox != null)
        {
            m_attackBox.gameObject.layer = PhysicsLayers.MeleeHitbox;
            m_attackBox.isTrigger = true;
            m_attackBox.enabled = false;
        }

        if (m_attackBoxMeshRenderer == null && m_attackBox != null)
            m_attackBoxMeshRenderer = m_attackBox.GetComponent<MeshRenderer>();

        if (m_attackBoxMeshRenderer != null)
            m_attackBoxMeshRenderer.enabled = false;
    }

    private void Start()
    {
        m_data = m_enemyBase.StatusData;
        m_attackTimer = m_data != null ? m_data.attackInterval : 0f;

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

        m_attackTimer += Time.deltaTime;

        if (m_enemyBase.IsMagnetControlled)
        {
            SetAttackBoxActive(false);
            return;
        }

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
            TryAttack();
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
            TryAttack();
            return;
        }

        m_enemyBase.AccelerateToward(GetDirectionToPlayer(player), dt);
    }

    private void TryAttack()
    {
        if (m_attackBox == null)
            return;

        if (m_isAttacking)
            return;

        if (m_attackTimer < m_data.attackInterval)
            return;

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        m_isAttacking = true;
        m_attackTimer = 0f;
        m_hitTargets.Clear();
        SetAttackBoxActive(true);

        float timer = 0f;
        float duration = Mathf.Max(0.01f, m_data.attackHitboxDuration);
        while (timer < duration)
        {
            timer += Time.deltaTime;
            CheckAttackBoxOverlapAndDamage();
            yield return null;
        }

        SetAttackBoxActive(false);
        m_isAttacking = false;
    }

    private void SetAttackBoxActive(bool active)
    {
        if (m_attackBox != null)
            m_attackBox.enabled = active;

        if (m_attackBoxMeshRenderer != null)
            m_attackBoxMeshRenderer.enabled = active;
    }

    private void CheckAttackBoxOverlapAndDamage()
    {
        if (m_attackBox == null || !m_attackBox.enabled)
            return;

        GetCapsuleWorldPoints(m_attackBox, out Vector3 p0, out Vector3 p1, out float radius);

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            p0,
            p1,
            radius,
            m_overlapResults,
            1 << PhysicsLayers.Player,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = m_overlapResults[i];
            if (col == null)
                continue;

            TryApplyDamage(col);
        }
    }

    private static void GetCapsuleWorldPoints(CapsuleCollider capsule, out Vector3 p0, out Vector3 p1, out float radius)
    {
        Transform t = capsule.transform;
        Vector3 center = t.TransformPoint(capsule.center);
        Vector3 scale = t.lossyScale;
        float sx = Mathf.Abs(scale.x);
        float sy = Mathf.Abs(scale.y);
        float sz = Mathf.Abs(scale.z);

        Vector3 axis;
        float axisScale;
        float radiusScale;

        switch (capsule.direction)
        {
            case 0:
                axis = t.right;
                axisScale = sx;
                radiusScale = Mathf.Max(sy, sz);
                break;
            case 2:
                axis = t.forward;
                axisScale = sz;
                radiusScale = Mathf.Max(sx, sy);
                break;
            default:
                axis = t.up;
                axisScale = sy;
                radiusScale = Mathf.Max(sx, sz);
                break;
        }

        radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
        float halfLine = Mathf.Max(0f, height * 0.5f - radius);

        p0 = center + axis * halfLine;
        p1 = center - axis * halfLine;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_attackBox == null || !m_attackBox.enabled)
            return;

        TryApplyDamage(other);
    }

    private void TryApplyDamage(Collider other)
    {
        if (other == null)
            return;

        IHittable hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null)
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health != null && !m_hitTargets.Add(health))
            return;

        hittable.OnHit(new HitData
        {
            damage = m_data.attackDamage,
            hitPoint = other.ClosestPoint(transform.position),
            knockbackDir = (other.transform.position - transform.position).normalized,
            source = gameObject
        });
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
        if (m_isAttacking)
            return;

        m_enemyBase.FaceToward(GetDirectionToPlayer(player), dt);
    }

    private CapsuleCollider FindAttackBox()
    {
        CapsuleCollider[] colliders = GetComponentsInChildren<CapsuleCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            CapsuleCollider col = colliders[i];
            if (col != null && col.name.Contains("AttackBox"))
                return col;
        }

        return null;
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
