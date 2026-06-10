using UnityEngine;

/// <summary>
/// 磁力接触ダメージの設定。役割（箱／敵など）ごとに別アセットで管理。
/// 磁化した物がぶつかった時に固定ダメージを与える。
/// </summary>
[CreateAssetMenu(menuName = "MagnetRush/ContactDamageSettings")]
[ClassLabelSO("接触ダメージ設定")]
public class ContactDamageSettings : ScriptableObject
{
    [Header("[ダメージ]")]
    [Label("ダメージ")]
    [Tooltip("ぶつかった時に与える固定ダメージ")]
    public int damage = 1;

    [Label("スタン値の蓄積率（％）")]
    [Tooltip("この物理オブジェクトをボス本体にぶつけたとき、ボスのスタンゲージが何％溜まるか。小=10, 大=30 が目安")]
    [Range(0, 100)]
    public int stunGaugePercent = 10;

    [Header("[判定]")]
    [Label("最低速度")]
    [Tooltip("この速さ未満の接触はダメージにしない")]
    public float minVelocity = 2f;
}
