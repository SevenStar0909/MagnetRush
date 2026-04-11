using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Collide-and-Slideベースの衝突制御。
/// 自前のTrigger CapsuleColliderで衝突判定し、衝突面に沿ってスライドする。
/// めり込み時はComputePenetrationで押し出す。
/// 動的Rigidbodyは押しながらプレイヤーと一緒に動く。
/// 参考: PLAYER TWO Platformer Project の EntityController
/// </summary>
[DefaultExecutionOrder(-100)]
public class EntityController : MonoBehaviour
{
    [Range(0, 180f)]
    [Tooltip("歩行可能な最大傾斜角。これより急な面は壁として扱う。")]
    public float slopeLimit = 45f;

    [Min(0)]
    [Tooltip("段差を登れる高さ。")]
    public float stepOffset = 0.3f;

    [Min(0.0001f)]
    [Tooltip("壁との間に保つスキン幅。ジッター防止。")]
    public float skinWidth = 0.01f;

    [Tooltip("衝突判定用カプセルの中心。")]
    public Vector3 center;

    [Min(0)]
    [SerializeField]
    [Tooltip("衝突判定用カプセルの半径。")]
    private float m_radius = 0.5f;

    [Min(0)]
    [SerializeField]
    [Tooltip("衝突判定用カプセルの高さ。")]
    private float m_height = 2f;

    [Tooltip("衝突判定のレイヤーマスク。Awakeで未設定なら自動でPhysicsLayers.MaskEntityCollisionを適用。")]
    public LayerMask collisionLayer = -5;

    private const int k_MaxCollisionSteps = 3;

    private Rigidbody m_rigidbody;
    private CapsuleCollider m_collider;
    private Collider[] m_overlaps = new Collider[128];
    private HashSet<Collider> m_ignoredColliders = new();

    // 押し中のオブジェクト管理
    private readonly List<PushInfo> m_pushActive = new();

    private struct PushInfo
    {
        public Rigidbody rb;
        public Collider col;
    }

    public float radius
    {
        get => Mathf.Max(m_radius, skinWidth);
        set => m_radius = value;
    }

    public float height
    {
        get => Mathf.Max(m_height, radius * 2f);
        set => m_height = value;
    }

    public new CapsuleCollider collider => m_collider;

    private Vector3 capsuleOffset => transform.up * (height * 0.5f - radius);

    void Awake()
    {
        // collisionLayerが未設定(0)または旧デフォルト(-5)の場合はPhysicsLayers値で上書き
        if (collisionLayer == 0 || collisionLayer == -5)
            collisionLayer = PhysicsLayers.MaskEntityCollision;

        DisableExistingCollider();
        InitializeCollider();
        InitializeRigidbody();
        RefreshCollider();
        WarnExtraColliders();
    }

