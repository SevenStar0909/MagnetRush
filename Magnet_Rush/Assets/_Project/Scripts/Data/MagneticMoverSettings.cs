using UnityEngine;

[CreateAssetMenu(fileName = "MagneticMoverSettings", menuName = "MagnetRush/MagneticMoverSettings")]
public class MagneticMoverSettings : ScriptableObject
{
    [Header("移動")]
    public float maxSpeed = 15f;
    [Tooltip("磁力停止後の NavMesh 復帰待ち時間")]
    public float recoveryDelay = 1f;
    public ForceMode forceMode = ForceMode.Force;

    [Header("復帰")]
    [Tooltip("NavMesh 復帰の最大リトライ回数")]
    public int maxRecoveryAttempts = 10;
}
