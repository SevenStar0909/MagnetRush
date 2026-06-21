/// <summary>
/// スタブ着弾の「エフェクトだけ黒・他は全部白」演出の状態を持つ静的な受け渡し口。
/// 描画側(Assembly-CSharp の StabInkRenderFeature)と演出側(MagnetRush.Player の StabInkScreen)は
/// asmdef が異なり直接参照できないため、双方から参照できる中立な Common に置く。
/// Global Volume は使わない。スタブVFXを専用レンダリングレイヤーで分離して描画側でマスクする。
/// </summary>
public static class StabInkEffect
{
    /// <summary>白黒インク演出の強さ。0=通常、1=完全に「効果黒・他白」。Timeline の StabInkTrack が駆動する。平常時は必ず 0。</summary>
    public static float Strength;

    /// <summary>
    /// スタブ効果を他から分離するレンダリングレイヤーのビット（bit9=512）。
    /// bit0-8 は既存（bit8=Magnetized）で埋まっているため空いている bit9 を使う。描画側とVFX側で共有する。
    /// </summary>
    public const uint RenderingLayerBit = 1u << 9;
}
