using UnityEngine;

/// <summary>
/// アニメーションでボス本体が壁にめり込むのを防ぐ。
/// ボスは EntityController により runtime で kinematic 化されるため、物理エンジンでは壁に押し返されない。
/// その代替として、ボス全体を覆うカプセル形状と Wall を ComputePenetration で判定し、
/// アニメ適用後の LateUpdate ではみ出した分だけボスを壁の外へ押し戻す。
/// 依存: 形状参照用の CapsuleCollider（判定専用なので disabled で良い）。
/// </summary>
[DisallowMultipleComponent]
public class BossWallBlocker : MonoBehaviour
{
    [SerializeField]
    [Tooltip("ボス全体を覆う形状参照用カプセル。判定にだけ使うので disabled（物理イベントは発火させない）")]
    private CapsuleCollider m_bodyCapsule;

    [SerializeField]
    [Tooltip("押し戻す相手のレイヤー。壁(Wall)のみ。プレイヤーや敵は入れない")]
    private LayerMask m_wallMask;

    [SerializeField]
    [Tooltip("1フレームで壁解消を繰り返す回数。角で複数の壁に同時に刺さった時の解消用")]
    private int m_maxIterations = 2;

    [SerializeField]
    [Tooltip("水平方向だけ押し戻す（壁に当たって上へ跳ね上がるのを防ぐ）")]
    private bool m_horizontalOnly = true;

    private readonly Collider[] m_overlaps = new Collider[8];

    private void Reset()
    {
        m_bodyCapsule = GetComponentInChildren<CapsuleCollider>();
        m_wallMask = 1 << LayerMask.NameToLayer("Wall");
    }

    // 移動・衝突解決は EnemyBossBase.Update() で走り、その後にアニメーションが本体を動かす。
    // よってめり込み補正はアニメ適用後・描画前の LateUpdate で行う必要がある。
    private void LateUpdate()
    {
        if (m_bodyCapsule == null) { ChannelLogger.LogGuardReturn("Enemy", "BossWallBlocker: bodyCapsule 未設定"); return; }
        if (m_wallMask == 0) { ChannelLogger.LogGuardReturn("Enemy", "BossWallBlocker: wallMask 未設定"); return; }

        for (int i = 0; i < m_maxIterations; i++)
        {
            if (!ResolvePenetration())
                break;
        }
    }

    /// <summary>壁とのめり込みを1回解消する。押し戻しが発生したら true を返す。</summary>
    private bool ResolvePenetration()
    {
        Vector3 point0;
        Vector3 point1;
        float radius;
        GetCapsuleWorldSegment(out point0, out point1, out radius);

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

            // disabled な m_bodyCapsule でも shape は参照されるので ComputePenetration は機能する。
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

    /// <summary>カプセルの世界座標での芯の両端点と、スケールを反映した半径を求める。</summary>
    private void GetCapsuleWorldSegment(out Vector3 point0, out Vector3 point1, out float radius)
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

        Vector3 center = t.TransformPoint(m_bodyCapsule.center);
        point0 = center + axis * halfSegment;
        point1 = center - axis * halfSegment;
    }
}
