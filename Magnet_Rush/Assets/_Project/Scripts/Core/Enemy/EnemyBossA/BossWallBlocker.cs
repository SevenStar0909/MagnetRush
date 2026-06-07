using UnityEngine;

/// <summary>
/// アニメーションでボス本体が壁にめり込むのを防ぐ。
/// ボスは EntityController により runtime で kinematic 化されるため、物理では壁に押し返されない。
/// その代替として、ボス全体を覆うカプセル形状で2段構えの壁判定を LateUpdate（アニメ適用後）に行う:
///   (1) 前フレームからの水平移動を CapsuleCast でスイープし、経路上の壁の手前で止める。
///       レイ/スイープ系なので非凸 MeshCollider にも効き、トンネリングも防ぐ。
///   (2) 残った凸コライダーへのめり込みを ComputePenetration で押し戻す（Box 等の取りこぼし回収）。
/// 潜った地面などは CapsuleCast が距離0で返すため、距離0付近の初期重なりは無視し、
/// 経路上で新たに当たった壁だけをクランプする（地面に潜るとボスが完全停止する事故を防ぐ）。
/// 依存: 形状参照用の CapsuleCollider（判定専用なので disabled で良い）。
/// </summary>
[DisallowMultipleComponent]
public class BossWallBlocker : MonoBehaviour
{
    [SerializeField]
    [Tooltip("ボス全体を覆う形状参照用カプセル。判定にだけ使うので disabled（物理イベントは発火させない）")]
    private CapsuleCollider m_bodyCapsule;

    [SerializeField]
    [Tooltip("止める相手の環境レイヤー（Default/Ground/Wall）。プレイヤーや敵は入れない")]
    private LayerMask m_wallMask;

    [SerializeField]
    [Tooltip("壁から離す余白。これより浅いヒット（=ほぼ接触/初期重なり）はクランプしない")]
    private float m_skinWidth = 0.08f;

    [SerializeField]
    [Tooltip("ComputePenetration の押し戻しを繰り返す回数（角で複数の凸壁に同時に刺さった時用）")]
    private int m_maxIterations = 2;

    [SerializeField]
    [Tooltip("水平方向だけ補正する（壁に当たって上へ跳ね上がる/床を突き上げるのを防ぐ）")]
    private bool m_horizontalOnly = true;

    private readonly Collider[] m_overlaps = new Collider[8];
    private readonly RaycastHit[] m_hits = new RaycastHit[12];
    private Vector3 m_prevPosition;
    private bool m_hasPrev;

    private void Reset()
    {
        m_bodyCapsule = GetComponentInChildren<CapsuleCollider>();
        m_wallMask = (1 << LayerMask.NameToLayer("Default"))
            | (1 << LayerMask.NameToLayer("Ground"))
            | (1 << LayerMask.NameToLayer("Wall"));
    }

    private void OnEnable()
    {
        m_prevPosition = transform.position;
        m_hasPrev = true;
    }

    // 移動・衝突解決は EnemyBossBase.Update() で走り、その後にアニメーションが本体を動かす。
    // よって補正はアニメ適用後・描画前の LateUpdate で行う。
    private void LateUpdate()
    {
        if (m_bodyCapsule == null) { ChannelLogger.LogGuardReturn("Enemy", "BossWallBlocker: bodyCapsule 未設定"); return; }
        if (m_wallMask == 0) { ChannelLogger.LogGuardReturn("Enemy", "BossWallBlocker: wallMask 未設定"); return; }

        if (!m_hasPrev) { m_prevPosition = transform.position; m_hasPrev = true; return; }

        // (1) この1フレームの水平移動をスイープして壁の手前で止める（非凸 Mesh 対応・トンネリング防止）
        SweepClampHorizontal();

        // (2) 残った凸コライダーへのめり込みを押し戻す（Box 等の取りこぼし）
        for (int i = 0; i < m_maxIterations; i++)
        {
            if (!ResolvePenetration())
                break;
        }

        m_prevPosition = transform.position;
    }

    /// <summary>前フレームからの水平移動を CapsuleCast し、経路上の壁の手前で止める（必要なら壁沿いにスライド）。</summary>
    private void SweepClampHorizontal()
    {
        Vector3 current = transform.position;
        Vector3 delta = current - m_prevPosition;
        delta.y = 0f;

        float distance = delta.magnitude;
        if (distance < 1e-4f)
            return;

        Vector3 dir = delta / distance;

        // スイープ始点は「前フレームの水平位置・現在の高さ」。前フレームは壁外なので経路ヒットを正しく拾える。
        Vector3 sweepRoot = new Vector3(m_prevPosition.x, current.y, m_prevPosition.z);

        if (!TrySweep(sweepRoot, dir, distance, out float allowed, out Vector3 normal))
            return;

        Vector3 clamped = sweepRoot + dir * allowed;

        // 残り移動を壁平面へ射影し、壁に沿ってスライドさせる
        Vector3 remaining = dir * (distance - allowed);
        Vector3 slide = Vector3.ProjectOnPlane(remaining, normal);
        slide.y = 0f;

        float slideDistance = slide.magnitude;
        if (slideDistance > 1e-4f)
        {
            Vector3 slideDir = slide / slideDistance;
            if (TrySweep(clamped, slideDir, slideDistance, out float slideAllowed, out _))
                clamped += slideDir * slideAllowed;
            else
                clamped += slide;
        }

        transform.position = new Vector3(clamped.x, current.y, clamped.z);
    }

