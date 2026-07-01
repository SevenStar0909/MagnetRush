using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AttackHitboxes に配置し、右手攻撃と Rush 攻撃の Collider をまとめて管理する。
/// 子 Collider の Trigger は BossAttackHitboxRelay からこのコンポーネントへ転送される。
/// </summary>
public class BossAttackHitboxes : MonoBehaviour
{
    [Header("Attack Colliders")]
    [SerializeField] private CapsuleCollider m_armCollider;
    [SerializeField] private CapsuleCollider m_rushCollider;

    [Header("References")]
    [Tooltip("ダメージ値の参照元 SO")]
    [SerializeField] private EnemyBossSettings m_settings;

    [Tooltip("HitData.source に渡す加害者 GameObject。未設定なら親階層の EnemyBossBase を使う")]
    [SerializeField] private EnemyBossBase m_owner;
    [SerializeField] private EnemyBossAI m_ai;

    // 攻撃ヒットの重複を防ぐため、攻撃開始からヒット対象を記録する HashSet を用意。攻撃終了でクリアする。
    private readonly HashSet<Health> m_armHitTargets = new();
    private readonly HashSet<Health> m_rushHitTargets = new();
    private readonly Collider[] m_overlapResults = new Collider[16];

    private void Awake()
    {
        if (m_owner == null)
            m_owner = GetComponentInParent<EnemyBossBase>();

        if (m_settings == null && m_owner != null)
            m_settings = m_owner.StatusData;
        if (m_ai == null)
            m_ai = GetComponentInParent<EnemyBossAI>();

        // Collider を Trigger にして無効化
        // arm Collider は攻撃開始フレームで有効化、rushCollider は攻撃開始フレームの少し前に有効化する想定
        SetupCollider(m_armCollider, true);
        SetupCollider(m_rushCollider, false);

        if (m_settings == null)
            ChannelLogger.LogError("Enemy", $"[BossAttackHitboxes] {name}: EnemyBossSettings 未設定");
    }

    private void OnDisable()
    {
        DisableAllHitboxes();
    }

    public void EnableArmHitbox()
    {
        EnableHitbox(m_armCollider, m_armHitTargets);
        ChannelLogger.Log("EnemyBoss", $"BossArmHitbox step3 is Triggered ");
    }

    public void DisableArmHitbox()
    {
        DisableHitbox(m_armCollider);
    }

    public void EnableRushHitbox()
    {
        EnableHitbox(m_rushCollider, m_rushHitTargets);
        ChannelLogger.Log("EnemyBoss", $"BossRushHitbox step3 is Triggered ");
    }

    public void DisableRushHitbox()
    {
        DisableHitbox(m_rushCollider);
    }

    public void DisableAllHitboxes()
    {
        DisableArmHitbox();
        DisableRushHitbox();
    }

    public bool TryGetArmHitboxDustPose(out Vector3 position, out float diameter)
    {
        position = default;
        diameter = 0f;

        if (m_armCollider == null)
            return false;

        Bounds bounds = m_armCollider.bounds;
        if (bounds.size.sqrMagnitude <= Mathf.Epsilon)
        {
            GetCapsuleWorldPoints(m_armCollider, out Vector3 p0, out Vector3 p1, out float radius);
            position = (p0 + p1) * 0.5f;
            diameter = Mathf.Max(radius * 2f, Vector3.Distance(p0, p1) + radius * 2f);
            return diameter > 0f;
        }

        position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        diameter = Mathf.Max(bounds.size.x, bounds.size.z);
        if (diameter <= Mathf.Epsilon)
            diameter = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

        return diameter > 0f;
    }

    private void SetupCollider(CapsuleCollider attackCollider, bool isArm)
    {
        if (attackCollider == null)
            return;

        attackCollider.gameObject.layer = PhysicsLayers.MeleeHitbox;
        attackCollider.isTrigger = true;
        attackCollider.enabled = false;

        var relay = attackCollider.GetComponent<BossAttackHitboxRelay>();
        if (relay == null)
        {
            ChannelLogger.LogError(
                "Enemy",
                $"[BossAttackHitboxes] {attackCollider.name}: BossAttackHitboxRelay 未設定");
            return;
        }

        relay.Initialize(this, attackCollider, isArm);
    }

