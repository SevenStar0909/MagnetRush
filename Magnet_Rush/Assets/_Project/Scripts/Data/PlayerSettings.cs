using UnityEngine;

[CreateAssetMenu(fileName = "PlayerSettings", menuName = "MagnetRush/PlayerSettings")]
public class PlayerSettings : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float gravity = -20f;
    public float groundCheckRadius = 0.3f;

    [Header("Camera")]
    public float cameraSensitivityX = 200f;
    public float cameraSensitivityY = 200f;
}
