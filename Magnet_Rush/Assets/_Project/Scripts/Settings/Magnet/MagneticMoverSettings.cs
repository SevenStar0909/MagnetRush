using UnityEngine;

[CreateAssetMenu(fileName = "MagneticMoverSettings", menuName = "MagnetRush/MagneticMoverSettings")]
public class MagneticMoverSettings : ScriptableObject
{
    [Header("移動")]
    [Tooltip("磁力の倍率。Playerとは独立してエネミーの磁力感度を調整する")]
    public float forceMultiplier = 3f;
    [Tooltip("磁力移動時の最大速度（m/s）")]
    public float maxSpeed = 15f;
    [Tooltip("磁力停止後の NavMesh 復帰待ち時間（秒）")]
    public float recoveryDelay = 1f;
    [Header("復帰")]
    [Tooltip("NavMesh 復帰の最大リトライ回数")]
    public int maxRecoveryAttempts = 10;
}