    /// <summary>
    /// origin からの水平 CapsuleCast。距離0付近の初期重なり（潜った地面など）は無視し、
    /// 経路上で新たに当たった最短ヒットの許容移動量と法線を返す。新規ヒットがあれば true。
    /// </summary>
    private bool TrySweep(Vector3 origin, Vector3 dir, float distance, out float allowed, out Vector3 normal)
    {
        allowed = distance;
        normal = Vector3.zero;

        GetCapsuleWorldSegmentAt(origin, out Vector3 point0, out Vector3 point1, out float radius);

        int count = Physics.CapsuleCastNonAlloc(
            point0, point1, radius, dir, m_hits, distance + m_skinWidth, m_wallMask, QueryTriggerInteraction.Ignore);
        if (count == 0)
            return false;

        bool hitFound = false;
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            // 距離0付近 = 開始時から重なっている地面/壁。新規の貫通ではないので無視（ここで止めると停止する）。
            if (m_hits[i].distance <= m_skinWidth)
                continue;
            if (m_hits[i].distance < nearest)
            {
                nearest = m_hits[i].distance;
                normal = m_hits[i].normal;
                hitFound = true;
            }
        }

        if (!hitFound)
            return false;

        allowed = Mathf.Max(0f, nearest - m_skinWidth);
        return true;
    }

    /// <summary>凸コライダーへの残留めり込みを ComputePenetration で1回押し戻す（非凸 Mesh は false なので無視され、スイープ側で防ぐ）。</summary>
    private bool ResolvePenetration()
    {
        GetCapsuleWorldSegmentAt(transform.position, out Vector3 point0, out Vector3 point1, out float radius);

        int count = Physics.OverlapCapsuleNonAlloc(
            point0, point1, radius, m_overlaps, m_wallMask, QueryTriggerInteraction.Ignore);
        if (count == 0)
            return false;

        Vector3 totalPush = Vector3.zero;
        bool pushed = false;

        for (int i = 0; i < count; i++)
        {
            Collider wall = m_overlaps[i];
            if (wall == null) continue;

            if (Physics.ComputePenetration(
                    m_bodyCapsule, m_bodyCapsule.transform.position, m_bodyCapsule.transform.rotation,
                    wall, wall.transform.position, wall.transform.rotation,
                    out Vector3 direction, out float distance))
            {
                Vector3 push = direction * distance;
                if (m_horizontalOnly)
                    push.y = 0f;

                totalPush += push;
                pushed = true;
            }
        }

        if (pushed)
            transform.position += totalPush;

        return pushed;
    }

    /// <summary>指定したルート位置にカプセルがあると仮定した時の、世界座標の芯の両端点とスケール反映半径を求める。</summary>
    private void GetCapsuleWorldSegmentAt(Vector3 rootPosition, out Vector3 point0, out Vector3 point1, out float radius)
    {
        Transform t = m_bodyCapsule.transform;
        Vector3 scale = t.lossyScale;

        Vector3 axis;
        float heightScale;
        float radiusScale;
        switch (m_bodyCapsule.direction)
        {
            case 0:
                axis = t.right;
                heightScale = Mathf.Abs(scale.x);
                radiusScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                break;
            case 2:
                axis = t.forward;
                heightScale = Mathf.Abs(scale.z);
                radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                break;
            default:
                axis = t.up;
                heightScale = Mathf.Abs(scale.y);
                radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                break;
        }

        radius = m_bodyCapsule.radius * radiusScale;
        float scaledHeight = Mathf.Max(m_bodyCapsule.height * heightScale, radius * 2f);
        float halfSegment = Mathf.Max(0f, scaledHeight * 0.5f - radius);

        // 現在のカプセル中心の、ルートからの相対オフセットを使って任意ルート位置の中心を出す
        Vector3 capsuleCenterWorld = t.TransformPoint(m_bodyCapsule.center);
        Vector3 offset = capsuleCenterWorld - transform.position;
        Vector3 center = rootPosition + offset;

        point0 = center + axis * halfSegment;
        point1 = center - axis * halfSegment;
    }
}
