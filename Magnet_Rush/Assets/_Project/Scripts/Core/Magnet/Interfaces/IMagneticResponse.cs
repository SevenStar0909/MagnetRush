using UnityEngine;

/// <summary>
/// 磁力への応答方法を抽象化するインターフェース。
/// Magnetizable が力の適用先を判別する際に使用する。
/// オブジェクトごとに異なる応答（移動/回転/軌道変化）を実装可能にする。
/// </summary>
public interface IMagneticResponse
{
    /// <summary>磁力を受けたときの応答。MagnetManager の力計算後に毎フレーム呼ばれる。</summary>
    void OnMagnetForce(Vector3 force, Vector3 sourcePosition);

    /// <summary>異極オブジェクトが snapDistance 以内に入ったときの接触処理。</summary>
    void OnMagnetContact(Magnetizable self, Magnetizable other);

    /// <summary>レスポンスが現在アクティブか（磁化解除で false にできる）。</summary>
    bool IsResponseActive { get; }
}
