using UnityEngine;

/// <summary>
/// 幾何計算ユーティリティ。MagnetFieldの形状別方向計算で使用する。
/// Platformer Projectから移植。
/// </summary>
public static class BoundsHelper
{
    /// <summary>
    /// 有限線分上の最近接点を返す。Cylinder方向計算用。
    /// </summary>
    public static Vector3 NearestPointOnFiniteLine(Vector3 start, Vector3 end, Vector3 point)
    {
        var line = end - start;
        var len = line.magnitude;
        line /= len;

        var v = point - start;
        var d = Vector3.Dot(v, line);
        d = Mathf.Clamp(d, 0f, len);
        return start + line * d;
    }

    /// <summary>
    /// Box表面上の最近接点を返す。Box方向計算用。
    /// </summary>
    public static Vector3 NearestPointOnBox(Vector3 center, Vector3 size, Quaternion rotation, Vector3 point)
    {
        var extents = size * 0.5f;
        var direction = point - center;

        direction = Quaternion.Inverse(rotation) * direction;

        var clampedDirection = new Vector3(
            Mathf.Clamp(direction.x, -extents.x, extents.x),
            Mathf.Clamp(direction.y, -extents.y, extents.y),
            Mathf.Clamp(direction.z, -extents.z, extents.z)
        );

        return center + rotation * clampedDirection;
    }
}
