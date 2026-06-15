using UnityEngine;

/// <summary>
/// 磁力接触ダメージの設定。役割（箱／敵など）ごとに別アセットで管理。
/// ダメージは「磁化された位置から衝突地点まで飛んだ移動距離」をカーブに通して決める。
/// </summary>
[CreateAssetMenu(menuName = "MagnetRush/ContactDamageSettings")]
[ClassLabelSO("接触ダメージ設定")]
public class ContactDamageSettings : ScriptableObject
{
    [Header("[ダメージ]")]
    [Label("飛んできた距離ごとのダメージ")]
    [Tooltip("横軸=磁化された位置からぶつかるまでに飛んだ距離(m)、縦軸=ダメージ。近くから当たると小さく、遠くから飛んでくるほど大きくなるよう描く")]
    public AnimationCurve damageByDistance = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(8f, 5f));

    [Label("スタン値の蓄積率（％）")]
    [Tooltip("この物理オブジェクトをボス本体にぶつけたとき、ボスのスタンゲージが何％溜まるか。小=10, 大=30 が目安")]
    [Range(0, 100)]
    public int stunGaugePercent = 10;

    [Header("[判定]")]
    [Label("最低速度")]
    [Tooltip("この速さ未満の接触はダメージにしない")]
    public float minVelocity = 2f;

    [Header("[カメラシェイク]")]
    [Label("カメラシェイクを使う")]
    [Tooltip("敵に磁力接触ダメージを与えたとき、ダメージ量に応じてカメラを揺らす")]
    public bool enableCameraShake = true;

    [Label("ダメージごとの揺れの強さ")]
    [Tooltip("横軸=ダメージ量、縦軸=カメラの揺れ幅。0にすると揺れない")]
    public AnimationCurve cameraShakeAmplitudeByDamage = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(5f, 0.12f),
        new Keyframe(15f, 0.35f));

    [Label("ダメージごとの揺れ時間")]
    [Tooltip("横軸=ダメージ量、縦軸=揺れる時間(秒)。ダメージが大きいほど長く揺らしたいときに調整する")]
    public AnimationCurve cameraShakeDurationByDamage = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(5f, 0.16f),
        new Keyframe(15f, 0.28f));

    [Label("揺れの細かさ")]
    [Tooltip("数値が大きいほど細かく速く揺れる")]
    [Min(0f)]
    public float cameraShakeFrequency = 35f;

    [Header("[ヒットストップ]")]
    [Label("ヒットストップを使う")]
    [Tooltip("敵に磁力接触ダメージを与えたとき、ダメージ量に応じて一瞬だけ時間を止める")]
    public bool enableHitStop = true;

    [Label("ダメージごとの停止時間")]
    [Tooltip("横軸=ダメージ量、縦軸=停止時間(秒・実時間)。0にすると止まらない")]
    public AnimationCurve hitStopDurationByDamage = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(5f, 0.04f),
        new Keyframe(15f, 0.08f));

    [Label("停止中の時間倍率")]
    [Tooltip("0で完全停止、0.1ならかなり遅いスロー。強すぎる場合は少し上げる")]
    [Range(0f, 1f)]
    public float hitStopTimeScale = 0f;
}
