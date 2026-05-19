using System;
using UnityEngine;
/// <summary>
/// プレイヤーエンティティ。入力・ステート・磁力の統合制御を行うハブ。
/// 能力系（射撃/エイム/磁極）は同 GameObject 上の Controller に分離。
/// Movement は Entity base の protected メソッド依存のため本クラスに保持。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
[RequireComponent(typeof(PoleAbility))]
[RequireComponent(typeof(AimAbility))]
[RequireComponent(typeof(ShootingAbility))]
[RequireComponent(typeof(JumpAbility))]
[RequireComponent(typeof(StabAbility))]
public class Player : Entity
{
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

    /// <summary>射撃 Ability。</summary>
    public ShootingAbility shooting { get; private set; }

    /// <summary>エイム Ability。</summary>
    public AimAbility aim { get; private set; }

    /// <summary>磁極 Ability。</summary>
    public PoleAbility pole { get; private set; }

    /// <summary>ジャンプ Ability。Jump() メソッド本体は feature/jump で実装する。</summary>
    public JumpAbility jump { get; private set; }

    /// <summary>スタブ攻撃 Ability。Stab() / OnStabHitEvent() メソッド本体は feature/stab で実装する。</summary>
    public StabAbility stab { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInputHandler>();
        events = GetComponent<PlayerEvents>();
        states = GetComponent<PlayerStateManager>();
        magnetizable = GetComponent<Magnetizable>();
        shooting = GetComponent<ShootingAbility>();
        aim = GetComponent<AimAbility>();
        pole = GetComponent<PoleAbility>();
        jump = GetComponent<JumpAbility>();
        stab = GetComponent<StabAbility>();

        if (m_settings.groundLayer == 0)
            Debug.LogWarning("[Player] PlayerSettings.groundLayerが未設定。PhysicsLayers.MaskGroundCheckを使用。");

        // HP=0でDiePlayerStateに遷移
        if (m_health != null)
        {
            if (m_settings != null)
                m_health.SetMaxHealth(m_settings.maxHp);

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

    /// <summary>
    /// プレイヤーをリスポーン地点にテレポートし、HP/速度/状態を復帰させる。
    /// 死亡 → 待機 → このメソッド呼び出し、というオーケストレーションは GameManager が担当する。
    /// </summary>
    public void Respawn()
    {
        if (GameManager.Instance != null)
        {
            transform.position = GameManager.Instance.GetSpawnPosition();
        }
        else
        {
            ChannelLogger.LogGuardReturn("Game", "GameManager未取得 — スポーン地点テレポートをスキップ");
        }

        velocity = Vector3.zero;
        externalVelocity = Vector3.zero;

        if (m_health != null)
        {
            m_health.ResetHealth();
        }

        states.Change<IdlePlayerState>();
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

    /// <summary>
    /// スロー時のみ FixedUpdate + TransformInterpolator 経路に切り替える閾値。
    /// 通常時は Update ベース 60Hz 直書きで補間を介在させずフルレートの滑らかさを確保する。
    /// </summary>
    private const float k_SlowMotionThreshold = 0.99f;

    /// <summary>スロー時かどうか。TransformInterpolator も同条件で補間 ON/OFF を切り替える。</summary>
    public static bool IsSlowMotion => Time.timeScale < k_SlowMotionThreshold;

    // === Ability ラッパー(State.OnStep から呼ばれる Facade API) ===

    /// <summary>磁極切替(PoleAbility ラッパー)。</summary>
    public void SwitchPole() => pole.Switch();

    /// <summary>エイム入力処理(AimAbility ラッパー)。</summary>
    public void UpdateAim() => aim.UpdateInput();

    /// <summary>通常射撃(ShootingAbility ラッパー)。</summary>
    public void Fire() => shooting.Fire();

    /// <summary>セルフファイア(ShootingAbility ラッパー)。</summary>
    public void SelfFire() => shooting.SelfFire();

    /// <summary>リロード(ShootingAbility ラッパー)。</summary>
    public void Reload() => shooting.Reload();

    /// <summary>ジャンプ(JumpAbility ラッパー)。jump プロパティは feature/jump 実装で接続される。</summary>
    public void Jump() => jump.Jump();

    /// <summary>スタブ攻撃(StabAbility ラッパー)。stab プロパティは feature/stab 実装で接続される。</summary>
    public void Stab() => stab.Stab();

    /// <summary>
    /// 通常 State 用の全許可ヘルパ。Idle / Move / Aim 等が呼ぶ。
    /// 各 Ability は内部で入力 peek + 発動条件をチェックして no-op 判定するため、
    /// 毎フレーム呼んでも安全(`shooting.Fire()` が `IsFirePressed` で early return するのと同じパターン)。
    /// </summary>
    public void TickAllAbilities()
    {
        SwitchPole();
        UpdateAim();
        Fire();
        SelfFire();
        Reload();
        Jump();
        Stab();
    }

    void Update()
    {
        UpdateMagneticInfluence();

        bool isDying = states.IsCurrentOfType<DiePlayerState>();

        // 能力呼び出しは各 State の OnStep が Player.TickAllAbilities() 経由で行う。
        // Stab/Die は OnStep を空にすることで自動的に全入力ロックされる。

        // 通常時は従来どおり Update ベースで動かす（60Hz 直書きで滑らか）
        if (!IsSlowMotion)
        {
            float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
            states.UpdateState(dt);
            if (!isDying) UpdateEntity(dt);
        }
    }

    void FixedUpdate()
    {
        // スロー時のみ FixedUpdate で動かし、TransformInterpolator にサブフレーム補間させる
        if (!IsSlowMotion) return;

        bool isDying = states.IsCurrentOfType<DiePlayerState>();
        float dt = Time.fixedDeltaTime;
        states.UpdateState(dt);
        if (!isDying) UpdateEntity(dt);
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
