using UnityEngine;

/// <summary>
/// 磁力場の共通インターフェース。Entity が参照する型。
/// Core アセンブリ内の Magnet/Interfaces/ に分離配置し、MagnetField 本体の実装詳細に依存せずに利用できる。
/// </summary>
public interface IMagnetField
{
    MagneticPole Pole { get; }
    Vector3 GetFieldDirection(Vector3 point);
    float GetStrengthAt(Vector3 point);
    int Priority { get; }
    Vector3 Center { get; }
    bool IsDestroyed { get; }
}