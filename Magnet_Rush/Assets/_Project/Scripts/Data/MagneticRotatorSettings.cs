using UnityEngine;

[CreateAssetMenu(fileName = "MagneticRotatorSettings", menuName = "MagnetRush/MagneticRotatorSettings")]
public class MagneticRotatorSettings : ScriptableObject
{
    [Header("回転")]
    public Vector3 rotationAxis = Vector3.up;
    [Tooltip("最大角速度 (degrees/sec)")]
    public float maxAngularSpeed = 90f;

    [Header("角度制限")]
    public float minAngle = -180f;
    public float maxAngle = 180f;
}
