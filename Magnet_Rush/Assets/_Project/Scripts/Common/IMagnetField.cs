using UnityEngine;

/// <summary>
/// 磁力場の共通インターフェース。Entity が参照する型。
/// Common モジュールに配置し、循環依存を回避する。
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