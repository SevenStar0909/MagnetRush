using UnityEngine;

/// <summary>
/// 磁力を受けるオブジェクトのインターフェース（OCP準拠）。
/// 新しい移動タイプを追加する場合はこのインターフェースを実装する。
/// </summary>
public interface IMagnetTarget
{
    void ApplyMagnetForce(Vector3 force);
}
