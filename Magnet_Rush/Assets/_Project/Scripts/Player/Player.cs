using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
public class Player : Entity
{
    [SerializeField] private PlayerSettings settings;

    public PlayerInputHandler input { get; private set; }
    public PlayerEvents events { get; private set; }
    public PlayerStateManager states { get; private set; }
    public PlayerSettings Settings => settings;
    public Magnetizable magnetizable { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInputHandler>();
        events = GetComponent<PlayerEvents>();
        states = GetComponent<PlayerStateManager>();
        magnetizable = GetComponent<Magnetizable>();

        // SO値をEntity基底フィールドに反映
        m_pullOrientationThreshold = settings.pullOrientationThreshold;
        m_pullOrientationSpeed = settings.pullOrientationSpeed;

        // HP=0でDiePlayerStateに遷移
        if (health != null)
        {
            health.OnDie += OnDie;
        }
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnDie -= OnDie;
        }
    }

    private void OnDie()
    {
        states.Change<DiePlayerState>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        UpdateMagneticInfluence();
        states.Step(dt);
        UpdateGround();
        UpdateMagneticOrientation(dt);
        ApplyGravity(settings.gravity, settings.snapForce, dt);
        ApplyMovement(dt);
    }

    /// <summary>
    /// 磁力場の影響度に応じてEntity multiplierを更新する。
    /// 強い磁力を受けているほど移動が鈍くなる。
    /// </summary>
    private void UpdateMagneticInfluence()
    {
        if (magnetizable == null || MagnetManager.Instance == null
            || MagnetManager.Instance.Settings == null)
        {
            topSpeedMultiplier = 1f;
            turningDragMultiplier = 1f;
            return;
        }

        float influence = magnetizable.GetInfluence(MagnetManager.Instance.Settings.maxForcePerObject);
        float damping = MagnetManager.Instance.Settings.magnetSpeedDamping;

        topSpeedMultiplier = 1f - influence * damping;
        turningDragMultiplier = 1f + influence * damping;
    }

    /// <summary>
    /// カメラ相対の入力方向に加速し、進行方向を向く。
    /// </summary>
    public void AccelerateToInputDirection(float dt)
    {
        var direction = GetCameraRelativeDirection(input.MoveInput);
        if (direction.sqrMagnitude > 0.01f)
        {
            Accelerate(direction, settings.turningDrag, settings.acceleration, settings.topSpeed, dt);
            FaceDirection(direction, settings.rotationSpeed, dt);
        }
    }

    public void MoveWithInput(float dt)
    {
        AccelerateToInputDirection(dt);
    }

    public void MoveWithInputStrafe(float dt)
    {
        Vector3 dir = GetCameraRelativeDirection(input.MoveInput);
        float aimSpeed = settings.topSpeed * settings.aimMoveSpeedMultiplier;
        if (dir.sqrMagnitude > 0.01f)
        {
            Accelerate(dir, settings.turningDrag, settings.acceleration, aimSpeed, dt);
        }
        if (cachedCameraTransform != null)
        {
            Vector3 camForward = cachedCameraTransform.forward;
            camForward.y = 0f;
            FaceDirection(camForward, settings.rotationSpeed * 2f, dt, false);
        }
    }

    public void SlowDown(float dt)
    {
        Decelerate(settings.deceleration, dt);
    }

    /// <summary>
    /// 斜面での加減速を適用する
    /// </summary>
    public void RegularSlopeFactor(float dt)
    {
        SlopeFactor(settings.slopeUpwardForce, settings.slopeDownwardForce, dt);
    }
}
