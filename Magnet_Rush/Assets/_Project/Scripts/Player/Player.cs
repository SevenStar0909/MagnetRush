using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// プレイヤーエンティティ。入力・ステート・磁力の統合制御を行う。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
public class Player : Entity
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private PlayerSettings m_settings;

    /// <summary>
    /// プレイヤーの入力ハンドラー。
    /// </summary>
    public PlayerInputHandler input { get; private set; }

    /// <summary>
    /// プレイヤーイベントの発火用。
    /// </summary>
    public PlayerEvents events { get; private set; }

    /// <summary>
    /// プレイヤーのステートマシン。
    /// </summary>
    public PlayerStateManager states { get; private set; }

    /// <summary>
    /// プレイヤー設定（ScriptableObject）。
    /// </summary>
    public PlayerSettings Settings => m_settings;

    /// <summary>
    /// 磁力影響を受けるコンポーネント。
    /// </summary>
    public Magnetizable magnetizable { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInputHandler>();
        events = GetComponent<PlayerEvents>();
        states = GetComponent<PlayerStateManager>();
        magnetizable = GetComponent<Magnetizable>();

        // SO値をEntity基底フィールドに反映
        m_gravity = m_settings.gravity;
        m_snapForce = m_settings.snapForce;
        m_pullOrientationThreshold = m_settings.pullOrientationThreshold;
        m_pullOrientationSpeed = m_settings.pullOrientationSpeed;
        m_groundCheckDistance = m_settings.groundCheckDistance;

        if (m_settings.groundLayer == 0)
            Debug.LogWarning("[Player] PlayerSettings.groundLayerが未設定。PhysicsLayers.MaskGroundCheckを使用。");
        m_groundLayer = m_settings.groundLayer != 0
            ? m_settings.groundLayer
            : PhysicsLayers.MaskGroundCheck;
        m_externalDrag = m_settings.externalDrag;

        // HP=0でDiePlayerStateに遷移
        if (m_health != null)
        {
            m_health.OnDie += OnDie;
        }
    }

    void OnDestroy()
    {
        if (m_health != null)
        {
            m_health.OnDie -= OnDie;
        }
    }

    private void OnDie()
    {
        states.Change<DiePlayerState>();
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
        UpdateMagneticInfluence();
        states.Step(dt);

        // 死亡中は重力・移動処理をスキップ（EntityStepがvelocityを上書きして落下するのを防ぐ）
        if (!states.IsCurrentOfType<DiePlayerState>())
            EntityStep(dt);
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
            Accelerate(direction, m_settings.turningDrag, m_settings.acceleration, m_settings.topSpeed, dt);
            FaceDirection(direction, m_settings.rotationSpeed, dt);
        }
    }

    /// <summary>
    /// 入力方向に加速し、進行方向を向く通常移動。
    /// </summary>
    public void MoveWithInput(float dt)
    {
        AccelerateToInputDirection(dt);
    }

    /// <summary>
    /// エイム中のストレイフ移動。カメラ方向を向いたまま横移動する。
    /// </summary>
    public void MoveWithInputStrafe(float dt)
    {
        Vector3 dir = GetCameraRelativeDirection(input.MoveInput);
        float aimSpeed = m_settings.topSpeed * m_settings.aimMoveSpeedMultiplier;
        if (dir.sqrMagnitude > 0.01f)
        {
            Accelerate(dir, m_settings.turningDrag, m_settings.acceleration, aimSpeed, dt);
        }
        if (m_cachedCameraTransform != null)
        {
            Vector3 camForward = m_cachedCameraTransform.forward;
            camForward.y = 0f;
            FaceDirection(camForward, m_settings.rotationSpeed * 2f, dt, false);
        }
    }

    /// <summary>
    /// 横移動速度を減速する。
    /// </summary>
    public void SlowDown(float dt)
    {
        Decelerate(m_settings.deceleration, dt);
    }

    /// <summary>
    /// 斜面での加減速を適用する
    /// </summary>
    public void RegularSlopeFactor(float dt)
    {
        SlopeFactor(m_settings.slopeUpwardForce, m_settings.slopeDownwardForce, dt);
    }
}
