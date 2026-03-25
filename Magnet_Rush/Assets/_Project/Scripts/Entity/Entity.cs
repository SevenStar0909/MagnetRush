using UnityEngine;

/// <summary>
/// 全物理エンティティ（プレイヤー、敵）の基底クラス。
/// 速度（横移動・垂直・外部）、重力、移動を管理する。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public abstract class Entity : MonoBehaviour, IMagnetTarget
{
    protected CharacterController cc;
    [HideInInspector] public Health health;
    protected Transform cachedCameraTransform;

    public Vector3 lateralVelocity;
    public float verticalVelocity;
    public Vector3 externalVelocity;

    public bool IsGrounded { get; private set; }

    protected virtual void Awake()
    {
        cc = GetComponent<CharacterController>();
        health = GetComponent<Health>();

        var mainCam = Camera.main;
        if (mainCam != null)
            cachedCameraTransform = mainCam.transform;
    }

    /// <summary>
    /// 全速度成分をCharacterControllerの単一Moveコールで適用する。
    /// 適用後にexternalVelocityをリセットする。
    /// </summary>
    protected void ApplyMovement(float dt)
    {
        Vector3 total = lateralVelocity + Vector3.up * verticalVelocity + externalVelocity;
        cc.Move(total * dt);
        externalVelocity = Vector3.zero;
        IsGrounded = cc.isGrounded;
    }

    /// <summary>
    /// 重力を適用する。接地時は地面にスナップする。
    /// </summary>
    protected void ApplyGravity(float gravity, float snapForce, float dt)
    {
        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -snapForce;
        }
        else
        {
            verticalVelocity += gravity * dt;
        }
    }

    /// <summary>
    /// 指定方向への横移動速度を加速する。
    /// </summary>
    protected void Accelerate(Vector3 direction, float acceleration, float topSpeed, float dt)
    {
        if (direction.sqrMagnitude < 0.01f) return;

        direction.Normalize();
        float currentSpeed = Vector3.Dot(lateralVelocity, direction);
        float addSpeed = Mathf.Clamp(topSpeed - currentSpeed, 0f, acceleration * dt);
        lateralVelocity += direction * addSpeed;

        if (lateralVelocity.magnitude > topSpeed)
        {
            lateralVelocity = lateralVelocity.normalized * topSpeed;
        }
    }

    /// <summary>
    /// 横移動速度をゼロに向けて減速する。
    /// </summary>
    protected void Decelerate(float deceleration, float dt)
    {
        float speed = lateralVelocity.magnitude;
        if (speed < 0.01f)
        {
            lateralVelocity = Vector3.zero;
            return;
        }

        float drop = deceleration * dt;
        float newSpeed = Mathf.Max(speed - drop, 0f);
        lateralVelocity *= (newSpeed / speed);
    }

    /// <summary>
    /// Slerpを使用してエンティティを指定方向に回転させる。
    /// </summary>
    protected void FaceDirection(Vector3 direction, float rotationSpeed, float dt)
    {
        if (direction.sqrMagnitude < 0.01f) return;

        direction.y = 0f;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * dt
        );
    }

    /// <summary>
    /// IMagnetTarget実装。磁力システムから外部力を適用する。
    /// </summary>
    public void ApplyMagnetForce(Vector3 force)
    {
        externalVelocity += force;
    }

    /// <summary>
    /// 2D入力からカメラ相対の移動方向を取得する。
    /// </summary>
    protected Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (cachedCameraTransform == null) return Vector3.zero;

        Vector3 forward = cachedCameraTransform.forward;
        Vector3 right = cachedCameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return forward * input.y + right * input.x;
    }
}
