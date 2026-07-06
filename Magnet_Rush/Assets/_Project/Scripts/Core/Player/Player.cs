using System;
using System.Collections;
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

    /// <summary>落下リスポーン開始時に発火。カメラを止める（追従解除）用。CameraSettingsApplier が購読。</summary>
    public static event Action OnFallRespawnStart;

    /// <summary>落下リスポーン完了時に発火。カメラ追従を戻して新しい足場へカットする用。</summary>
    public static event Action OnFallRespawnEnd;

    /// <summary>スタブ演出開始時に発火。引数=突き刺し目標Transform, 演出プロファイルindex。カメラ寄せ用。</summary>
    public static event Action<Transform, int> OnStabFinisherStart;

    /// <summary>スタブ演出終了時に発火。カメラを通常追従へ戻す用。</summary>
    public static event Action OnStabFinisherEnd;

    /// <summary>突きがボスに刺さった瞬間（ヒットVFXと同フレーム）に発火。カメラの着弾アップ＋スロー開始用。</summary>
    public static event Action OnStabFinisherImpact;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
        OnPlayerReady = null;
        OnFallRespawnStart = null;
        OnFallRespawnEnd = null;
        OnStabFinisherStart = null;
        OnStabFinisherEnd = null;
        OnStabFinisherImpact = null;
    }

    protected override float Gravity => m_settings.gravity;
    protected override float SnapForce => m_settings.snapForce;
    protected override float ExternalDrag => m_settings.externalDrag;
    protected override float GroundCheckDistance => m_settings.groundCheckDistance;
    protected override LayerMask GroundLayer => m_settings.groundLayer != 0 ? m_settings.groundLayer
        : (LayerMask)(PhysicsLayers.Bit(PhysicsLayers.Default) | PhysicsLayers.Bit(PhysicsLayers.Ground) | PhysicsLayers.Bit(PhysicsLayers.Wall));
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

    /// <summary>機能ロック状態（チュートリアル用）。無ければ全機能アンロック扱い。</summary>
    public PlayerAbilityLocker abilityLocker { get; private set; }

    /// <summary>最後に接地した位置の記録。落下リスポーンの戻り先に使う。任意（無くても固定スポーンへフォールバック）。</summary>
    private PlayerGroundTracker m_groundTracker;

    /// <summary>落下リスポーンの多重起動を防ぐフラグ。</summary>
    private bool m_isFallRespawning;

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
        abilityLocker = GetComponent<PlayerAbilityLocker>();
        m_groundTracker = GetComponent<PlayerGroundTracker>();

        if (m_settings.groundLayer == 0)
            Debug.LogWarning("[Player] PlayerSettings.groundLayerが未設定。環境(Default/Ground/Wall)の既定マスクを使用。");

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

    /// <summary>
    /// 奈落（デスボックス）に落ちたときのソフトリスポーン。HPは減らさず、最後に接地していた位置へ戻す。
    /// 敵に倒された場合の通常リスポーン（<see cref="Respawn"/>）とは別経路で、死亡演出も経由しない。
    /// 落下〜復帰の間はカメラを止め（OnFallRespawnStart）、復帰後に追従を戻す（OnFallRespawnEnd）。
    /// </summary>
    public void FallRespawn()
    {
        if (m_isFallRespawning)
        {
            ChannelLogger.LogGuardReturn("Player", "落下リスポーン処理中 — 多重起動をスキップ");
            return;
        }
        if (states.IsCurrentOfType<DiePlayerState>())
        {
            ChannelLogger.LogGuardReturn("Player", "死亡中 — 落下リスポーンは通常リスポーンに任せる");
            return;
        }
        StartCoroutine(FallRespawnRoutine());
    }

    private IEnumerator FallRespawnRoutine()
    {
        m_isFallRespawning = true;
        OnFallRespawnStart?.Invoke();

        // 落下中の入力・速度を断ち切ってからカメラ静止の余韻を待つ
        input.ClearBuffers();
        input.enabled = false;
        velocity = Vector3.zero;
        externalVelocity = Vector3.zero;

        float delay = m_settings != null ? m_settings.fallRespawnDelay : 0.5f;
        yield return new WaitForSeconds(delay);

        // 最後の足場へ。一度も接地していなければ固定スポーン地点へフォールバック
        if (m_groundTracker != null && m_groundTracker.TryGetLastGrounded(out var groundedPosition))
        {
            transform.position = groundedPosition;
        }
        else if (GameManager.Instance != null)
        {
            transform.position = GameManager.Instance.GetSpawnPosition();
        }
        else
        {
            ChannelLogger.LogGuardReturn("Game", "接地位置もGameManagerも無し — テレポートをスキップ");
        }

        velocity = Vector3.zero;
        externalVelocity = Vector3.zero;
        states.Change<IdlePlayerState>();
        input.enabled = true;

        m_isFallRespawning = false;
        OnFallRespawnEnd?.Invoke();
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
    // チュートリアルの機能ロックはここで一括ゲートする。
    // 移動は PlayerInputHandler.MoveInput、カメラは CameraSettingsApplier 側でゲートする（ラッパーを通らないため）

    /// <summary>指定機能がロック中か。PlayerAbilityLocker が無いシーンでは常にアンロック。</summary>
    public bool IsAbilityLocked(PlayerAbilityType ability)
        => abilityLocker != null && abilityLocker.IsLocked(ability);

    /// <summary>磁極切替(PoleAbility ラッパー)。</summary>
    public void SwitchPole()
    {
        if (IsAbilityLocked(PlayerAbilityType.PoleSwitch)) return;
        pole.Switch();
    }

    /// <summary>エイム入力処理(AimAbility ラッパー)。ロック時はエイム継続中でも強制解除する。</summary>
    public void UpdateAim()
    {
        if (IsAbilityLocked(PlayerAbilityType.Aim))
        {
            if (aim.IsAiming) aim.StopAim();
            return;
        }
        aim.UpdateInput();
    }

    /// <summary>通常射撃(ShootingAbility ラッパー)。</summary>
    public void Fire()
    {
        if (IsAbilityLocked(PlayerAbilityType.Fire)) return;
        shooting.Fire();
    }

    /// <summary>セルフファイア(ShootingAbility ラッパー)。</summary>
    public void SelfFire()
    {
        if (IsAbilityLocked(PlayerAbilityType.SelfFire)) return;
        shooting.SelfFire();
    }

    /// <summary>リロード(ShootingAbility ラッパー)。</summary>
    public void Reload()
    {
        if (IsAbilityLocked(PlayerAbilityType.Reload)) return;
        shooting.Reload();
    }

    /// <summary>ジャンプ(JumpAbility ラッパー)。jump プロパティは feature/jump 実装で接続される。</summary>
    public void Jump()
    {
        if (IsAbilityLocked(PlayerAbilityType.Jump)) return;
        jump.Jump();
    }

    /// <summary>スタブ攻撃(StabAbility ラッパー)。stab プロパティは feature/stab 実装で接続される。</summary>
    public void Stab()
    {
        if (IsAbilityLocked(PlayerAbilityType.Stab)) return;
        stab.Stab();
    }

    /// <summary>BossStabFinisherState から呼ぶ。カメラ演出の開始/終了を通知する。</summary>
    public void FireStabFinisherStart(Transform target, int variant) => OnStabFinisherStart?.Invoke(target, variant);
    public void FireStabFinisherEnd() => OnStabFinisherEnd?.Invoke();

    /// <summary>StabAbility.OnStabHitEvent から呼ぶ。突き刺さりの瞬間をカメラへ通知する（着弾アップ＋スロー開始）。</summary>
    public void FireStabFinisherImpact() => OnStabFinisherImpact?.Invoke();

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
        // 死亡・スタブ演出中は自前で transform を動かすため共通物理ステップ(重力/衝突/磁力)をスキップする。
        bool controlsOwnTransform = isDying || states.IsCurrentOfType<BossStabFinisherState>();
        // 磁力張り付き中も重力・移動を完全停止（ビタ止め）するため物理ステップをスキップする。
        bool isMagnetStuck = states.IsCurrentOfType<MagnetStickPlayerState>();

        // 能力呼び出しは各 State の OnStep が Player.TickAllAbilities() 経由で行う。
        // Stab/Die は OnStep を空にすることで自動的に全入力ロックされる。

        bool isStabFinisher = states.IsCurrentOfType<BossStabFinisherState>();

        // 通常時は従来どおり Update ベースで動かす。
        // スタブ演出はスロー中も PlayerRoot と Camera の Timeline を同じ描画フレームで評価し、ずれを防ぐ。
        if (!IsSlowMotion || isStabFinisher)
        {
            float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
            states.UpdateState(dt);

            // UpdateState 内でスタブへ遷移する場合があるため、遷移後も再判定する。
            // 遷移フレームに通常移動を重ねると Timeline の先頭姿勢が一度押し戻されてガクつく。
            bool controlsOwnTransformAfterState = states.IsCurrentOfType<DiePlayerState>()
                || states.IsCurrentOfType<BossStabFinisherState>();
            bool isMagnetStuckAfterState = states.IsCurrentOfType<MagnetStickPlayerState>();
            if (!controlsOwnTransform && !controlsOwnTransformAfterState
                && !isMagnetStuck && !isMagnetStuckAfterState)
                UpdateEntity(dt);
        }
    }

    void FixedUpdate()
    {
        // スロー時のみ FixedUpdate で動かし、TransformInterpolator にサブフレーム補間させる
        if (!IsSlowMotion) return;

        // スタブ Timeline は Update で PlayerRoot と Camera をまとめて評価する。
        // ここでも進めると二重更新になり、両トラックの再生位置がずれる。
        if (states.IsCurrentOfType<BossStabFinisherState>()) return;

        bool isDying = states.IsCurrentOfType<DiePlayerState>();
        bool controlsOwnTransform = isDying || states.IsCurrentOfType<BossStabFinisherState>();
        bool isMagnetStuck = states.IsCurrentOfType<MagnetStickPlayerState>();
        float dt = Time.fixedDeltaTime;
        states.UpdateState(dt);

        // FixedUpdate 内でスタブへ遷移した場合も、同じ物理ステップの通常移動を重ねない。
        bool controlsOwnTransformAfterState = states.IsCurrentOfType<DiePlayerState>()
            || states.IsCurrentOfType<BossStabFinisherState>();
        bool isMagnetStuckAfterState = states.IsCurrentOfType<MagnetStickPlayerState>();
        if (!controlsOwnTransform && !controlsOwnTransformAfterState
            && !isMagnetStuck && !isMagnetStuckAfterState)
            UpdateEntity(dt);
    }

    /// <summary>
    /// 磁力で壁・地面にビタ止めできる状況か。
    /// 磁化中に磁力で引かれていて、引かれる方向の至近距離に張り付き対象レイヤーの面がある時 true。
    /// </summary>
    public bool CanMagnetStick()
    {
        if (m_settings == null) return false;
        if (magnetizable == null || !magnetizable.IsActive) return false;
        if (!HasAttractionInPullDirection(m_settings.magnetStickMinPullSpeed)) return false;

        // 押し付け深さ（magnetStickIntoSurfaceDot）を要求し、遠くの磁石へ横向きに引かれながら
        // 面を掠っただけで張り付く誤発動を弾く
        if (!IsPulledIntoSurface(m_settings.magnetStickLayer,
            m_settings.magnetStickCheckDistance, m_settings.magnetStickMinPullSpeed,
            m_settings.magnetStickIntoSurfaceDot, out RaycastHit surfaceHit)) return false;

        // ビタ止めは壁のみ。床にも張り付けると、反発ジャンプ後の着地の瞬間に
        // 近くの磁化物への引きで床に固定されて「硬直」して見えるため
        return Vector3.Angle(Vector3.up, surfaceHit.normal) >= m_settings.magnetStickWallAngleMinDeg;
    }

    private bool HasAttractionInPullDirection(float minPullSpeed)
    {
        Vector3 pull = externalVelocity + holdVelocity;
        if (pull.sqrMagnitude < minPullSpeed * minPullSpeed) return false;

        var manager = MagnetManager.Instance;
        if (manager == null || manager.Settings == null) return false;

        MagneticPole selfPole = magnetizable.Pole;
        if (selfPole == MagneticPole.None) return false;

        Vector3 pullDir = pull.normalized;
        Vector3 selfPosition = magnetizable.Position;
        float maxRange = manager.Settings.magnetRange;
        float maxRangeSqr = maxRange * maxRange;
        var activeMagnets = manager.GetActiveMagnetizables();

        for (int i = 0; i < activeMagnets.Count; i++)
        {
            Magnetizable other = activeMagnets[i];
            if (other == null || other == magnetizable || !other.IsActive) continue;
            if (other.Pole == MagneticPole.None || other.Pole == selfPole) continue;

            Vector3 toOther = other.Position - selfPosition;
            float distanceSqr = toOther.sqrMagnitude;
            if (distanceSqr < 0.0001f || distanceSqr > maxRangeSqr) continue;

            float effectiveOuter = Mathf.Max(
                magnetizable.FieldOuterRadius + other.FieldInnerRadius,
                other.FieldOuterRadius + magnetizable.FieldInnerRadius);
            if (effectiveOuter <= 0f) effectiveOuter = maxRange;
            if (distanceSqr > effectiveOuter * effectiveOuter) continue;

            if (Vector3.Dot(pullDir, toOther.normalized) > 0.35f)
                return true;
        }

        return false;
    }

    /// <summary>
    /// IMagnetTarget実装のoverride。プレイヤーは空中でのみ磁力を受ける。
    /// </summary>
    public override void ApplyMagnetForce(Vector3 force)
    {
        if (IsGrounded)
        {
            ChannelLogger.LogGuardReturn("Player", "接地中は磁力無効");
            return;
        }
        base.ApplyMagnetForce(force);
    }

    /// <summary>
    /// 磁力場の影響度に応じてEntity multiplierを更新する。
    /// 強い磁力を受けているほど移動が鈍くなる（空中のみ。接地中は磁力の影響を受けない）。
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

        if (IsGrounded)
        {
            topSpeedMultiplier = 1f;
            turningDragMultiplier = 1f;
            ChannelLogger.LogGuardReturn("Player", "接地中は磁力鈍化なし");
            return;
        }

        float influence = magnetizable.GetInfluence(MagnetManager.Instance.Settings.influenceNormalizeForce);
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
