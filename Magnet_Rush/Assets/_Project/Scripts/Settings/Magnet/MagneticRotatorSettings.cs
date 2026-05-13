using UnityEngine;

[CreateAssetMenu(fileName = "MagneticRotatorSettings", menuName = "MagnetRush/MagneticRotatorSettings")]
[ClassLabelSO("磁力回転体設定")]
public class MagneticRotatorSettings : ScriptableObject
{
    [Header("[回転]")]
    [Label("回転軸")]
    [Tooltip("磁力による回転軸")]
    public Vector3 rotationAxis = Vector3.up;
    [Label("最大角速度（度/秒）")]
    [Tooltip("最大角速度（度/秒）")]
    public float maxAngularSpeed = 90f;

    [Header("[角度制限]")]
    [Label("最小角度（度）")]
    [Tooltip("回転の最小角度（度）")]
    public float minAngle = -180f;
    [Label("最大角度（度）")]
    [Tooltip("回転の最大角度（度）")]
    public float maxAngle = 180f;
}
