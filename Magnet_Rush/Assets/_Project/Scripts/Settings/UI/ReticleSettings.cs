using UnityEngine;

/// <summary>
/// レティクル演出のパラメータ。発射時のキック量・上限・リターン挙動を保持する。
/// </summary>
[CreateAssetMenu(fileName = "ReticleSettings", menuName = "MagnetRush/ReticleSettings")]
public class ReticleSettings : ScriptableObject
{
    [Header("[キック]")]
    [Label("1発のキック量（px）")]
    [Tooltip("1発で増えるオフセット (px)")]
    public float kickDistance = 12f;
    [Label("累積上限（px）")]
    [Tooltip("累積上限 (px)。連射してもこれ以上は外に飛ばない")]
    public float maxKickDistance = 48f;

    [Header("[リターン]")]
    [Label("リターン時間（秒）")]
    [Tooltip("ピーク→restの戻り時間 (秒)")]
    public float returnDuration = 0.18f;
    [Label("リターン補間カーブ")]
    [Tooltip("リターン補間カーブ。横軸=正規化時間, 縦軸=0(キック位置)→1(rest)")]
    public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}
