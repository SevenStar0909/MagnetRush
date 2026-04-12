using UnityEngine;

/// <summary>
/// 磁力接触ダメージの設定。箱・武器部位ごとに別アセットで管理。
/// </summary>
[CreateAssetMenu(menuName = "MagnetRush/ContactDamageSettings")]
public class ContactDamageSettings : ScriptableObject
{
    [Header("ダメージ")]
    public int damage = 1;

    [Header("判定")]
    [Tooltip("磁力衝突とみなす最低速度")]
    public float minVelocity = 2f;

    [Tooltip("Overlap判定の半径")]
    public float overlapRadius = 0.5f;
}
