using UnityEngine;

/// <summary>
/// 全物理エンティティ（プレイヤー、敵）の基底クラス。
/// 速度（横移動・垂直・外部）、重力、移動を管理する。
/// </summary>
public abstract class Entity : MonoBehaviour, IMagnetTarget
{
    protected Rigidbody rb;
    protected CapsuleCollider capsuleCollider;
    [HideInInspector] public Health health;
    protected Transform cachedCameraTransform;

    /// <summary>ワールド空間の速度ベクトル。</summary>
    public Vector3 velocity;

    /// <summary>外部からの一時的な力（磁力等）。毎フレームリセット。</summary>
    public Vector3 externalVelocity;

    /// <summary>
    /// ローカル空間の速度（transform.upとVector3.upの間で自動変換）。
    /// 斜面上でも正しく動作する。
    /// </summary>
    public Vector3 localVelocity
    {
        get => Quaternion.FromToRotation(transform.up, Vector3.up) * velocity;
        set => velocity = Quaternion.FromToRotation(Vector3.up, transform.up) * value;
    }

    /// <summary>ローカル空間のXZ速度（横移動成分）。</summary>
    public Vector3 lateralVelocity
    {
        get
        {
            var local = localVelocity;
            var value = new Vector3(local.x, 0f, local.z);
            return value.sqrMagnitude < 0.0001f ? Vector3.zero : value;
        }
        set
        {
            var local = localVelocity;
            localVelocity = new Vector3(value.x, local.y, value.z);
        }
    }

    /// <summary>ローカル空間のY速度（垂直成分）。</summary>
    public float verticalVelocity
    {
        get => localVelocity.y;
        set
        {
            var local = localVelocity;
            local.y = value;
            localVelocity = local;
        }
    }

    public bool IsGrounded { get; protected set; }

    // --- 地面情報（斜面対応） ---
    public RaycastHit groundHit { get; protected set; }
    public float groundAngle { get; protected set; }
    public Vector3 groundNormal { get; protected set; } = Vector3.up;
    public Vector3 localSlopeDirection { get; protected set; }

    protected readonly float slopingGroundAngle = 20f;

    // --- 磁力場 ---
    /// <summary>現在支配的な磁力場。MagnetManagerが毎フレーム設定する。</summary>
    public IMagnetField magnetField { get; set; }

    // --- 外部変調用マルチプライヤー（磁力場・エリア効果等） ---
    public float topSpeedMultiplier { get; set; } = 1f;
    public float turningDragMultiplier { get; set; } = 1f;
    public float decelerationMultiplier { get; set; } = 1f;

