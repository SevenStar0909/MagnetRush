using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerSettings", menuName = "MagnetRush/PlayerSettings")]
public class PlayerSettings : ScriptableObject
{
    [Header("Movement")]
    [FormerlySerializedAs("moveSpeed")]
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

    [Header("射撃")]
    public float firePointHeight = 1.2f;

    [Header("Aim")]
    public float aimReleaseGraceTime = 0.15f;
    public float aimTimeScale = 0.3f;
    public float aimFOV = 40f;
    public float aimCameraDistance = 3f;
    public float aimMoveSpeedMultiplier = 0.5f;

    [Header("死亡・リスポーン")]
    public float respawnDelay = 3f;

    [Header("Slope")]
    public float slopeUpwardForce = 15f;
    public float slopeDownwardForce = 25f;

    [Header("Magnet")]
    public float magnetResistance = 0.5f;

    [Header("磁力回転")]
    [Tooltip("この外部力（magnitude）以上で空中回転開始")]
    public float pullOrientationThreshold = 5f;
    [Tooltip("磁力方向への回転速度")]
    public float pullOrientationSpeed = 8f;
}
