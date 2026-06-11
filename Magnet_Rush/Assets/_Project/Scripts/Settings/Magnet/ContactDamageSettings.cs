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
}
