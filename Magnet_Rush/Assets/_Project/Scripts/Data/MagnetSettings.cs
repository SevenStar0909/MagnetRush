using UnityEngine;

[CreateAssetMenu(fileName = "MagnetSettings", menuName = "MagnetRush/MagnetSettings")]
public class MagnetSettings : ScriptableObject
{
    [Header("Force")]
    public float magnetForce = 15f;
    public float magnetRange = 10f;

    [Header("減衰")]
    [Tooltip("1=線形, 2=逆二乗")]
    public float forceDecayPower = 2f;

    [Header("制限")]
    [Tooltip("1オブジェクトが受ける合力の上限。0=無制限")]
    public float maxForcePerObject = 50f;
}
