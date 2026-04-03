using UnityEngine;

[CreateAssetMenu(fileName = "MagnetSettings", menuName = "MagnetRush/MagnetSettings")]
public class MagnetSettings : ScriptableObject
{
    [Header("磁力")]
    [Tooltip("磁力の基本の強さ。inner範囲内でこの値がフルで適用される")]
    public float magnetForce = 15f;
    [Tooltip("ペア検索のハードカットオフ距離（m）。この距離以上のペアは力を計算しない（パフォーマンス用）")]
    public float magnetRange = 10f;

    [Header("制限")]
    [Tooltip("1オブジェクトが受ける合力の上限。0=無制限")]
    public float maxForcePerObject = 50f;

    [Header("接触")]
    [Tooltip("磁力スナップが発生する距離")]
    public float snapDistance = 1.5f;

    [Header("スナップ")]
    [Tooltip("FixedJoint の破壊力（同極反発で分離）")]
    public float snapBreakForce = 100f;

    [Header("移動変調")]
    [Tooltip("磁力場内での最高速度低下率(0=影響なし, 1=完全停止)")]
    [Range(0f, 1f)]
    public float magnetSpeedDamping = 0.3f;
}
