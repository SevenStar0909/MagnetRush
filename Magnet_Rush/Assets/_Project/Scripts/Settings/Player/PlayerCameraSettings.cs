using UnityEngine;

[CreateAssetMenu(fileName = "PlayerCameraSettings", menuName = "MagnetRush/PlayerCameraSettings")]
[ClassLabelSO("プレイヤーカメラ設定")]
public class PlayerCameraSettings : ScriptableObject
{
    [Header("[反発ジャンプ演出]")]
    [Label("引く距離（m）")]
    [Tooltip("反発で飛んでいる間、カメラを後ろへ引く距離。0以下で演出オフ")]
    public float repulsePullDistance = 0.8f;

    [Label("引き時間（秒）")]
    [Tooltip("カメラを引くのにかける時間。短いほどスッと引く")]
    public float repulsePullInDuration = 0.15f;

    [Label("戻し時間（秒）")]
    [Tooltip("元の距離に戻すのにかける時間。長いほどゆっくり戻る")]
    public float repulsePullOutDuration = 0.6f;

    [Label("発動に必要な反発の強さ")]
    [Tooltip("この強さ以上の反発力を受けた時だけカメラを引く。弱い反発では発動しない")]
    public float repulseForceThreshold = 5f;
}
