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

    [Header("[判定]")]
    [Label("最低速度")]
    [Tooltip("磁力衝突とみなす最低速度")]
    public float minVelocity = 2f;

    [Label("Overlap判定半径")]
    [Tooltip("Overlap判定の半径")]
    public float overlapRadius = 0.5f;
}