    /// <summary>
    /// 同一Rigidbody上にEntityController管理外の非triggerコライダーがあれば警告する。
    /// SweepTestで自動除外されるが、意図しない構成を早期に検出するため。
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void WarnExtraColliders()
    {
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col == m_collider) continue;
            if (col.isTrigger) continue;
            if (col.attachedRigidbody != m_rigidbody) continue;
            Debug.LogWarning(
                $"[EntityController] {name}: 非triggerコライダー '{col.GetType().Name}' が同一Rigidbody上にあります。" +
                "SweepTestで自動除外されますが、意図した構成か確認してください。", col);
        }
    }

    private void DisableExistingCollider()
    {
        var existing = GetComponent<CapsuleCollider>();
        if (existing != null)
        {
            m_radius = existing.radius;
            m_height = existing.height;
            center = existing.center;
            existing.enabled = false;
        }
    }

    private void InitializeCollider()
    {
        m_collider = gameObject.AddComponent<CapsuleCollider>();
        m_collider.isTrigger = true;
    }

    private void InitializeRigidbody()
    {
        if (!TryGetComponent(out m_rigidbody))
            m_rigidbody = gameObject.AddComponent<Rigidbody>();

        m_rigidbody.isKinematic = true;
        m_rigidbody.useGravity = false;
        m_rigidbody.interpolation = RigidbodyInterpolation.None;
    }

    private void RefreshCollider()
    {
        m_collider.radius = radius - skinWidth;
        m_collider.height = height - skinWidth;
        m_collider.center = center;
    }

    /// <summary>
    /// motionの方向に移動する。壁にはスライドし、めり込みは押し出す。
    /// </summary>
    public Vector3 Move(Vector3 currentPosition, Vector3 motion)
    {
        // 前フレームの押し状態をリセット
        ReleasePushedObjects();

        var localMotion = transform.InverseTransformDirection(motion);
        var lateralMotion = transform.TransformDirection(new Vector3(localMotion.x, 0, localMotion.z));
        var verticalMotion = transform.TransformDirection(new Vector3(0, localMotion.y, 0));

        currentPosition = MoveAndSlide(currentPosition, lateralMotion, false, motion);
        currentPosition = MoveAndSlide(currentPosition, verticalMotion, true);
        currentPosition = HandlePenetration(currentPosition);

        return currentPosition;
    }

    public void IgnoreCollider(Collider col, bool ignore = true)
    {
        if (ignore)
        {
            if (!m_ignoredColliders.Contains(col))
                m_ignoredColliders.Add(col);
        }
        else
        {
            m_ignoredColliders.Remove(col);
        }
    }

    public void Resize(float newHeight)
    {
        var originalHeight = height;
        height = newHeight;
        var delta = height - originalHeight;
        center += Vector3.up * delta * 0.5f;
        RefreshCollider();
    }

    // --- Collide-and-Slide（pushEnabled時は動的オブジェクトを押す） ---

    private Vector3 MoveAndSlide(Vector3 position, Vector3 motion, bool verticalPass, Vector3 fullMotion = default)
    {
        bool pushEnabled = fullMotion.sqrMagnitude > 0f;

        for (int i = 0; i < k_MaxCollisionSteps; i++)
        {
            float moveDistance = motion.magnitude;
            if (moveDistance <= Mathf.Epsilon) break;

            Vector3 moveDirection = motion / moveDistance;
            float distance = moveDistance + radius - skinWidth;
            Vector3 origin = position + transform.rotation * center - moveDirection * radius;
            Vector3 point1 = origin + capsuleOffset;
            Vector3 point2 = origin - capsuleOffset;

            if (!verticalPass && height > radius * 2f)
                point2 += transform.up * stepOffset;

            bool colliding = SweepTest(origin, point1, point2, moveDirection, distance, out var hit);

            // 自分のRigidbodyに属するコライダーは自動除外（弾検出用の非triggerコライダー等）
            if (colliding && hit.collider.attachedRigidbody != m_rigidbody && !m_ignoredColliders.Contains(hit.collider))
            {
                // 垂直パスで動的Rigidbodyに当たったら無視して通過（水平パスで押す）
                if (!pushEnabled)
                {
                    var checkRb = hit.collider.attachedRigidbody;
                    if (checkRb != null && !checkRb.isKinematic)
                    {
                        m_pushActive.Add(new PushInfo { rb = checkRb, col = hit.collider });
                        m_ignoredColliders.Add(hit.collider);
                        continue;
                    }
                }

                // 動的Rigidbodyの押し処理（水平パス、移動方向がオブジェクトに向かっている場合のみ）
                if (pushEnabled)
                {
                    var hitRb = hit.collider.attachedRigidbody;
                    if (hitRb != null && !hitRb.isKinematic)
                    {
                        // 衝突面に対して正面から押しているか判定（hit.normalベース）
                        Vector3 pushDir = new Vector3(moveDirection.x, 0f, moveDirection.z).normalized;
                        Vector3 surfaceDir = new Vector3(-hit.normal.x, 0f, -hit.normal.z).normalized;
                        float dot = Vector3.Dot(pushDir, surfaceDir);

                        // 面に対して斜め（dot < 0.7 ≒ 45度以上ズレ）なら壁として扱う
                        if (dot < 0.7f) goto wallHandling;

                        float pushAmount = new Vector3(fullMotion.x, 0f, fullMotion.z).magnitude;

                        if (pushDir.sqrMagnitude > 0.01f && pushAmount > 0.001f)
                        {
                            float maxPush = GetMaxPushDistance(hit.collider, pushDir, pushAmount);

                            if (maxPush > 0.001f)
                            {
                                m_pushActive.Add(new PushInfo { rb = hitRb, col = hit.collider });
                                hitRb.isKinematic = true;
                                hitRb.transform.position += pushDir * maxPush;
                                m_ignoredColliders.Add(hit.collider);
                                continue;
                            }
                        }
                    }
                }

                // 壁として止まる+スライド
                wallHandling:
                float safeDistance = hit.distance - skinWidth - radius;
                Vector3 offset = moveDirection * safeDistance;
                Vector3 leftover = motion - offset;
                float angle = Vector3.Angle(transform.up, hit.normal);

                position += offset;

                if (angle <= slopeLimit && verticalPass) continue;

                motion = Vector3.ProjectOnPlane(leftover, hit.normal);

                if (!verticalPass && angle >= slopeLimit)
                    motion -= transform.up * Vector3.Dot(motion, transform.up);
            }
            else
            {
                position += motion;
                break;
            }
        }

        return position;
    }

    // --- Penetration解決 ---

    private Vector3 HandlePenetration(Vector3 position)
    {
        Vector3 origin = position + transform.rotation * center;
        Vector3 point1 = origin + capsuleOffset;
        Vector3 point2 = origin - capsuleOffset;

        int count = Physics.OverlapCapsuleNonAlloc(point1, point2, radius,
            m_overlaps, collisionLayer, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (m_ignoredColliders.Contains(m_overlaps[i])) continue;
            if (m_overlaps[i].attachedRigidbody == m_rigidbody) continue;

            // 動的Rigidbodyはめり込み解決しない（押し処理で対応）
            var overlapRb = m_overlaps[i].attachedRigidbody;
            if (overlapRb != null && !overlapRb.isKinematic) continue;

            if (Physics.ComputePenetration(m_collider, position, transform.rotation,
                m_overlaps[i], m_overlaps[i].transform.position, m_overlaps[i].transform.rotation,
                out var direction, out var dist))
            {
                position += direction * dist;
            }
        }

        return position;
    }

    // --- 押し状態のリセット ---

    private void ReleasePushedObjects()
    {
        foreach (var info in m_pushActive)
        {
            if (info.rb != null)
                info.rb.isKinematic = false;
            if (info.col != null)
                m_ignoredColliders.Remove(info.col);
        }
        m_pushActive.Clear();
    }

    // --- オブジェクト側の壁チェック ---

    /// <summary>
    /// 押されるオブジェクトが壁にぶつからずに移動できる最大距離を返す。
    /// </summary>
    private float GetMaxPushDistance(Collider objCollider, Vector3 direction, float desiredDistance)
    {
        Bounds bounds = objCollider.bounds;
        Vector3 halfExtents = bounds.extents;
        Vector3 center = bounds.center;

        // 押されるオブジェクトのBoundsでBoxCast
        if (Physics.BoxCast(center, halfExtents, direction, out var wallHit,
            Quaternion.identity, desiredDistance + skinWidth, collisionLayer, QueryTriggerInteraction.Ignore))
        {
            // 自分自身のColliderに当たった場合は無視
            if (wallHit.collider == objCollider || wallHit.collider.transform == objCollider.transform)
                return desiredDistance;

            float maxDist = wallHit.distance - skinWidth;
            return Mathf.Max(maxDist, 0f);
        }

        return desiredDistance;
    }

    // --- SweepTest ---

    private bool SweepTest(Vector3 position, Vector3 top, Vector3 bottom,
        Vector3 direction, float distance, out RaycastHit hit)
    {
        bool capsuleHit = Physics.CapsuleCast(top, bottom, radius,
            direction, out hit, distance, collisionLayer, QueryTriggerInteraction.Ignore);
        if (capsuleHit) return true;

        return Physics.Raycast(position, direction, out hit,
            distance, collisionLayer, QueryTriggerInteraction.Ignore);
    }
}
