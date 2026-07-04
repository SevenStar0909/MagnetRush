using UnityEngine;

/// <summary>
/// プレイヤーが追跡範囲外のときの徘徊パラメータ。EnemySettings / EnemyAirSettings に埋め込んで使う。
/// 徘徊は「出現位置を中心にランダムな地点へ移動しては休む」の繰り返し。
/// </summary>
[System.Serializable]
public class EnemyWanderSettings
{
    [Label("徘徊を有効にする")]
    [Tooltip("プレイヤーが追跡範囲の外にいるとき、その場に立ち止まらずうろうろ動き回る")]
    public bool enabled = true;

    [LabelMin("徘徊半径（m）", 0f)]
    [Tooltip("出現位置を中心に、この半径内のランダムな地点を行き先に選ぶ")]
    public float radius = 6f;

    [LabelRange("徘徊時の速度倍率", 0.1f, 1f)]
    [Tooltip("徘徊中の移動の速さ。1で通常の移動速度、0.5で半分のゆっくり移動")]
    public float speedMultiplier = 0.5f;

    [LabelMin("1回の移動の上限時間（秒）", 1f)]
    [Tooltip("行き先に着けないときでも、この時間で移動を打ち切って休止に入る")]
    public float moveTimeout = 6f;

    [LabelMin("休止時間の最小（秒）", 0f)]
    [Tooltip("行き先に着いたあと、次に動き出すまでの待ち時間の下限")]
    public float pauseDurationMin = 1f;

    [LabelMin("休止時間の最大（秒）", 0f)]
    [Tooltip("行き先に着いたあと、次に動き出すまでの待ち時間の上限")]
    public float pauseDurationMax = 3f;
}
