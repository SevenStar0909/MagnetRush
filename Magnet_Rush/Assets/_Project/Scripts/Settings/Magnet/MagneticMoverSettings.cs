using UnityEngine;

[CreateAssetMenu(fileName = "MagneticMoverSettings", menuName = "MagnetRush/MagneticMoverSettings")]
[ClassLabelSO("磁力移動設定")]
public class MagneticMoverSettings : ScriptableObject
{
    [Header("[移動]")]
    [Label("敵の引き寄せられやすさ倍率")]
    [Tooltip("敵が磁力でどれだけ動きやすいかの倍率。プレイヤーとは別に調整できる")]
    public float forceMultiplier = 3f;
    [Label("敵が磁力で動ける最大速度（m/s）")]
    [Tooltip("敵が磁力で引っ張られる時の最高速度（m/s）。ここで頭打ちになる")]
    public float maxSpeed = 15f;
    [Label("動きが止まってから歩行に戻るまでの時間（秒）")]
    [Tooltip("磁力で動かされた後、通常の歩行に戻るまでの待ち時間（秒）")]
    public float recoveryDelay = 1f;
    [Header("[復帰]")]
    [Label("歩行に戻すのを試す最大回数")]
    [Tooltip("通常の歩行に戻すのを何回まで試すか")]
    public int maxRecoveryAttempts = 10;
}
