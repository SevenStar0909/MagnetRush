using UnityEngine;

/// <summary>
/// 磁力接触ダメージの設定。箱・武器部位ごとに別アセットで管理。
/// </summary>
[CreateAssetMenu(menuName = "MagnetRush/ContactDamageSettings")]
[ClassLabelSO("接触ダメージ設定")]
public class ContactDamageSettings : ScriptableObject
{
    [Header("[ダメージ]")]
    [Label("ダメージ")]
    public int damage = 1;

    [Label("スタン値の蓄積率（％）")]
    [Tooltip("この物理オブジェクトをボス本体にぶつけたとき、ボスのスタンゲージが何％溜まるか。小=10, 大=30 が目安")]
    [Range(0, 100)]
    public int stunGaugePercent = 10;

    [Header("[判定]")]
    [Label("最低速度")]
    [Tooltip("磁力衝突とみなす最低速度")]
    public float minVelocity = 2f;

    [Label("Overlap判定半径")]
    [Tooltip("Overlap判定の半径")]
    public float overlapRadius = 0.5f;
}
