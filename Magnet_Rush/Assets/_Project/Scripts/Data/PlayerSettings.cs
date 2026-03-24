using UnityEngine;

[CreateAssetMenu(fileName = "PlayerSettings", menuName = "MagnetRush/PlayerSettings")]
public class PlayerSettings : ScriptableObject
{
    [Header("Movement")]
    public float topSpeed = 6f;
    public float acceleration = 30f;
    public float deceleration = 25f;
    public float turningDrag = 20f;
    public float rotationSpeed = 15f;

    [Header("Gravity")]
    public float gravity = -20f;
    public float snapForce = 2f;
    public float groundCheckDistance = 0.3f;
    public LayerMask groundLayer;

    [Header("Camera")]
    public float cameraSensitivityX = 200f;
    public float cameraSensitivityY = 200f;
    public float cameraDistance = 5f;
    public Vector3 shoulderOffset;

    [Header("Aim")]
    public float aimTimeScale = 0.3f;
    public float aimFOV = 40f;
    public float aimCameraDistance = 3f;
    public float aimMoveSpeedMultiplier = 0.5f;

    [Header("死亡・リスポーン")]
    public float respawnDelay = 3f;

    [Header("Magnet")]
    public float magnetResistance = 0.5f;
}
