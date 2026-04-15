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
    [Tooltip("Object×Object の FixedJoint 発動距離（Entity絡みは holdEngageDistance を使用）")]
    public float snapDistance = 1.5f;

    [Header("スナップ")]
    [Tooltip("FixedJoint の破壊力（同極反発で分離）")]
    public float snapBreakForce = 100f;

    [Header("PDホールド")]
    [Tooltip("この距離内に入ると PD 保持に切り替わる (Entity絡みペア専用)")]
    public float holdEngageDistance = 1.5f;

    [Tooltip("位置エラーに対するバネ定数。大きいほどガッチリ追従するが振動しやすい")]
    public float holdStiffness = 80f;

    [Tooltip("速度に対するダンパ係数。大きいほど振動が収まるが追従が重くなる")]
    public float holdDamping = 15f;

    [Tooltip("吸着中の最大許容距離。超過で吸着解除")]
    public float holdMaxDistance = 3f;

    [Header("弾同士")]
    [Tooltip("異極の弾が近接した時にダメージ蓄積が発生する距離")]
    public float bulletProximityRange = 1f;

    [Header("移動変調")]
    [Tooltip("磁力場内での最高速度低下率(0=影響なし, 1=完全停止)")]
    [Range(0f, 1f)]
    public float magnetSpeedDamping = 0.3f;
}
