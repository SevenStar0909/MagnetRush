using UnityEngine;

[CreateAssetMenu(fileName = "MagnetSettings", menuName = "MagnetRush/MagnetSettings")]
public class MagnetSettings : ScriptableObject
{
    [Header("Force")]
    public float magnetForce = 15f;
    public float magnetRange = 10f;

    [Header("減衰")]
    [Tooltip("deprecated: forceCurve 未設定時のフォールバック。1=線形, 2=逆二乗")]
    public float forceDecayPower = 2f;
    [Tooltip("X=正規化距離(0-1), Y=力の倍率(0-1)。設定するとforceDecayPowerより優先")]
    public AnimationCurve forceCurve;

    [Header("制限")]
    [Tooltip("1オブジェクトが受ける合力の上限。0=無制限")]
    public float maxForcePerObject = 50f;

    [Header("接触")]
    [Tooltip("磁力スナップが発生する距離")]
    public float snapDistance = 1.5f;

    [Header("スナップ")]
    [Tooltip("スプリング接近の smoothTime")]
    public float snapSmoothTime = 0.15f;
    [Tooltip("FixedJoint 生成距離")]
    public float snapContactThreshold = 0.1f;
    [Tooltip("FixedJoint の破壊力（同極反発で分離）")]
    public float snapBreakForce = 100f;

    [Header("アウトライン")]
    [Tooltip("S極アウトラインマテリアル（青）")]
    public Material outlineMatS;
    [Tooltip("N極アウトラインマテリアル（赤）")]
    public Material outlineMatN;

    [Header("移動変調")]
    [Tooltip("磁力場内での最高速度低下率(0=影響なし, 1=完全停止)")]
    [Range(0f, 1f)]
    public float magnetSpeedDamping = 0.3f;
}
