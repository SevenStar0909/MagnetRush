using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Collide-and-Slideベースの衝突制御。
/// Hitbox/MovementCapsule（disabled CapsuleCollider）の形状で衝突判定し、衝突面に沿ってスライドする。
/// めり込み時はComputePenetrationで押し出す。動的Rigidbodyは押しながらプレイヤーと一緒に動く。
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

    [Tooltip("衝突判定のレイヤーマスク。Awakeで未設定なら自動でPhysicsLayers.MaskEntityCollisionを適用。")]
    public LayerMask collisionLayer = -5;

    private const int k_MaxCollisionSteps = 3;

    private Rigidbody m_rigidbody;
    private CapsuleCollider m_movementCapsule;
    private Transform m_hitboxContainer;
    private Collider[] m_overlaps = new Collider[128];
    private HashSet<Collider> m_ignoredColliders = new();

    // 磁力吸着中の Object を push しないための Owner 側磁極キャッシュ（PD ホルダーが位置を制御するので押しても無意味）
    // Entity asmdef は Magnet asmdef を参照できないため Common の IMagnetPoleProvider を経由する
    private IMagnetPoleProvider m_ownerMagnetPole;

    // 接地判定にアクセスするための同一GO Entity 参照（HandlePenetrationでEntityBody衝突時にY成分削除する判定に使う）
    private Entity m_entity;

    // 押し中のオブジェクト管理
    private readonly List<PushInfo> m_pushActive = new();

    private struct PushInfo
    {
        public Rigidbody rb;
        public Collider col;
    }

    public float radius => m_movementCapsule != null
        ? Mathf.Max(m_movementCapsule.radius, skinWidth) : skinWidth;

    public float height => m_movementCapsule != null
        ? Mathf.Max(m_movementCapsule.height, radius * 2f) : 0f;

    public Vector3 center => m_movementCapsule != null
        ? m_movementCapsule.center : Vector3.zero;

    private Vector3 capsuleOffset => transform.up * (height * 0.5f - radius);

    void Awake()
    {
        // collisionLayerが未設定(0)または旧デフォルト(-5)の場合はPhysicsLayers値で上書き
        if (collisionLayer == 0 || collisionLayer == -5)
            collisionLayer = PhysicsLayers.MaskEntityCollision;

        InitializeRigidbody();
        CreateHitboxContainer();
        InitializeMovementCapsule();
        InitializePushbox();

        // 磁気例外チェック用キャッシュ（親階層探索を毎フレーム避ける）
        m_ownerMagnetPole = GetComponent<IMagnetPoleProvider>();

        // 接地判定参照
        m_entity = GetComponent<Entity>();
    }

    /// <summary>
    /// hit 対象が「自分と異極で磁化された Object」なら push ではなく壁扱いにする。
    /// PD ホルダーが位置を制御しているため push しても無意味 (むしろ箱が無限に動くバグ源)。
    /// </summary>
    private bool IsMagneticallyHeld(Rigidbody hitRb)
    {
        if (m_ownerMagnetPole == null || !m_ownerMagnetPole.IsActive) return false;
        if (hitRb == null) return false;
        var hitMag = hitRb.GetComponent<IMagnetPoleProvider>();
        if (hitMag == null || !hitMag.IsActive) return false;
        if (hitMag.Pole == MagneticPole.None) return false;
        return hitMag.Pole != m_ownerMagnetPole.Pole;
    }

    private void CreateHitboxContainer()
    {
        var existing = transform.Find("Hitbox");
        if (existing != null)
        {
            m_hitboxContainer = existing;
        }
        else
        {
            var go = new GameObject("Hitbox");
            go.transform.SetParent(transform, false);
            m_hitboxContainer = go.transform;
        }

        // Hitbox親コンテナにIHittable実装を集約
        // プレハブで設定済みなら無視、未設定なら警告ログを出してフォールバック追加
        if (m_hitboxContainer.GetComponent<Hitbox>() == null)
        {
            Debug.LogWarning($"[EntityController] {name} のHitbox子オブジェクトに Hitbox コンポーネントがありません。実行時に追加しますが、プレハブで明示設定してください。", this);
            m_hitboxContainer.gameObject.AddComponent<Hitbox>();
        }
    }

    private void InitializeMovementCapsule()
    {
        if (m_hitboxContainer == null) return;

        var mc = m_hitboxContainer.Find("MovementCapsule");
        if (mc == null)
        {
            Debug.LogError($"[EntityController] {name} の Hitbox/MovementCapsule が見つかりません。プレハブで設定してください。", this);
            return;
        }

        m_movementCapsule = mc.GetComponent<CapsuleCollider>();
        if (m_movementCapsule == null)
        {
            Debug.LogError($"[EntityController] {name} の MovementCapsule に CapsuleCollider がありません。プレハブで設定してください。", this);
            return;
        }

        // disabled で運用するルール: enabled だと Bullet/Melee の Trigger イベントが
        // MovementCapsule と Hurtbox 両方で発火し、ダメージ二重適用の事故になる。
        // ComputePenetration は Collider の shape のみ参照（位置は引数指定）するので、
        // disabled でも shape取得可能。
        if (m_movementCapsule.enabled)
            Debug.LogWarning($"[EntityController] {name} の MovementCapsule は disabled で運用してください（物理イベント二重発火防止）。", this);
    }

    private void InitializeRigidbody()
    {
        if (!TryGetComponent(out m_rigidbody))
            m_rigidbody = gameObject.AddComponent<Rigidbody>();

        m_rigidbody.isKinematic = true;
        m_rigidbody.useGravity = false;
        m_rigidbody.interpolation = RigidbodyInterpolation.None;
    }

    private void InitializePushbox()
    {
        // プレハブで静的に配置された EntityBody レイヤーの Collider を無視リストに登録する。
        // 自動生成・自動設定は行わない（プレハブでサイズ・形状を確定させる）。
        // 自身の SweepTest / OverlapCapsule が自 Pushbox を誤検出してジッタる事故を防ぐため。
        var colliders = GetComponentsInChildren<Collider>(true);
        bool found = false;
        foreach (var col in colliders)
        {
            if (col.gameObject.layer == PhysicsLayers.EntityBody)
            {
                m_ignoredColliders.Add(col);
                found = true;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[EntityController] {name} に EntityBody レイヤーの Pushbox が見つかりません。プレハブで設定してください。", this);
        }
    }

    /// <summary>
    /// motionの方向に移動する。壁にはスライドし、めり込みは押し出す。
    /// </summary>
    public Vector3 Move(Vector3 currentPosition, Vector3 motion)
    {
        if (m_movementCapsule == null)
        {
            ChannelLogger.LogGuardReturn("Entity", "MovementCapsule未初期化");
            return currentPosition;
        }

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
        if (m_movementCapsule == null) return;
        var delta = newHeight - m_movementCapsule.height;
        m_movementCapsule.height = newHeight;
        var c = m_movementCapsule.center;
        c.y += delta * 0.5f;
        m_movementCapsule.center = c;
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

            if (colliding && !m_ignoredColliders.Contains(hit.collider))
            {
                // 垂直パスで動的Rigidbodyに当たったら無視して通過（水平パスで押す）
                if (!pushEnabled)
                {
                    var checkRb = hit.collider.attachedRigidbody;
                    if (checkRb != null && !checkRb.isKinematic)
                    {
                        // 磁気吸着中の Object は通過させずに壁扱い（PDホルダーが位置を制御する）
                        if (IsMagneticallyHeld(checkRb)) goto wallHandling;

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
                        // 磁気吸着中の Object は push しない（PD ホルダーが位置を制御する、箱が無限に押されるバグ源）
                        if (IsMagneticallyHeld(hitRb)) goto wallHandling;

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

            // 動的Rigidbodyはめり込み解決しない（押し処理で対応）
            var overlapRb = m_overlaps[i].attachedRigidbody;
            if (overlapRb != null && !overlapRb.isKinematic) continue;

            if (Physics.ComputePenetration(m_movementCapsule, position, transform.rotation,
                m_overlaps[i], m_overlaps[i].transform.position, m_overlaps[i].transform.rotation,
                out var direction, out var dist))
            {
                Vector3 displacement = direction * dist;

                // 接地中の他Entity(EntityBody Pushbox)との衝突は水平のみで解決。
                // ボス上半身(腕/胸)のPushboxが下向き押し成分を生み、プレイヤーが地中に沈む現象の根本対策。
                if (m_entity != null && m_entity.IsGrounded
                    && m_overlaps[i].gameObject.layer == PhysicsLayers.EntityBody)
                {
                    displacement.y = 0f;
                }

                position += displacement;
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
