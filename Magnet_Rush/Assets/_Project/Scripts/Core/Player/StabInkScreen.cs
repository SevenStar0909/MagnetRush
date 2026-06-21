using UnityEngine;

/// <summary>
/// スタブ着弾VFXに付け、配下のレンダラを専用レンダリングレイヤー(StabInkEffect.RenderingLayerBit)に乗せる。
/// これにより StabInkRenderFeature がこのVFXだけをインク化できる（マスク対象になる）。
/// ただし m_excludeNameContains に名前が一致するレンダラ（既定＝煙パフ ExplosionPS）はインクから除外する。
/// 白黒化の ON/OFF・強さは Timeline の StabInkTrack が StabInkEffect.Strength で制御するので、ここでは触らない。
/// 依存: StabInkEffect（Common）。VFX 生成側 StabAbility が AddComponent して使う。
/// </summary>
public class StabInkScreen : MonoBehaviour
{
    [Tooltip("名前にこの文字列を含むレンダラはインク（白黒）に含めない。ExplosionPS=丸い煙パフ、Drop_PS=メッシュ破片を除外（雷だけ残す）")]
    [SerializeField] private string[] m_excludeNameContains = { "ExplosionPS", "Drop_PS" };

    [Tooltip("インク対象（雷）のパーティクルのサイズをこの倍率で大きくする。画面拡大ではなく最初から大きく描画するのでギザギザにならない。1で等倍。煙・破片は対象外")]
    [SerializeField] private float m_lightningSizeMultiplier = 2f;

    void OnEnable()
    {
        // 対象レンダラに専用ビットを足す（既存ビットは保持＝ライティングはそのまま）。煙は除外する。
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (IsExcluded(r.gameObject.name)) continue;
            r.renderingLayerMask |= StabInkEffect.RenderingLayerBit;
        }

        // インク対象（雷）のパーティクルを最初から大きく描画する（画面拡大ではないのでギザらない）。煙・破片は除外名なので対象外。
        if (!Mathf.Approximately(m_lightningSizeMultiplier, 1f))
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (IsExcluded(ps.gameObject.name)) continue;
                var main = ps.main;
                main.startSizeMultiplier *= m_lightningSizeMultiplier;
            }
        }
    }

    private bool IsExcluded(string objectName)
    {
        if (m_excludeNameContains == null) return false;
        foreach (var s in m_excludeNameContains)
            if (!string.IsNullOrEmpty(s) && objectName.Contains(s)) return true;
        return false;
    }
}
