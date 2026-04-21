using System;
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

    [Header("Shooting")]
    [FormerlySerializedAs("bulletSettings")]
    [SerializeField] private BulletSettings m_bulletSettings;

    [FormerlySerializedAs("firePoint")]
    [SerializeField] private Transform m_firePoint;

    [SerializeField] private float m_selfFireHeightOffset = 1.0f;

    private Camera m_mainCamera;

    private const float k_ForwardDotThreshold = 0.1f;

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

    // --- 磁極制御 ---

    /// <summary>現在の磁極（S または N）。</summary>
    public MagneticPole CurrentPole { get; private set; } = MagneticPole.S;

    /// <summary>磁極切替時に発火。UI 等が購読。</summary>
    public event Action<MagneticPole> OnPolarityChanged;

    /// <summary>
    /// Y 入力があれば磁極を切り替える。毎フレーム呼ぶ前提。
    /// </summary>
    public void SwitchPole()
    {
        if (!input.ConsumeSwitchPole()) return;
        CurrentPole = CurrentPole == MagneticPole.S ? MagneticPole.N : MagneticPole.S;
        OnPolarityChanged?.Invoke(CurrentPole);
        events?.FirePolaritySwitch();
    }

    // --- エイム制御 ---

    /// <summary>エイム中かどうか。</summary>
    public bool IsAiming { get; private set; }

    /// <summary>エイム状態変化時に発火。CameraSettingsApplier 等が購読。</summary>
    public static event Action<bool> OnAimChanged;

    private float m_aimReleaseGrace;

    /// <summary>LT 入力に応じてエイムモードを開始/維持する。毎フレーム呼ぶ。</summary>
    public void HandleAimInput()
    {
        if (input.AimHeld)
        {
            m_aimReleaseGrace = m_settings.aimReleaseGraceTime;
            if (!IsAiming) StartAim();
        }
        else if (IsAiming)
        {
            m_aimReleaseGrace -= Time.unscaledDeltaTime;
            if (m_aimReleaseGrace <= 0f) StopAim();
        }
    }

    /// <summary>エイムモード開始。スロー + ステート遷移。</summary>
    public void StartAim()
    {
        IsAiming = true;
        Time.timeScale = m_settings.aimTimeScale;
        OnAimChanged?.Invoke(true);
        states.Change<AimPlayerState>();
    }

    /// <summary>エイムモード終了。入力があれば Move、なければ Idle に戻る。</summary>
    public void StopAim()
    {
        IsAiming = false;
        Time.timeScale = 1f;
        OnAimChanged?.Invoke(false);

        if (input != null && input.MoveInput.sqrMagnitude > 0.01f)
            states.Change<MovePlayerState>();
        else
            states.Change<IdlePlayerState>();
    }

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
        SwitchPole();                        // 一時的にここで呼ぶ（将来 State 側に移す）
        HandleAimInput();
        Fire();
        SelfFire();
        Reload();
        states.UpdateState(dt);

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

    // --- 射撃 ---

    /// <summary>RT 入力があれば通常射撃。毎フレーム呼ぶ。</summary>
    public void Fire()
    {
        if (!input.ConsumeFire()) return;
        if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null)
        { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定"); return; }
        if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot())
        { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可"); return; }
        if (m_mainCamera == null)
        { ChannelLogger.LogGuardReturn("Player", "MainCameraなし"); return; }

        // 発射位置を先に確定
        float height = m_settings != null ? m_settings.firePointHeight : 1.2f;
        Vector3 spawnPos = m_firePoint != null ? m_firePoint.position : transform.position + Vector3.up * height;

        // 画面中央からカメラレイ取得
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Ray ray = m_mainCamera.ScreenPointToRay(screenCenter);
        Vector3 camForward = m_mainCamera.transform.forward;

        int layerMask = PhysicsLayers.MaskShootingRaycast;
        float maxDist = m_bulletSettings.raycastDistance;

        Vector3 targetPoint = CalculateTargetPoint(ray, camForward, spawnPos, layerMask, maxDist);

        float debugDuration = 3.0f;
        Debug.DrawLine(ray.origin, targetPoint, Color.cyan, debugDuration);
        Debug.DrawLine(spawnPos, targetPoint, Color.yellow, debugDuration);

        Vector3 direction = (targetPoint - spawnPos).normalized;

        GameObject bulletObj = Instantiate(m_bulletSettings.bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
        var bullet = bulletObj.GetComponent<MagnetBullet>();
        if (bullet != null)
        {
            bullet.Initialize(CurrentPole, direction);
            BulletManager.Instance.Register(bullet);
            bullet.OnImpact += StopAim;
        }

        events?.FireShoot();
    }

    /// <summary>A / F 入力があればセルフファイア（自己磁化）。毎フレーム呼ぶ。</summary>
    public void SelfFire()
    {
        if (!input.ConsumeSelfFire()) return;
        if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null)
        { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定(SelfFire)"); return; }
        if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot())
        { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可(SelfFire)"); return; }

        if (magnetizable != null)
            magnetizable.SetPole(CurrentPole);

        var fieldSettings = m_bulletSettings.bulletFieldSettings;
        if (fieldSettings != null)
        {
            var existing = GetComponent<MagnetField>();
            if (existing == null)
            {
                var field = gameObject.AddComponent<MagnetField>();
                field.Initialize(CurrentPole, fieldSettings);

                if (MagnetManager.Instance != null)
                    MagnetManager.Instance.RegisterField(field);

                var visualizer = gameObject.AddComponent<MagnetFieldVisualizer>();
                visualizer.Show(CurrentPole, fieldSettings);

                GameObject effectPrefab = CurrentPole == MagneticPole.S
                    ? m_bulletSettings.impactEffect_S
                    : m_bulletSettings.impactEffect_N;
                GameObject effectInstance = null;
                if (effectPrefab != null)
                {
                    effectInstance = Instantiate(effectPrefab, transform);
                    effectInstance.transform.localPosition = Vector3.zero;
                }

                field.OnFieldExpired += () =>
                {
                    if (magnetizable != null) magnetizable.Deactivate();
                    if (visualizer != null) Destroy(visualizer);
                    if (effectInstance != null) Destroy(effectInstance);
                };
            }
        }

        if (BulletManager.Instance != null)
            BulletManager.Instance.IncrementShotCount();

        events?.FireSelfShoot();
    }

    /// <summary>X 入力があればリロード（全弾クリア）。毎フレーム呼ぶ。</summary>
    public void Reload()
    {
        if (!input.ConsumeReload()) return;
        if (BulletManager.Instance == null) return;
        BulletManager.Instance.ClearAll();
        events?.FireReload();
    }

    /// <summary>弾道計算。カメラレイ交差 → 平面交差 → 前方フォールバック。</summary>
    private Vector3 CalculateTargetPoint(Ray ray, Vector3 camForward, Vector3 spawnPos, int layerMask, float maxDist)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, layerMask))
        {
            if (Vector3.Dot(camForward, hit.point - spawnPos) > 0f)
                return hit.point;
        }

        Plane firePlane = new Plane(Vector3.up, spawnPos);
        if (firePlane.Raycast(ray, out float enter) && enter > 0f)
        {
            Vector3 intersection = ray.GetPoint(enter);
            Vector3 toIntersection = (intersection - spawnPos).normalized;
            if (Vector3.Dot(camForward, toIntersection) > k_ForwardDotThreshold)
                return intersection;
        }

        return spawnPos + camForward * maxDist;
    }

}
