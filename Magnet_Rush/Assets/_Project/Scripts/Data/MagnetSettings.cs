using UnityEngine;

[CreateAssetMenu(fileName = "MagnetSettings", menuName = "MagnetRush/MagnetSettings")]
public class MagnetSettings : ScriptableObject
{
    [Header("Force")]
    public float magnetForce = 15f;
    public float magnetRange = 10f;

    [Header("Decay")]
    [Tooltip("1=linear, 2=inverse square")]
    public float forceDecayPower = 2f;
}
