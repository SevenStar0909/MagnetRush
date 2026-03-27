using UnityEngine;

[CreateAssetMenu(fileName = "MagneticDeflectorSettings", menuName = "MagnetRush/MagneticDeflectorSettings")]
public class MagneticDeflectorSettings : ScriptableObject
{
    [Header("偏向")]
    [Range(0f, 1f)]
    [Tooltip("偏向強度 (0=影響なし, 1=力方向に完全追従)")]
    public float deflectionStrength = 0.5f;
    [Tooltip("1フレームあたりの最大偏向角度")]
    public float maxDeflectionAngle = 45f;
}
