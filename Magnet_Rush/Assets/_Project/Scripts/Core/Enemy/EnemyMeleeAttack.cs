using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(EnemyBase))]
public class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Attack Hitbox")]
    [SerializeField] private CapsuleCollider m_attackHitbox;
    [SerializeField] private MeshRenderer m_attackHitboxMeshRenderer;

    [Header("Weapon Swing")]
    [SerializeField] private float m_weaponSwingAngle = 90f;
    [SerializeField] private float m_weaponSwingForwardDuration = 0.12f;
    [SerializeField] private float m_weaponSwingReturnDuration = 0.12f;

    private EnemyBase m_enemyBase;
    private EnemySettings m_data;

    private float m_attackTimer;
    private bool m_isAttacking;
    public bool IsAttacking => m_isAttacking;

    private readonly HashSet<Health> m_hitTargets = new HashSet<Health>();
    private readonly Collider[] m_overlapResults = new Collider[16];

    private CapsuleCollider m_unarmedAttackHitbox;
    private MeshRenderer m_unarmedAttackHitboxMeshRenderer;

    private void Awake()
    {
        m_enemyBase = GetComponent<EnemyBase>();

        // 初期状態を空手攻撃用Hitboxとして保存。攻撃開始時に武器Hitboxがあればそちらに切り替えるが、なければこれを使う。
        m_unarmedAttackHitbox = m_attackHitbox;
        m_unarmedAttackHitboxMeshRenderer = m_attackHitboxMeshRenderer;

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

        if (m_attackHitbox != null && m_attackHitbox.transform.IsChildOf(transform))
        {
            float atkR = m_data.attackRange;
            m_attackHitbox.height = atkR;
            m_attackHitbox.center = new Vector3(0f, atkR / 2f, 0f);
        }

        if (m_attackHitboxMeshRenderer != null && m_attackHitboxMeshRenderer.transform.IsChildOf(transform))
        {
            float atkR = m_data.attackRange;
            float currentY = m_attackHitboxMeshRenderer.transform.localPosition.y;
            m_attackHitboxMeshRenderer.transform.localPosition = new Vector3(0f, currentY, atkR / 2f);
            m_attackHitboxMeshRenderer.transform.localScale = new Vector3(1f, atkR / 2f, 1f);
        }
    }

    private void Update()
    {
        m_attackTimer += Time.deltaTime;
    }

    public void TryAttack()
    {
        if (m_data == null) { ChannelLogger.LogGuardReturn("Enemy", "EnemySettings未設定"); return; }
        if (m_isAttacking) { ChannelLogger.LogGuardReturn("Enemy", "攻撃中"); return; }
        if (m_attackTimer < m_data.attackInterval) { ChannelLogger.LogGuardReturn("Enemy", "攻撃クールダウン中"); return; }

        StartCoroutine(AttackRoutine());
    }

    // 攻撃後は親相対姿勢を優先復元して武器ズレを防ぐ
    private IEnumerator AttackRoutine()
    {
        m_isAttacking = true;
        m_attackTimer = 0f;
        m_hitTargets.Clear();

        // 攻撃開始時点での武器状態に応じてHitboxを切り替え。武器があれば武器Hitbox、なければ空手Hitboxを使う。
        m_attackHitbox = m_unarmedAttackHitbox;
        m_attackHitboxMeshRenderer = m_unarmedAttackHitboxMeshRenderer;

        WeaponStateController weapon = m_enemyBase.HasWeapon ? m_enemyBase.EquippedWeapon : null;
        Transform weaponTransform = weapon != null ? weapon.transform : null;
        Transform gripPoint = weapon != null ? weapon.GripPoint : null;

        if (weapon != null)
        {
            ResolveAttackHitboxFromWeapon(weapon);
            weapon.BeginAttackWindow();
        }

        if (m_attackHitbox != null) m_attackHitbox.enabled = true;
        if (m_attackHitboxMeshRenderer != null) m_attackHitboxMeshRenderer.enabled = true;

        // 武器がある場合は、グリップポイントを中心に武器を振るアニメーションを再生しつつ、攻撃ヒットの判定も行う。
        // 武器がない場合は、単純に攻撃ヒットの判定だけ行う。
        if (weaponTransform != null)
        {
            Transform startParent = weaponTransform.parent;
            Vector3 startLocalPosition = weaponTransform.localPosition;
            Quaternion startLocalRotation = weaponTransform.localRotation;

            Vector3 startPosition = weaponTransform.position;
            Quaternion startRotation = weaponTransform.rotation;

            Vector3 pivotWorld = gripPoint != null ? gripPoint.position : weaponTransform.position;
            Vector3 axisWorld = m_enemyBase != null ? m_enemyBase.transform.right : transform.right;
            if (axisWorld.sqrMagnitude <= 0.0001f) axisWorld = Vector3.right;
            axisWorld.Normalize();

            float signedForwardAngle = ResolveSignedForwardAngle(startRotation, axisWorld);

            yield return RotateWeaponAroundGripWithHitCheck(
                weaponTransform, pivotWorld, startPosition, startRotation,
                axisWorld, 0f, signedForwardAngle, m_weaponSwingForwardDuration, true);

            float remainHitTime = Mathf.Max(0f, m_data.attackHitboxDuration - m_weaponSwingForwardDuration);
            float holdTimer = 0f;
            while (holdTimer < remainHitTime)
            {
                holdTimer += Time.deltaTime;
                CheckHitboxOverlapAndDamage();
                yield return null;
            }

            if (m_attackHitbox != null) m_attackHitbox.enabled = false;
            if (m_attackHitboxMeshRenderer != null) m_attackHitboxMeshRenderer.enabled = false;
            weapon.EndAttackWindow();

            yield return RotateWeaponAroundGripWithHitCheck(
                weaponTransform, pivotWorld, startPosition, startRotation,
                axisWorld, signedForwardAngle, 0f, m_weaponSwingReturnDuration, false);

            // 攻撃前の手ソケット相対姿勢を優先して復元（ズレ防止）
            if (startParent != null && weaponTransform.parent == startParent)
            {
                weaponTransform.localPosition = startLocalPosition;
                weaponTransform.localRotation = startLocalRotation;
            }
            else
            {
                weaponTransform.position = startPosition;
                weaponTransform.rotation = startRotation;
            }
        }
        else
        {
            // 空手攻擊（AtkHitboxC）
            yield return new WaitForSeconds(m_data.attackHitboxDuration);

            if (m_attackHitbox != null) m_attackHitbox.enabled = false;
            if (m_attackHitboxMeshRenderer != null) m_attackHitboxMeshRenderer.enabled = false;
        }

        m_isAttacking = false;
    }

    private float ResolveSignedForwardAngle(Quaternion startRotation, Vector3 axisWorld)
    {
        float absAngle = Mathf.Abs(m_weaponSwingAngle);
        if (absAngle <= 0.001f) return 0f;

        Vector3 enemyForward = m_enemyBase != null ? m_enemyBase.transform.forward : transform.forward;
        Vector3 currentForward = startRotation * Vector3.forward;

        Vector3 plusForward = Quaternion.AngleAxis(absAngle, axisWorld) * currentForward;
        Vector3 minusForward = Quaternion.AngleAxis(-absAngle, axisWorld) * currentForward;

        float plusDot = Vector3.Dot(plusForward, enemyForward);
        float minusDot = Vector3.Dot(minusForward, enemyForward);

        return plusDot >= minusDot ? absAngle : -absAngle;
    }
    // 武器をグリップポイントを中心に回転させるコルーチン。回転中は毎フレームヒットボックスの重なりをチェックする。pivotWorld はグリップポイントのワールド位置、axisWorld は回転軸のワールド方向。
    private IEnumerator RotateWeaponAroundGripWithHitCheck(
        Transform weaponTransform,
        Vector3 pivotWorld,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 axisWorld,
        float fromAngle,
        float toAngle,
        float duration,
        bool checkHitbox)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < safeDuration)
        {
            t += Time.deltaTime;
            float rate = Mathf.Clamp01(t / safeDuration);
            float angle = Mathf.Lerp(fromAngle, toAngle, rate);

            ApplyWeaponPoseAroundGrip(weaponTransform, pivotWorld, startPosition, startRotation, axisWorld, angle);

            if (checkHitbox)
                CheckHitboxOverlapAndDamage();

            yield return null;
        }

        ApplyWeaponPoseAroundGrip(weaponTransform, pivotWorld, startPosition, startRotation, axisWorld, toAngle);
    }

    // 武器をグリップポイントを中心に回転させる。pivotWorld はグリップポイントのワールド位置、axisWorld は回転軸のワールド方向。
    private static void ApplyWeaponPoseAroundGrip(
        Transform weaponTransform,  // 回転させる武器のTransform
        Vector3 pivotWorld,         // グリップポイントのワールド位置
        Vector3 startPosition,      // 武器の回転開始前のワールド位置
        Quaternion startRotation,   // 武器の回転開始前のワールド回転
        Vector3 axisWorld,          // 回転軸のワールド方向
        float angle)                // 回転させる角度（度数）
    {
        Quaternion delta = Quaternion.AngleAxis(angle, axisWorld);
        weaponTransform.position = pivotWorld + delta * (startPosition - pivotWorld);
        weaponTransform.rotation = delta * startRotation;
    }

    // 武器から攻撃用のヒットボックスを探して設定する。
    // 武器のAttackTriggerがCapsuleColliderならそれを使い、そうでなければ子オブジェクトから名前に"attack"を含むCapsuleColliderを探す。
    // それもなければ最初に見つかったCapsuleColliderを使う。
    private void ResolveAttackHitboxFromWeapon(WeaponStateController weapon)
    {
        if (weapon == null) { ChannelLogger.LogGuardReturn("Enemy", "武器参照なし"); return; }

        CapsuleCollider capsule = null;

        if (weapon.AttackTrigger is CapsuleCollider triggerCapsule)
        {
            capsule = triggerCapsule;
        }
        else
        {
            CapsuleCollider[] childCapsules = weapon.GetComponentsInChildren<CapsuleCollider>(true);
            for (int i = 0; i < childCapsules.Length; i++)
            {
                CapsuleCollider c = childCapsules[i];
                if (c == null) continue;

                if (c.name.ToLowerInvariant().Contains("attack"))
                {
                    capsule = c;
                    break;
                }
            }

            if (capsule == null && childCapsules.Length > 0)
                capsule = childCapsules[0];
        }

        if (capsule != null)
        {
            m_attackHitbox = capsule;
            m_attackHitbox.gameObject.layer = PhysicsLayers.MeleeHitbox;
            m_attackHitboxMeshRenderer = m_attackHitbox.GetComponent<MeshRenderer>();
        }
    }

    private void CheckHitboxOverlapAndDamage()
    {
        if (m_attackHitbox == null) { ChannelLogger.LogGuardReturn("Enemy", "攻撃Hitbox未設定"); return; }
        if (!m_attackHitbox.enabled) { ChannelLogger.LogGuardReturn("Enemy", "攻撃Hitbox無効"); return; }

        GetCapsuleWorldPoints(m_attackHitbox, out Vector3 p0, out Vector3 p1, out float radius);

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            p0, p1, radius, m_overlapResults, 1 << PhysicsLayers.Player, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = m_overlapResults[i];
            if (col == null) continue;

            TryApplyDamage(col);
        }
    }

    private static void GetCapsuleWorldPoints(CapsuleCollider capsule, out Vector3 p0, out Vector3 p1, out float radius)
    {
        Transform t = capsule.transform;
        Vector3 center = t.TransformPoint(capsule.center);

        Vector3 lossy = t.lossyScale;
        float sx = Mathf.Abs(lossy.x);
        float sy = Mathf.Abs(lossy.y);
        float sz = Mathf.Abs(lossy.z);

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
        float worldHeight = Mathf.Max(capsule.height * axisScale, radius * 2f);
        float halfLine = Mathf.Max(0f, worldHeight * 0.5f - radius);

        p0 = center + axis * halfLine;
        p1 = center - axis * halfLine;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_attackHitbox == null) { ChannelLogger.LogGuardReturn("Enemy", "攻撃Hitbox未設定"); return; }
        if (!m_attackHitbox.enabled) { ChannelLogger.LogGuardReturn("Enemy", "攻撃Hitbox無効"); return; }

        TryApplyDamage(other);
    }

    private void TryApplyDamage(Collider other)
    {
        if (other == null) { ChannelLogger.LogGuardReturn("Enemy", "対象Colliderなし"); return; }
        if (m_data == null) { ChannelLogger.LogGuardReturn("Enemy", "EnemySettings未設定"); return; }

        // Hurtbox 子コライダー直撃でも親 Hitbox の IHittable に到達する
        var hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null) { ChannelLogger.LogGuardReturn("Enemy", "IHittable未実装"); return; }

        // 重複ダメージ防止（HashSetでフレーム内1回のみ）
        var health = other.GetComponentInParent<Health>();
        if (health != null && !m_hitTargets.Add(health)) { ChannelLogger.LogGuardReturn("Enemy", "同一フレーム重複ヒット"); return; }

        hittable.OnHit(new HitData
        {
            damage = m_data.attackDamage,
            hitPoint = other.ClosestPoint(transform.position),
            knockbackDir = (other.transform.position - transform.position).normalized,
            source = gameObject
        });
    }
}
