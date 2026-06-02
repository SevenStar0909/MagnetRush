using UnityEngine;

[CreateAssetMenu(fileName = "MagneticMoverSettings", menuName = "MagnetRush/MagneticMoverSettings")]
[ClassLabelSO("磁力移動設定")]
public class MagneticMoverSettings : ScriptableObject
{
    [Header("[移動]")]
    [Label("磁力倍率")]
    [Tooltip("磁力の倍率。Playerとは独立してエネミーの磁力感度を調整する")]
    public float forceMultiplier = 3f;
    [Label("最大速度（m/s）")]
    [Tooltip("磁力移動時の最大速度（m/s）")]
    public float maxSpeed = 15f;
    [Label("NavMesh復帰待ち時間（秒）")]
    [Tooltip("磁力停止後の NavMesh 復帰待ち時間（秒）")]
    public float recoveryDelay = 1f;
    [Header("[復帰]")]
    [Label("NavMesh復帰最大リトライ回数")]
    [Tooltip("NavMesh 復帰の最大リトライ回数")]
    public int maxRecoveryAttempts = 10;
}
