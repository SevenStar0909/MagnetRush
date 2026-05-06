using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// プレイヤーエンティティ。入力・ステート・磁力の統合制御を行うハブ。
/// 能力系のうち射撃/エイムは Controller 分離、磁極は Player.cs 直保持。
/// Movement は Entity base の protected メソッド依存のため本クラスに保持。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
[RequireComponent(typeof(AimController))]
[RequireComponent(typeof(ShootingController))]
public class Player : Entity
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private PlayerSettings m_settings;

    /// <summary>プレイヤー設定SO。Controller から参照される唯一の保持者。</summary>
    public PlayerSettings Settings => m_settings;

    /// <summary>現在アクティブな Player インスタンス。Awakeで設定、OnDestroyでクリア。</summary>
    public static Player Current { get; private set; }

    /// <summary>Player.Awake で発火。シーン参照なしでサブシステムが Player を取得する用。</summary>
    public static event Action<Player> OnPlayerReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
        OnPlayerReady = null;
    }

    protected override float Gravity => m_settings.gravity;
    protected override float SnapForce => m_settings.snapForce;
    protected override float ExternalDrag => m_settings.externalDrag;
    protected override float GroundCheckDistance => m_settings.groundCheckDistance;
    protected override LayerMask GroundLayer => m_settings.groundLayer != 0 ? m_settings.groundLayer : PhysicsLayers.MaskGroundCheck;
    protected override float PullOrientationThreshold => m_settings.pullOrientationThreshold;
    protected override float PullOrientationSpeed => m_settings.pullOrientationSpeed;

    /// <summary>プレイヤーの入力ハンドラー。</summary>
    public PlayerInputHandler input { get; private set; }

    /// <summary>プレイヤーイベントの発火用。</summary>
    public PlayerEvents events { get; private set; }

    /// <summary>プレイヤーのステートマシン。</summary>
    public PlayerStateManager states { get; private set; }

    /// <summary>磁力影響を受けるコンポーネント。</summary>
    public Magnetizable magnetizable { get; private set; }

    /// <summary>射撃 Controller。</summary>
    public ShootingController shooting { get; private set; }

    /// <summary>エイム Controller。</summary>
    public AimController aim { get; private set; }

    /// <summary>現在の磁極（S または N）。デフォルトは S。</summary>
    public MagneticPole CurrentPole { get; private set; } = MagneticPole.S;

    /// <summary>磁極切替時に発火。UI 等が購読する。</summary>
    public event Action<MagneticPole> OnPoleChanged;

    /// <summary>Y 入力があれば磁極を切り替える。Player.Update から、または各 PlayerState.UpdateState から毎フレーム呼ぶ前提。</summary>
    public void SwitchPole()
    {
        if (input == null || events == null)
        {
            ChannelLogger.LogGuardReturn("Player", "SwitchPole: input または events が null");
            return;
        }
        if (!input.IsSwitchPolePressed) return;
        input.ConsumeSwitchPole();
        CurrentPole = CurrentPole == MagneticPole.S ? MagneticPole.N : MagneticPole.S;
        OnPoleChanged?.Invoke(CurrentPole);
        events.FirePoleSwitch();
    }

    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInputHandler>();
        events = GetComponent<PlayerEvents>();
        states = GetComponent<PlayerStateManager>();
        magnetizable = GetComponent<Magnetizable>();
        shooting = GetComponent<ShootingController>();
        aim = GetComponent<AimController>();

        if (m_settings.groundLayer == 0)
            Debug.LogWarning("[Player] PlayerSettings.groundLayerが未設定。PhysicsLayers.MaskGroundCheckを使用。");

        // HP=0でDiePlayerStateに遷移
        if (m_health != null)
        {
            m_health.OnDie += OnDie;
        }

        Current = this;
        OnPlayerReady?.Invoke(this);
    }

    void OnDestroy()
    {
        if (m_health != null)
        {
            m_health.OnDie -= OnDie;
        }
        if (Current == this) Current = null;
    }

    private void OnDie()
    {
        states.Change<DiePlayerState>();
    }

    void OnDisable()
    {
        // シーン遷移・オブジェクト破棄時にスロー状態を強制解除
        // aim は RequireComponent 保証、OnDisable 時点で sibling は生きている
        if (aim.IsAiming)
        {
            Time.timeScale = 1f;
        }
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
        UpdateMagneticInfluence();

        bool isDying = states.IsCurrentOfType<DiePlayerState>();

        // 入力消費系を State 横断で1箇所に集約。死亡中は処理しない
        // pole/aim/shooting の入力処理で State 遷移が発生する場合があり
        // （aim.StopAim() → MovePlayerState 遷移など）、入力処理直後の最新Stateで移動処理が走る
        if (!isDying)
        {
            SwitchPole();
            aim.UpdateInput();
            shooting.Fire();
            shooting.SelfFire();
            shooting.Reload();
        }

        states.UpdateState(dt);   // State は移動・遷移判定のみ

        // 死亡中は重力・移動処理をスキップ（UpdateEntityがvelocityを上書きして落下するのを防ぐ）
        if (!isDying)
            UpdateEntity(dt);
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
            ChannelLogger.LogGuardReturn("Player", "Magnetizable/MagnetManager未取得");
            return;
        }

        float influence = magnetizable.GetInfluence(MagnetManager.Instance.Settings.maxForcePerObject);
        float damping = MagnetManager.Instance.Settings.magnetSpeedDamping;

        topSpeedMultiplier = 1f - influence * damping;
        turningDragMultiplier = 1f + influence * damping;
    }

    // --- Movement ---（Entity base の protected メソッド依存のため Player に保持）

    /// <summary>カメラ相対の入力方向に加速し、進行方向を向く。</summary>
    public void AccelerateToInputDirection(float dt)
    {
        var direction = GetCameraRelativeDirection(input.MoveInput);
        if (direction.sqrMagnitude > 0.01f)
        {
            Accelerate(direction, m_settings.turningDrag, m_settings.acceleration, m_settings.topSpeed, dt);
            FaceDirection(direction, m_settings.rotationSpeed, dt);
        }
    }

    /// <summary>エイム中のストレイフ移動。カメラ方向を向いたまま横移動する。</summary>
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

    /// <summary>横移動速度を減速する。</summary>
    public void SlowDown(float dt)
    {
        Decelerate(m_settings.deceleration, dt);
    }

    /// <summary>
    /// 指定ワールド方向の水平成分に体を即座に向ける。射撃時に弾の発射方向へスナップする用途。
    /// 仰角（Y成分）は無視するためモデルが空を向くことはない。
    /// </summary>
    public void FaceHorizontalInstant(Vector3 worldDirection)
    {
        Vector3 flat = worldDirection;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(flat.normalized);
    }
}
