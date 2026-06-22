/// <summary>
/// Timeline から描画側へ渡す全画面白黒フリッカーの状態。
/// Strength は 0=通常、1=完全適用。演出外では必ず 0 に戻す。
/// </summary>
public static class ScreenInvertEffect
{
    public static float Strength;
    public static float Contrast = 12f;
    public static float Threshold = 0.5f;
    public static float InvertPolarity;
}
