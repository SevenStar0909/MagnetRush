using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyWalkBase))]
public class EnemyWalkAxeAttack : MonoBehaviour
{
    [Header("Attack Hitbox")]
    [SerializeField] private CapsuleCollider m_attackHitbox;
    [SerializeField] private MeshRenderer m_attackHitboxMeshRenderer;

    private EnemyWalkBase m_enemyBase;
    private EnemySettings m_data;
    private float m_attackTimer;
    private bool m_isAttacking;

    private readonly HashSet<Health> m_hitTargets = new();
    private readonly Collider[] m_overlapResults = new Collider[16];

    public bool IsAttacking => m_isAttacking;

    private void Awake()
    {
        m_enemyBase = GetComponent<EnemyWalkBase>();

        if (m_attackHitbox != null)
        {
            m_attackHitbox.gameObject.layer = PhysicsLayers.MeleeHitbox;
            m_attackHitbox.enabled = false;
        }

        if (m_attackHitboxMeshRenderer != null)
            m_attackHitboxMeshRenderer.enabled = false;
    }

    private void Start()
    {
        m_data = m_enemyBase.StatusData;
        m_attackTimer = m_data != null ? m_data.attackInterval : 0f;

        if (m_attackHitbox != null && m_attackHitbox.transform.IsChildOf(transform) && m_data != null)
        {
            float attackRange = m_data.attackRange;
            m_attackHitbox.height = attackRange;
            m_attackHitbox.center = new Vector3(0f, attackRange * 0.5f, 0f);
        }
    }

    private void Update()
    {
        m_attackTimer += Time.deltaTime;
    }

    public void TryAttack()
    {
        if (m_data == null)
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

        if (m_attackHitbox != null)
            m_attackHitbox.enabled = true;

        if (m_attackHitboxMeshRenderer != null)
            m_attackHitboxMeshRenderer.enabled = true;

        float timer = 0f;
        float duration = Mathf.Max(0.01f, m_data.attackHitboxDuration);
        while (timer < duration)
        {
            timer += Time.deltaTime;
            CheckHitboxOverlapAndDamage();
            yield return null;
        }

        if (m_attackHitbox != null)
            m_attackHitbox.enabled = false;

        if (m_attackHitboxMeshRenderer != null)
            m_attackHitboxMeshRenderer.enabled = false;

        m_isAttacking = false;
    }

    private void CheckHitboxOverlapAndDamage()
    {
        if (m_attackHitbox == null || !m_attackHitbox.enabled || m_data == null)
            return;

        GetCapsuleWorldPoints(m_attackHitbox, out Vector3 p0, out Vector3 p1, out float radius);

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
        if (m_attackHitbox == null || !m_attackHitbox.enabled)
            return;

        TryApplyDamage(other);
    }

    private void TryApplyDamage(Collider other)
    {
        if (other == null || m_data == null)
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
}
