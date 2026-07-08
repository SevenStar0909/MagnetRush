using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyBoss02Hitboxes : MonoBehaviour
{
    [Header("Attack Colliders")]
    [SerializeField] private SphereCollider m_attackSphere;
    [SerializeField] private Collider m_rushCollider;
    [SerializeField] private MeshRenderer m_attackDebugRenderer;
    [SerializeField] private MeshRenderer m_rushDebugRenderer;

    [Header("References")]
    [SerializeField] private EnemyBossBase m_owner;
    [SerializeField] private EnemyBossSettings m_settings;
    [SerializeField] private EnemyBoss02AI m_ai;

    private readonly HashSet<Health> m_hitTargets = new();
    private readonly Collider[] m_overlapResults = new Collider[32];
    private Collider m_activeCollider;

    private void Awake()
    {
        if (m_owner == null)
            m_owner = GetComponentInParent<EnemyBossBase>();

        if (m_ai == null)
            m_ai = GetComponentInParent<EnemyBoss02AI>();

        if (m_settings == null && m_owner != null)
            m_settings = m_owner.StatusData;

        SetupCollider(m_attackSphere);
        SetupCollider(m_rushCollider);
        SetDebugVisible(false);
    }

    private void OnDisable()
    {
        DisableAll();
    }

    private void LateUpdate()
    {
        if (m_activeCollider != null && m_activeCollider.enabled)
            CheckOverlapAndDamage(m_activeCollider);
    }

    public void EnableAttack()
    {
        Enable(m_attackSphere);
    }

    public void DisableAttack()
    {
        Disable(m_attackSphere);
    }

    public void EnableRush()
    {
        Enable(m_rushCollider);
    }

    public void DisableRush()
    {
        Disable(m_rushCollider);
    }

    public void DisableAll()
    {
        DisableAttack();
        DisableRush();
        m_activeCollider = null;
        m_hitTargets.Clear();
    }

    private void SetupCollider(Collider col)
    {
        if (col == null)
            return;

        col.gameObject.layer = PhysicsLayers.MeleeHitbox;
        col.isTrigger = true;
        col.enabled = false;
    }

    private void Enable(Collider col)
    {
        if (col == null)
            return;

        m_hitTargets.Clear();
        m_activeCollider = col;
        col.enabled = true;
        SetDebugVisible(true);
        Physics.SyncTransforms();
        CheckOverlapAndDamage(col);
    }

    private void Disable(Collider col)
    {
        if (col != null)
            col.enabled = false;

        if (m_activeCollider == col)
            m_activeCollider = null;

        SetDebugVisible(m_activeCollider != null);
    }

    private void CheckOverlapAndDamage(Collider attackCollider)
    {
        if (attackCollider == null || !attackCollider.enabled || m_settings == null)
            return;

        int targetMask = PhysicsLayers.Bit(PhysicsLayers.Player);
        int hitCount = Overlap(attackCollider, targetMask);

        for (int i = 0; i < hitCount; i++)
        {
            Collider other = m_overlapResults[i];
            m_overlapResults[i] = null;
            if (other != null)
                TryApplyDamage(attackCollider, other);
        }
    }

    private int Overlap(Collider attackCollider, int targetMask)
    {
        if (attackCollider is SphereCollider sphere)
        {
            GetSphereWorldShape(sphere, out Vector3 center, out float radius);
            return Physics.OverlapSphereNonAlloc(center, radius, m_overlapResults, targetMask, QueryTriggerInteraction.Collide);
        }

        if (attackCollider is CapsuleCollider capsule)
        {
            GetCapsuleWorldPoints(capsule, out Vector3 p0, out Vector3 p1, out float radius);
            return Physics.OverlapCapsuleNonAlloc(p0, p1, radius, m_overlapResults, targetMask, QueryTriggerInteraction.Collide);
        }

        if (attackCollider is BoxCollider box)
        {
            GetBoxWorldShape(box, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);
            return Physics.OverlapBoxNonAlloc(center, halfExtents, m_overlapResults, rotation, targetMask, QueryTriggerInteraction.Collide);
        }

        Bounds bounds = attackCollider.bounds;
        return Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, m_overlapResults, Quaternion.identity, targetMask, QueryTriggerInteraction.Collide);
    }

    private void TryApplyDamage(Collider attackCollider, Collider other)
    {
        if (other == null || other.transform.IsChildOf(transform))
            return;

        if (attackCollider == m_rushCollider && m_ai != null && m_ai.TryStartRushRepel(other))
            return;

        IHittable hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null)
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health != null && !m_hitTargets.Add(health))
            return;

        Vector3 origin = attackCollider.bounds.center;
        Vector3 direction = other.transform.position - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        hittable.OnHit(new HitData
        {
            damage = m_settings.attackDamage,
            hitPoint = other.ClosestPoint(origin),
            knockbackDir = direction.normalized,
            source = m_owner != null ? m_owner.gameObject : gameObject
        });
    }

    private void SetDebugVisible(bool visible)
    {
        if (m_attackDebugRenderer != null)
            m_attackDebugRenderer.enabled = visible && m_activeCollider == m_attackSphere;

        if (m_rushDebugRenderer != null)
            m_rushDebugRenderer.enabled = visible && m_activeCollider == m_rushCollider;
    }

    private static void GetSphereWorldShape(SphereCollider sphere, out Vector3 center, out float radius)
    {
        Transform t = sphere.transform;
        Vector3 scale = t.lossyScale;
        center = t.TransformPoint(sphere.center);
        radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
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

    private static void GetBoxWorldShape(BoxCollider box, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        Transform t = box.transform;
        Vector3 scale = t.lossyScale;
        center = t.TransformPoint(box.center);
        halfExtents = new Vector3(
            box.size.x * Mathf.Abs(scale.x) * 0.5f,
            box.size.y * Mathf.Abs(scale.y) * 0.5f,
            box.size.z * Mathf.Abs(scale.z) * 0.5f);
        rotation = t.rotation;
    }
}
