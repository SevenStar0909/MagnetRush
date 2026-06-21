using UnityEngine;

/// <summary>
/// スタブ着弾VFXに付け、自分配下の全レンダラを専用レンダリングレイヤー(StabInkEffect.RenderingLayerBit)に乗せる。
/// これにより StabInkRenderFeature がこのVFXだけを黒く塗れる（マスク対象になる）。
/// 白黒化の ON/OFF・強さは Timeline の StabInkTrack が StabInkEffect.Strength で制御するので、ここでは触らない。
/// 依存: StabInkEffect（Common）。VFX 生成側 StabAbility が AddComponent して使う。
/// </summary>
public class StabInkScreen : MonoBehaviour
{
    void OnEnable()
    {
        // VFX の全レンダラに専用ビットを足す（既存ビットは保持＝ライティングはそのまま）
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.renderingLayerMask |= StabInkEffect.RenderingLayerBit;
    }
}