    private void EnableHitbox(CapsuleCollider attackCollider, HashSet<Health> hitTargets)
    {
        hitTargets.Clear();

        if (attackCollider == null)
        {
            ChannelLogger.LogError("EnemyBoss", "EnableHitbox failed: attackCollider is null");
            return;
        }

        attackCollider.enabled = true;
        Physics.SyncTransforms();
        CheckHitboxOverlapAndDamage(attackCollider, hitTargets);

        ChannelLogger.Log(
            "EnemyBoss",
            $"BossAttack step4: {attackCollider.name} enabled={attackCollider.enabled}"
        );
    }

    private static void DisableHitbox(CapsuleCollider attackCollider)
    {
        if (attackCollider != null)
            attackCollider.enabled = false;
    }

    internal void HandleTriggerEnter(Collider attackCollider, Collider other, bool isArm)
    {
        if (attackCollider == null || !attackCollider.enabled || m_settings == null || other == null)
            return;

        TryApplyDamage(attackCollider, other, isArm ? m_armHitTargets : m_rushHitTargets);
    }

    private void CheckHitboxOverlapAndDamage(
        CapsuleCollider attackCollider,
        HashSet<Health> hitTargets)
    {
        if (attackCollider == null || !attackCollider.enabled || m_settings == null)
            return;

        int playerMask = PhysicsLayers.Bit(PhysicsLayers.Player);
        if (playerMask == 0)
            return;

        GetCapsuleWorldPoints(attackCollider, out Vector3 p0, out Vector3 p1, out float radius);

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            p0,
            p1,
            radius,
            m_overlapResults,
            playerMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider other = m_overlapResults[i];
            m_overlapResults[i] = null;

            if (other != null)
                TryApplyDamage(attackCollider, other, hitTargets);
        }
    }

    private void TryApplyDamage(
        Collider attackCollider,
        Collider other,
        HashSet<Health> hitTargets)
    {
        var hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null)
            return;

        var health = other.GetComponentInParent<Health>();
        if (health != null && !hitTargets.Add(health))
            return;

        Vector3 origin = attackCollider.transform.position;
        hittable.OnHit(new HitData
        {
            damage = m_settings.attackDamage,
            hitPoint = other.ClosestPoint(origin),
            knockbackDir = (other.transform.position - origin).normalized,
            source = m_owner != null ? m_owner.gameObject : gameObject
        });

        if (attackCollider == m_rushCollider && m_ai != null)
            m_ai.TryApplyRushKnockback(other, origin, attackColliderForward());
    }

    private Vector3 attackColliderForward()
    {
        if (m_rushCollider != null)
        {
            Vector3 forward = m_rushCollider.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                return forward.normalized;
        }

        Vector3 ownerForward = transform.forward;
        ownerForward.y = 0f;
        return ownerForward.sqrMagnitude > 0.0001f ? ownerForward.normalized : Vector3.forward;
    }

    private static void GetCapsuleWorldPoints(
        CapsuleCollider capsule,
        out Vector3 p0,
        out Vector3 p1,
        out float radius)
    {
        Transform t = capsule.transform;
        Vector3 center = t.TransformPoint(capsule.center);
        Vector3 scale = Abs(t.lossyScale);

        Vector3 axis;
        float axisScale;
        float radiusScale;

        switch (capsule.direction)
        {
            case 0:
                axis = t.right;
                axisScale = scale.x;
                radiusScale = Mathf.Max(scale.y, scale.z);
                break;
            case 2:
                axis = t.forward;
                axisScale = scale.z;
                radiusScale = Mathf.Max(scale.x, scale.y);
                break;
            default:
                axis = t.up;
                axisScale = scale.y;
                radiusScale = Mathf.Max(scale.x, scale.z);
                break;
        }

        radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
        float halfLine = Mathf.Max(0f, height * 0.5f - radius);

        p0 = center + axis * halfLine;
        p1 = center - axis * halfLine;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }
}
