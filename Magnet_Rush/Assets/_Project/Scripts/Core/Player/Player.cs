using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// プレイヤーエンティティ。入力・ステート・磁力の統合制御を行う。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
[RequireComponent(typeof(PolarityController))]
[RequireComponent(typeof(AimController))]
[RequireComponent(typeof(ShootingController))]
public partial class Player : Entity
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private PlayerSettings m_settings;

    /// <summary>プレイヤー設定SO。サブコンポーネントから参照される唯一の保持者。</summary>
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
    /// 磁力影響を受けるコンポーネント。
    /// </summary>
    public Magnetizable magnetizable { get; private set; }

    /// <summary>射撃 Controller。</summary>
    public ShootingController shooting { get; private set; }

    /// <summary>エイム Controller。</summary>
    public AimController aim { get; private set; }

    /// <summary>磁極 Controller。</summary>
    public PolarityController polarity { get; private set; }

    void Start()
    {
        m_mainCamera = Camera.main;
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
        polarity = GetComponent<PolarityController>();

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
        if (IsAiming)
        {
            Time.timeScale = 1f;
        }
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
        UpdateMagneticInfluence();
        states.UpdateState(dt);   // State 側で SwitchPole/HandleAimInput/Fire/SelfFire/Reload を呼ぶ

        // 死亡中は重力・移動処理をスキップ（UpdateEntityがvelocityを上書きして落下するのを防ぐ）
        if (!states.IsCurrentOfType<DiePlayerState>())
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

}