    // --- 磁力回転設定（サブクラスからSO値で上書き） ---
    protected float m_pullOrientationThreshold = 5f;
    protected float m_pullOrientationSpeed = 8f;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        health = GetComponent<Health>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        var mainCam = Camera.main;
        if (mainCam != null)
            cachedCameraTransform = mainCam.transform;
    }

    /// <summary>
    /// 全速度成分をRigidbody.MovePositionで適用する。
    /// 適用後にexternalVelocityをリセットする。
    /// </summary>
    protected virtual void ApplyMovement(float dt)
    {
        Vector3 total = velocity + externalVelocity;
        if (rb != null)
        {
            rb.MovePosition(rb.position + total * dt);
        }
        externalVelocity = Vector3.zero;
        // IsGrounded は UpdateGround() のレイキャストで更新
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
    /// 指定方向への横移動速度を加速する（turningDragで滑らかな方向転換）。
    /// Platformer ProjectのAccelerateパターンを採用。
    /// </summary>
    protected void Accelerate(Vector3 direction, float turningDrag, float acceleration, float topSpeed, float dt)
    {
        if (direction.sqrMagnitude < 0.01f) return;

        direction.Normalize();

        var effectiveTopSpeed = topSpeed * topSpeedMultiplier;
        var effectiveTurningDrag = turningDrag * turningDragMultiplier;

        // 速度を「進行方向成分」と「横方向成分」に分解
        float speed = Vector3.Dot(direction, lateralVelocity);
        Vector3 turningVelocity = lateralVelocity - direction * speed;

        // 横方向成分を減衰（滑らかな方向転換）
        float turningDelta = effectiveTurningDrag * dt;
        turningVelocity = Vector3.MoveTowards(turningVelocity, Vector3.zero, turningDelta);

        // 進行方向に加速
        if (lateralVelocity.magnitude < effectiveTopSpeed || speed < 0f)
        {
            speed += acceleration * dt;
            speed = Mathf.Clamp(speed, -effectiveTopSpeed, effectiveTopSpeed);
        }

        lateralVelocity = direction * speed + turningVelocity;
    }

    /// <summary>
    /// 横移動速度をゼロに向けて減速する。
    /// </summary>
    protected void Decelerate(float deceleration, float dt)
    {
        float delta = deceleration * decelerationMultiplier * dt;
        lateralVelocity = Vector3.MoveTowards(lateralVelocity, Vector3.zero, delta);
    }

    /// <summary>
    /// 地面情報（法線、角度、斜面方向）を更新する。
    /// </summary>
    protected void UpdateGround()
    {
        float height = capsuleCollider != null ? capsuleCollider.height : 2f;
        float groundCheckDist = height * 0.5f + 0.3f;

        // レイキャストで接地判定（cc.isGroundedの代替）
        if (Physics.Raycast(transform.position, -transform.up, out var hit, groundCheckDist,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // 足元からの距離が十分近ければ接地
            float footDist = hit.distance - height * 0.5f;
            IsGrounded = footDist < 0.1f;

            if (IsGrounded)
            {
                groundHit = hit;
                groundNormal = hit.normal;
                groundAngle = Vector3.Angle(Vector3.up, hit.normal);
                localSlopeDirection = new Vector3(groundNormal.x, 0f, groundNormal.z).normalized;
                return;
            }
        }
        else
        {
            IsGrounded = false;
        }

        groundAngle = 0f;
        groundNormal = Vector3.up;
        localSlopeDirection = Vector3.zero;
    }

    /// <summary>
    /// 現在の地面が斜面かどうかを返す。
    /// </summary>
    public virtual bool OnSlopingGround()
    {
        return IsGrounded && groundAngle > slopingGroundAngle;
    }

    /// <summary>
    /// 斜面での加減速を適用する。上り坂は減速、下り坂は加速。
    /// </summary>
    protected void SlopeFactor(float upwardForce, float downwardForce, float dt)
    {
        if (!IsGrounded || !OnSlopingGround()) return;

        var factor = Vector3.Dot(Vector3.up, groundNormal);
        var downwards = Vector3.Dot(localSlopeDirection, lateralVelocity) > 0;
        var multiplier = downwards ? downwardForce : upwardForce;
        var delta = factor * multiplier * dt;
        lateralVelocity += localSlopeDirection * delta;
    }

    /// <summary>
    /// Slerpを使用してエンティティを指定方向に回転させる。
    /// adjustUp=trueの場合、ローカル空間の方向をtransform.upに合わせて変換する。
    /// </summary>
    protected void FaceDirection(Vector3 direction, float rotationSpeed, float dt, bool adjustUp = true)
    {
        if (direction.sqrMagnitude < 0.01f) return;

        if (adjustUp)
            direction = Quaternion.FromToRotation(Vector3.up, transform.up) * direction;

        Quaternion targetRotation = Quaternion.LookRotation(direction, transform.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * dt
        );
    }

    /// <summary>
    /// 強い磁力を受けて空中にいるとき、引力/斥力方向にプレイヤーの正面を向ける。
    /// 接地中は通常の移動方向回転を維持する。
    /// </summary>
    protected virtual void UpdateMagneticOrientation(float dt)
    {
        if (IsGrounded) return;
        if (externalVelocity.sqrMagnitude < m_pullOrientationThreshold * m_pullOrientationThreshold) return;

        FaceDirection(externalVelocity.normalized, m_pullOrientationSpeed, dt);
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
        return GetCameraRelativeDirection(input, out _);
    }

    /// <summary>
    /// 2D入力からカメラ相対の移動方向を取得する（ProjectOnPlane + localSpace変換）。
    /// magnitudeには正規化前の大きさが出力される（アナログ入力の強度）。
    /// Platformer ProjectのGetMovementCameraDirectionパターンを採用。
    /// </summary>
    protected Vector3 GetCameraRelativeDirection(Vector2 input, out float magnitude, bool localSpace = true)
    {
        magnitude = 0f;
        if (cachedCameraTransform == null || input.sqrMagnitude < 0.01f)
            return Vector3.zero;

        // 2D入力を3D方向に変換
        Vector3 direction = new Vector3(input.x, 0f, input.y);

        // カメラのupをエンティティのupに合わせてから回転適用
        var rotation = Quaternion.FromToRotation(cachedCameraTransform.up, transform.up);
        direction = rotation * cachedCameraTransform.rotation * direction;

        if (localSpace)
        {
            // エンティティの接地面に投影し、ローカル空間に変換
            direction = Vector3.ProjectOnPlane(direction, transform.up);
            direction = Quaternion.FromToRotation(transform.up, Vector3.up) * direction;
        }

        magnitude = direction.magnitude;

        if (magnitude > 0.001f)
            direction /= magnitude;
        else
            return Vector3.zero;

        return direction;
    }
}
