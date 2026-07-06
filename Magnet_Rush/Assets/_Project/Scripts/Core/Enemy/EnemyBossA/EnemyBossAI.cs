using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ボスAI。8状態FSM (Idle/AttackStance/AttackMotion/Rush/Missile/Stunned/Stagger/Standing)。
/// 移動は EnemyBossBase.AccelerateToward 経由で EntityController が処理する（NavMeshAgent は使わない）。
/// AI → Animator: 状態読み取り (IsAttacking/IsInAttackMotion/IsStunned) のみ。書き込みは Animator が自分で行う。
/// Animator → AI: AnimEvent 経由の OnAttackFinished/OnStunEnd コールバック。
/// 依存: EnemyBossBase, EnemyBossBaseA_Animator
/// </summary>
[RequireComponent(typeof(EnemyBossBase))]
public class EnemyBossAI : MonoBehaviour, IStabReceiver, IDamageGuard
{
    public enum BossState { Idle, AttackStance, AttackMotion, Rush, Missile, Stunned, Stagger, Standing }
    private const string StabReadyEffectName = "StabReadyMagnetEffect";

    [Header("References")]
    [SerializeField] private EnemyBossBaseA_Animator m_animator;

    [Tooltip("スタブの突き刺し目標。頭ボーン下に置いた空オブジェクト（StabAnchor）をアサインする")]
    [SerializeField] private Transform m_stabAnchor;

    [Tooltip("このボス専用のスタブ演出設定（数値＋カメラTimeline）。未設定ならプレイヤー共通設定を使う")]
    [SerializeField] private StabFinisherSettings m_stabFinisherSettings;

    [Header("Shock Wave After Arm Attack Interrupted")]
    [Tooltip("AttackStance から Stunned / Stagger に入った時、周囲の PhysicsObject を押し出す")]
    [SerializeField] private bool m_shockAfterAttackStance = true;

    [Tooltip("AttackMotion から Stunned / Stagger に入った時、周囲の PhysicsObject を押し出す")]
    [SerializeField] private bool m_shockAfterAttackMotion = true;

    [Tooltip("衝撃波が PhysicsObject を押し出す範囲")]
    [Min(0f)]
    [SerializeField] private float m_shockRadius = 8f;

    [Tooltip("ボス中心から外側へ押し出す水平方向の力")]
    [Min(0f)]
    [SerializeField] private float m_shockHorizontalForce = 12f;

    [Tooltip("PhysicsObject を上へ持ち上げる力")]
    [Min(0f)]
    [SerializeField] private float m_shockUpwardlForce = 3f;

    [Header("Player Knockback")]
    [SerializeField] private float m_standImpactRadius = 8f;
    [SerializeField] private float m_standImpactHorizontalForce = 12f;
    [SerializeField] private float m_standImpactUpwardForce = 3f;
    [SerializeField] private float m_rushKnockbackHorizontalForce = 12f;
    [SerializeField] private float m_rushKnockbackUpwardForce = 3f;

    [Header("Stab Ready Effect")]
    [Tooltip("ダウンアニメーション開始からエフェクトを表示するまでの待ち時間")]
    [Min(0f)]
    [SerializeField] private float m_stabReadyEffectDelayAfterBreak = 3f;
    [Tooltip("スタブ可能時に磁力エフェクトを出す胸コア周りのボーンパス")]
    [SerializeField] private string m_stabReadyEffectBonePath = "Model/Boss01_Riging/Root/Oelvis/Body_Tube_1";
    [Tooltip("Scene上で位置調整したい場合に使う配置用Transform。未設定ならボーンパスを使う")]
    [SerializeField] private Transform m_stabReadyEffectSceneAnchor;
    [Tooltip("Scene/Prefab上に常時置いて調整するStabReadyMagnetEffect。未設定なら同名の子を探し、なければPrefabから生成する")]
    [SerializeField] private GameObject m_stabReadyEffectSceneObject;
    [SerializeField] private GameObject m_stabReadyEffectPrefab;
    [SerializeField] private Vector3 m_stabReadyEffectLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 m_stabReadyEffectLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 m_stabReadyEffectLocalScale = Vector3.one;
    [SerializeField] private Color m_stabReadyEffectColor = new Color(1f, 0.42f, 0.04f, 0.9f);
    [SerializeField, Min(0f)] private float m_stabReadyEffectBlinkCyclesPerSecond = 2f;
    [SerializeField, Range(0f, 1f)] private float m_stabReadyEffectMinAlpha = 0.18f;
    [SerializeField, Min(0f)] private float m_stabReadyEffectPulseScale = 0.18f;

    [Header("Debug")]
    [SerializeField] private bool m_logStateChange = true;
    public bool isBattleing = false;

    private EnemyBossBase m_boss;
    private BossMissileLauncher m_missileLauncher;
    private BossAttackHitboxes m_attackHitboxes;
    private Transform m_player;
    private EnemyBossSettings m_settings;
    private Stamina m_stamina;
    private Health m_health;
   

    // ボス本体（各ボーン）の Hitbox。物理オブジェクト接触でスタンゲージを溜める（機構1）。
    private Hitbox[] m_bodyHitboxes;
    // ボス配下の全 Magnetizable（本体root＋手などのHurtbox）。弾で付いた磁力の一括リセットに使う。
    private Magnetizable[] m_bodyMagnetizables;
    private readonly Collider[] m_interruptShockWaveBuffer = new Collider[64];
    private readonly HashSet<Rigidbody> m_interruptShockWaveBodies = new HashSet<Rigidbody>();

    private BossState m_state = BossState.Idle;
    private float m_cooldownTimer;
    private float m_staminaBreakTimer;
    private Vector3 m_rushTargetPosition;
    // Rush 突入時の進行方向。Rush 中は turningDrag の範囲でプレイヤー方向へ少しずつ補正する。
    private Vector3 m_rushDirection;

    [Header("Rush or missile")]
    [SerializeField] private bool m_nextLongRangeAttackIsRush = true; // rushとmissileを交互に行うためのフラグ
    [Min(0f)]
    [SerializeField] private float m_rushKeepSeconds = 0f;

    private bool m_wasInStunAnim;
    private bool m_wasInStaggerAnim;
    private float m_breakAnimationStartedTime = float.NegativeInfinity;
    private Transform m_stabReadyEffectAnchor;
    private GameObject m_stabReadyEffectRoot;
    private ParticleSystem[] m_stabReadyEffectParticles = Array.Empty<ParticleSystem>();
    private Renderer[] m_stabReadyEffectRenderers = Array.Empty<Renderer>();
    private Light[] m_stabReadyEffectLights = Array.Empty<Light>();
    private float[] m_stabReadyEffectLightBaseIntensities = Array.Empty<float>();
    private bool m_usePlacedStabReadyEffect;
    private Vector3 m_stabReadyEffectBaseLocalScale = Vector3.one;
    private bool m_warnedMissingStabReadyEffectAnchor;
    private bool m_warnedMissingStabReadyEffectPrefab;
    private bool m_staminaBreakEndRequested;
    private bool m_stabFinisherActive;
    private bool m_postStabHoldPending;      // スタブ命中後、起き上がりを遅らせて崩れたまま伏せている間 true
    private float m_postStabHoldTimer;       // その残り時間（秒）。0 で起き上がり（RecoverFromStab）
    // 演出開始〜終了まで持続する向きロック（命中で解除される m_stabFinisherActive と違い、立ち上がり中も維持）。
    private bool m_stabFinisherFacingLock;
    // 演出中の沈み込み防止。開始時の接地Yを保持し、LateUpdate で固定する（重力スナップ/崩れアニメ root motion を打ち消す）。
    private float m_stabFinisherFrozenY;

    // Rush 中に Animator が一度でも IsInRush=true になったか。
    // 入り transition と exit transition を区別し、exit 時の player 追尾回転を抑制する。
    private bool m_rushHasStarted;
    // DisableWindEffectEvent 後は回復姿勢に入るため、Rush 移動を停止する。
    private bool m_rushMovementStopped;
    private bool m_rushEndRequested;
    private float m_rushKeepTimer;
    private bool m_missileAnimationStarted;
    // Animator が実際にミサイル系ステートへ入ったのを見届けたか。Idle→MissileReady のブレンド中は
    // GetCurrentAnimatorStateInfo が Idle を返し続けるため、これを見ずに IsIdle を終了判定に使うと
    // 発射トリガー直後に Missile 状態を即抜けして AI と Animator がズレる（残留トリガーの温床）
    private bool m_missileAnimEntered;
    [SerializeField] private float m_missileFaceReadyAngleDeg = 0.5f;
    [Tooltip("ミサイル前にプレイヤーへ向き直るのを待つ上限（秒）。超えたら完全に向いていなくてもランダムで攻撃を強制開始する。0で無効")]
    [Min(0f)]
    [SerializeField] private float m_missileFaceWaitTimeoutSeconds = 2f;
    private float m_missileFaceWaitTimer;
    private float m_attackMissTurnTimer;
    private float m_attackMissTurnSpeedDegPerSec;
    private const float AttackMissTurnDuration = 1f;

    public event Action OnStabHitSucceeded;   // スタブが成功したときに発火

    public event Action OnArmAttackStarted;   // 振り下ろし（腕）攻撃を開始した時
    public event Action OnStaminaBroken;      // スンタゲージが削れきった（よろけた）時

    public BossState State => m_state;
    public bool IsInvincibleState => m_state == BossState.Standing;
    public bool CanTakeDamage(HitData hit) => !IsInvincibleState;

    public EnemyBossSettings Settings => m_settings;

    public Stamina Stamina => m_stamina;


    public void SetBattlingOn()
    {
        setisBattleing(true);
    }
    public void setisBattleing()
    {
        setisBattleing(!isBattleing);
    }
    public void setisBattleing(bool value)
    {
        isBattleing = value;
    }

    void Awake()
    {
        m_boss = GetComponent<EnemyBossBase>();
        m_missileLauncher = GetComponent<BossMissileLauncher>();
        m_attackHitboxes = GetComponentInChildren<BossAttackHitboxes>(true);
        m_stamina = GetComponent<Stamina>();
        m_health = GetComponent<Health>();

        // 右手の ArmStunHitbox は Hitbox 派生ではないので含まれない（＝カウンター経路と分離される）。
        m_bodyHitboxes = GetComponentsInChildren<Hitbox>(true);

        // Awake時点のプレハブ構成だけを対象にする（実行時に湧くミサイル等は含めない）
        m_bodyMagnetizables = GetComponentsInChildren<Magnetizable>(true);

        if (m_animator == null)
            m_animator = GetComponentInChildren<EnemyBossBaseA_Animator>();

        // 旧プレハブに残る NavMeshAgent は AI では使わないので無効化しておく（付いていても動かさない）。
        var navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null) navAgent.enabled = false;

        BuildStabReadyEffect();
    }

    void OnEnable()
    {
        if (m_stamina != null)
            m_stamina.OnBreak += HandleStaminaBreak;

        if (m_bodyHitboxes != null)
            foreach (var hb in m_bodyHitboxes)
                if (hb != null) hb.OnHitEvent += OnBodyHit;
    }

    void OnDisable()
    {
        if (m_stamina != null)
            m_stamina.OnBreak -= HandleStaminaBreak;

        if (m_bodyHitboxes != null)
            foreach (var hb in m_bodyHitboxes)
                if (hb != null) hb.OnHitEvent -= OnBodyHit;

        SetStabReadyEffectVisible(false);
    }

    void Start()
    {
        m_player = m_boss.Player;
        m_settings = m_boss.StatusData;

        if (m_animator == null)
            ChannelLogger.LogError("Enemy", $"[EnemyBossAI] {name}: EnemyBossBaseA_Animator 未設定");
    }

    void Update()
    {
        if (this.transform.position.y < -100f)
        {

            Vector3 pos = this.transform.position;
            pos.y = -25.0f;
            this.transform.position = pos;

        }

        if (m_player == null || m_settings == null || m_animator == null)
        { ChannelLogger.LogGuardReturn("Enemy", "プレイヤー/Settings/Animator未取得"); return; }

        float dt = Time.deltaTime;

        if (!isBattleing)
        {
            if (m_state != BossState.Idle)
                ChangeState(BossState.Idle);

            SetStabReadyEffectVisible(false);
            m_boss.SlowDown(dt);
            return;
        }

        m_cooldownTimer = Mathf.Max(0f, m_cooldownTimer - dt);

        TickStunEntry(); // Stunアニメーションの開始を検知してStunned状態に入り、回復タイマーを開始する
        TickStaggerEntry(); // Staggerアニメーションの開始を検知してStagger状態に入り、回復タイマーを開始する
        TickStaminaBreakTimer(dt); // Stunned/Staggered 共通の回復タイマー

        switch (m_state)
        {
            case BossState.Idle: TickIdle(dt); break;
            case BossState.AttackStance: TickAttackStance(dt); break;
            case BossState.AttackMotion: TickAttackMotion(dt); break;
            case BossState.Rush: TickRush(dt); break;
            case BossState.Missile: TickMissile(dt); break;
            case BossState.Stunned: TickStunned(dt); break;
            case BossState.Stagger: TickStagger(dt); break;
            case BossState.Standing: TickStanding(dt); break;
        }

        UpdateStabReadyEffect();
    }

    void LateUpdate()
    {
        // 演出中はボスを「刺される台」として安定させる。UpdateEntity(EnemyBossBase) の重力スナップや
        // 崩れアニメの root motion で沈むのを防ぐため、開始時の接地Yに固定する。
        // 演出外は毎フレーム来るので、ここはログを出さず静かに抜ける（RootMotionToController と同じ方針）。
        if (!m_stabFinisherFacingLock) return;
        Vector3 p = transform.position;
        if (!Mathf.Approximately(p.y, m_stabFinisherFrozenY))
        {
            p.y = m_stabFinisherFrozenY;
            transform.position = p;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
            return;

        UnityEditor.EditorApplication.delayCall -= EnsureStabReadyEffectInEditor;
        UnityEditor.EditorApplication.delayCall += EnsureStabReadyEffectInEditor;
    }

    [ContextMenu("Ensure Stab Ready Magnet Effect In Scene")]
    internal void EnsureStabReadyEffectInEditor()
    {
        if (this == null || Application.isPlaying)
            return;

        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            return;

        GameObject placedEffect = ResolvePlacedStabReadyEffect();
        if (placedEffect == null && m_stabReadyEffectPrefab != null)
        {
            Transform parent = ResolveStabReadyEffectAnchor();
            if (parent == null)
                parent = transform;

            placedEffect = UnityEditor.PrefabUtility.InstantiatePrefab(m_stabReadyEffectPrefab, parent) as GameObject;
            if (placedEffect == null)
                return;

            UnityEditor.Undo.RegisterCreatedObjectUndo(placedEffect, "Create Stab Ready Magnet Effect");
            placedEffect.name = StabReadyEffectName;
            placedEffect.transform.localPosition = m_stabReadyEffectLocalOffset;
            placedEffect.transform.localRotation = Quaternion.Euler(m_stabReadyEffectLocalEulerAngles);
            placedEffect.transform.localScale = m_stabReadyEffectLocalScale;
            placedEffect.SetActive(false);
        }

        if (placedEffect == null)
            return;

        placedEffect.name = StabReadyEffectName;

        bool changed = false;
        if (m_stabReadyEffectSceneObject != placedEffect)
        {
            m_stabReadyEffectSceneObject = placedEffect;
            changed = true;
        }

        if (!Mathf.Approximately(m_stabReadyEffectDelayAfterBreak, 3f))
        {
            m_stabReadyEffectDelayAfterBreak = 3f;
            changed = true;
        }

        if (!Mathf.Approximately(m_stabReadyEffectBlinkCyclesPerSecond, 2f))
        {
            m_stabReadyEffectBlinkCyclesPerSecond = 2f;
            changed = true;
        }

        if (changed)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }
#endif

    // スタンゲージが満タン（＝Stamina 0）になった時に Stamina.OnBreak から呼ばれる。
    // よろけ（Stagger）を1回だけ発火する。OnBreak はゲージが 0 に落ちた瞬間に1度だけ発火するのでループしない。
    // よろけ中もスタブ可（CanReceiveStab が Stagger を含む）。スタン（振り上げカウンター=ArmStunHitbox）でも同様にスタブできる。
    private void HandleStaminaBreak()
    {
        if (m_animator == null) return;
        if (IsInvincibleState) return;

        OnStaminaBroken?.Invoke();

        m_animator.TriggerBeInterrupted();
    }

    // ボス本体（ボーンの Hitbox）に物理オブジェクトが当たった時に呼ばれる。スタンゲージを蓄積する（機構1）。
    // 弾など磁化体でないものは無視。満タンになると Stamina.OnBreak → HandleStaminaBreak でよろけが発火する。
    private void OnBodyHit(HitData hit)
    {
        if (m_stamina == null) return;
        if (IsBreakOrStandingState()) return;                                // 崩れ中は無視
        if (m_stamina.IsBroken) return;                                      // 満タン到達済み（崩れ処理中）。崩れ終了時にリセットされる
        if (hit.source == null) return;
        if (hit.source.transform.IsChildOf(transform)) return;               // 自分由来は無視

        int percent = ResolveStunPercent(hit.source);
        if (percent <= 0) return;                                            // スタン値を持たない物（弾など）は無視

        int max = m_stamina.MaxStamina;
        int amount = Mathf.Max(1, Mathf.RoundToInt(max * percent / 100f));
        m_stamina.Consume(amount);

        ChannelLogger.Log("EnemyBossA",
            $"[StunGauge] 本体ヒット +{percent}% (+{amount}) 蓄積={max - m_stamina.CurrentStamina}/{max} src={hit.source.name}");
    }

    // ぶつかった物のスタン値蓄積率（％）を返す。箱=MagneticContactDamage（小10/大30）、誘導ミサイル=EnemyMissile（30）、
    // その他の磁化体は既定値、磁化体でなければ（弾など）0。
    private int ResolveStunPercent(GameObject source)
    {
        var contact = source.GetComponentInParent<MagneticContactDamage>();
        if (contact != null) return contact.StunGaugePercent;

        var missile = source.GetComponentInParent<EnemyMissile>();
        if (missile != null) return missile.StunGaugePercent;

        if (source.GetComponentInParent<Magnetizable>() != null)
            return m_settings != null ? m_settings.stunGaugePercentPerBodyHit : 10;

        return 0;
    }

    private void TickStunEntry()
    {
        bool inStunAnim = m_animator.IsStunned;
        // 既に Stunned/Stagger 中は再入場禁止。Animator のトランジション遅延中に IsStunned が true のまま残ると、
        // ChangeState(Idle) 後の次フレームで再検出されてループする
        bool alreadyInBreak = IsBreakOrStandingState();

        if (!alreadyInBreak && inStunAnim && !m_wasInStunAnim)
        {
            // 入場時に前サイクルの未消費な退場トリガーを掃除する。退場トリガーは「出した側」では即 Reset せず
            // 消費されるまで保持する方針なので、入場側でここで一度クリアして即抜けを防ぐ。
            m_animator.ResetStunEnd();
            m_animator.ResetStaggerEnd();

            m_staminaBreakTimer = Mathf.Max(0f, m_settings.staminaBreakDuration);
            m_staminaBreakEndRequested = false;
            m_breakAnimationStartedTime = Time.time;
            ChangeState(BossState.Stunned);
            // StunAnim に入ったら IsStunned bool を即落とす。AnyState→StunAnim は IsStunned==true で遷移するため、
            // true のままだと StunkeepAnim から AnyState 経由で StunAnim へ戻り続けてループする。
            // 状態保持は StunAnim→StunkeepAnim→(StunEnd)→Idle が担うので、bool は入場トリガーとして1回使えば十分。
            m_animator.SetIsStunnedFalse();
        }
        else if (alreadyInBreak && inStunAnim)
        {
            // 既に崩れ中に IsStunned bool が立つ（崩れ中の腕カウンター等）と、AnyState→StunAnim が
            // 引き続けて StunAnim↔StunkeepAnim で永久ループする。入場はブロックしつつ bool は消費して止める。
            m_animator.SetIsStunnedFalse();
        }

        m_wasInStunAnim = inStunAnim;
    }

    private void TickStaggerEntry()
    {
        bool inStaggerAnim = m_animator.IsInStagger;
        // Stun と同じ理由でループ防止
        bool alreadyInBreak = IsBreakOrStandingState();

        if (!alreadyInBreak && inStaggerAnim && !m_wasInStaggerAnim && !m_animator.IsStunned)
        {
            m_animator.ResetStunEnd();
            m_animator.ResetStaggerEnd();

            // よろけ（蓄積ルート）は専用の継続時間を使う。仕様＝10秒。スタン（カウンタールート）は staminaBreakDuration＝5秒。
            m_staminaBreakTimer = Mathf.Max(0f, m_settings.staggerDuration);
            m_staminaBreakEndRequested = false;
            m_breakAnimationStartedTime = Time.time;
            ChangeState(BossState.Stagger);
        }

        m_wasInStaggerAnim = inStaggerAnim;
    }

    private void TickStaminaBreakTimer(float dt)
    {
        if (m_state != BossState.Stunned && m_state != BossState.Stagger) return;

        // スタブ命中後の「ダウン保持」を最優先で処理する。演出ロック(m_stabFinisherActive)に関わらず時間を計り、
        // 経過したら起き上がる。これが「刺した直後に起き上がるのが速すぎる」対策。
        if (m_postStabHoldPending)
        {
            m_postStabHoldTimer = Mathf.Max(0f, m_postStabHoldTimer - dt);
            if (m_postStabHoldTimer > 0f) return;
            RecoverFromStab();
            return;
        }

        if (m_stabFinisherActive) return; // 演出中は崩れ回復を止める（途中で立ち上がらせない）
        if (m_staminaBreakEndRequested) return;

        m_staminaBreakTimer = Mathf.Max(0f, m_staminaBreakTimer - dt);
        if (m_staminaBreakTimer > 0f) return;

        m_staminaBreakEndRequested = true;

        // 崩れ回復時はスタミナをリセットする。スタンゲージが満タンのままだと、次の被弾で即よろけてループする。
        if (m_stamina != null)
            m_stamina.ResetStamina();

        // sssWind

        EndBreakAnimations();
        ChangeState(BossState.Standing);
    }

    // 崩れ（Stun/Stagger）を終了させる退場トリガーを出す。m_state ではなく「実際に再生中のアニメ状態」を見て
    // 一致する退場トリガーを出すので、再トリガーで m_state とアニメがズレていても確実に keep ループから抜ける。
    // トリガーはここで Reset しない（消費されるまで保持）。Reset は次の崩れ入場時に行う＝即抜け事故と取り逃し事故の両方を防ぐ。
    private void EndBreakAnimations()
    {
        if (m_animator == null) return;

        bool inStunAnim = m_animator.IsStunned;
        bool inStaggerAnim = m_animator.IsInStagger;

        if (inStunAnim) m_animator.TriggerStunEnd();
        if (inStaggerAnim) m_animator.TriggerStaggerEnd();

        // どちらのアニメ状態でもない（遷移中など）場合の保険として両方出す
        if (!inStunAnim && !inStaggerAnim)
        {
            m_animator.TriggerStunEnd();
            m_animator.TriggerStaggerEnd();
        }

        if (m_stamina != null)
        {
            m_stamina.ResetStamina();
        }

        m_animator.SetIsStunnedFalse();
    }

    // === 状態遷移 ===

    private void ChangeState(BossState next)
    {
        if (next == m_state) return;
        if (m_logStateChange)
            ChannelLogger.Log("EnemyBossA", $"[EnemyBossAI] {m_state} → {next}");

        var prev = m_state;
        m_state = next;

        ResetStaleAttackTriggers(next);

        // 崩れ（Stun/Stagger）中だけ接地スナップを切る。崩れアニメで胴体が沈む間の下押しを止め、
        // 本体が地面にめり込むのを防ぐ。崩れを抜けた瞬間（Idle 等）に false へ戻して通常の接地に復帰する。
        if (m_boss != null)
            m_boss.SuppressGroundSnap = next == BossState.Stunned || next == BossState.Stagger || next == BossState.Standing;

        TryEmitInterruptShockWave(prev, next);

        // 振り上げ攻撃の終了時とダウン（Stun/Stagger）確定時に、弾で付いた磁力を本体から一掃する。
        // 前回の攻撃で付いた磁力が残ると、次の振り上げでプレイヤーが何もしなくても
        // クレートが磁化部位へ飛んでダウンしてしまうため。
        bool leftArmAttack = IsArmAttackState(prev) && !IsArmAttackState(next);
        bool enteredBreak = next == BossState.Stunned || next == BossState.Stagger;
        if (leftArmAttack || enteredBreak)
            ResetBodyMagnetismAndRefundAmmo();

        if (next == BossState.Rush)
        {
            m_rushTargetPosition = m_player.position;
            m_boss.lateralVelocity = Vector3.zero;
            m_rushHasStarted = false; // 入り transition フェーズへ。Animator が IsInRush=true に入った時点で true 化
            m_rushMovementStopped = false;
            m_rushEndRequested = false;
            m_rushKeepTimer = 0f;
        }

        if (next == BossState.AttackStance)
        {
            m_attackHitboxes?.ResetArmHitThisAttack();
            OnArmAttackStarted?.Invoke();
        }

        if (next == BossState.Idle)
        {
            if (prev == BossState.AttackMotion && !DidArmAttackHit())
                BeginAttackMissTurn();

            ClearStaminaFlags();
            // Idle に戻る時は Wind/Dust を必ず止める。Rush の DisableWindEffectEvent が
            // 中断（被弾→Stagger 等）で発火しないまま Idle へ戻ると Wind が出続けるため、保険として停止する。
            // Wind/Dust は Rush/Stun 中しか点かないので Idle で消すのは常に正しい。

            // 押し出す衝撃波

            if (m_animator != null)
            {
                m_animator.DisableWindEffectEvent();
                m_animator.DisableDustEffectEvent();
            }
        }

        // ミサイル攻撃の入り口でトグルをリセット（必ず 1発目=上波 から始める）
        if (next == BossState.Missile)
        {
            m_missileAnimationStarted = false;
            m_missileAnimEntered = false;
            m_missileFaceWaitTimer = 0f;
            m_missileLauncher?.ResetWave();
        }

        // Rush 中に Stun/Stagger で割り込まれると Rush 側の Disable AnimEvent が発火せず
        // Wind/Dust が出続けるので、ブレイク入り口でも明示停止する
        if (next == BossState.Stunned || next == BossState.Stagger)
        {
            if (m_animator != null)
            {
                m_animator.DisableWindEffectEvent();
                m_animator.DisableDustEffectEvent();
            }
        }

        if (next == BossState.Standing && !m_stabFinisherActive)
            PlayStandingImpactEffect();

        if (prev == BossState.AttackMotion || prev == BossState.Rush || prev == BossState.Missile)
            m_cooldownTimer = m_settings.attackInterval;
    }

    private void TryEmitInterruptShockWave(BossState previous, BossState next)
    {
        bool enteredBreak = next == BossState.Stunned || next == BossState.Stagger;
        if (!enteredBreak)
            return;

        bool enabledForPreviousState =
            (previous == BossState.AttackStance && m_shockAfterAttackStance)
            || (previous == BossState.AttackMotion && m_shockAfterAttackMotion);
        if (!enabledForPreviousState)
            return;

        float radius = Mathf.Max(0f, m_shockRadius);
        if (radius <= 0f)
            return;

        int physicsObjectLayer = PhysicsLayers.PhysicsObject;
        if (physicsObjectLayer < 0)
            return;

        Vector3 center = transform.position;
        int count = Physics.OverlapSphereNonAlloc(
            center,
            radius,
            m_interruptShockWaveBuffer,
            1 << physicsObjectLayer,
            QueryTriggerInteraction.Ignore);

        m_interruptShockWaveBodies.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider col = m_interruptShockWaveBuffer[i];
            m_interruptShockWaveBuffer[i] = null;
            if (col == null)
                continue;

            Rigidbody body = col.attachedRigidbody;
            if (body == null)
                body = col.GetComponentInParent<Rigidbody>();
            if (body == null || body.isKinematic || !m_interruptShockWaveBodies.Add(body))
                continue;

            Vector3 horizontalDirection = body.worldCenterOfMass - center;
            horizontalDirection.y = 0f;
            if (horizontalDirection.sqrMagnitude <= 0.0001f)
                horizontalDirection = transform.forward;
            else
                horizontalDirection.Normalize();

            Vector3 impulse =
                horizontalDirection * Mathf.Max(0f, m_shockHorizontalForce)
                + Vector3.up * Mathf.Max(0f, m_shockUpwardlForce);
            body.AddForce(impulse, ForceMode.Impulse);
        }
        m_interruptShockWaveBodies.Clear();
    }

    /// <summary>
    /// 振り上げ攻撃（腕攻撃）中の状態か。磁力リセットとドームキャスト(BossHandMagnetCaster)で
    /// 同じ状態集合を共有する（片方だけ直して対象がズレるのを防ぐ）。
    /// </summary>
    public static bool IsArmAttackState(BossState state)
    {
        return state == BossState.AttackStance || state == BossState.AttackMotion;
    }

    // 弾で付いたボスの磁力（本体root＋手）を全解除し、使った分の残弾をプレイヤーへ返す。
    // ボスの磁化は弾着弾のみで起きる（ドームキャストは PhysicsObject 限定）ので、磁化1箇所＝弾1発として返却する。
    private void ResetBodyMagnetismAndRefundAmmo()
    {
        if (m_bodyMagnetizables == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Magnetizable未取得"); return; }

        // 先に返却数を確定する。root の DeactivateWithFields は子孫（手）のフィールドも
        // 巻き込んで解除するため、解除しながら数えると取りこぼす。
        int refundCount = 0;
        foreach (var mag in m_bodyMagnetizables)
            if (mag != null && mag.IsActive) refundCount++;

        if (refundCount == 0)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "磁化箇所なしでリセット不要"); return; }

        foreach (var mag in m_bodyMagnetizables)
        {
            if (mag == null || !mag.IsActive) continue;
            mag.DeactivateWithFields();
        }

        bool refunded = BulletManager.Instance != null;
        if (refunded)
            BulletManager.Instance.RefundShots(refundCount);

        ChannelLogger.Log("EnemyBossA", refunded
            ? $"[MagnetReset] ボスの磁力を{refundCount}箇所解除、残弾{refundCount}発返却"
            : $"[MagnetReset] ボスの磁力を{refundCount}箇所解除（BulletManager不在で残弾返却なし）");
    }

    // 攻撃系トリガーは、Stun/Stagger の AnyState 割り込みや Animator が Idle 以外にいるタイミングで
    // SetTrigger されると消費されずに残留する。残留したまま Animator が Idle へ戻ると、AI の状態と
    // 無関係に発火する（例: ミサイル発射と振り下ろしの同時発動）。状態の入り口で不要なトリガーを掃除する。
    // 直前の TickIdle / 強制攻撃で出したばかりの開始トリガー（Attack/AttackRush）は消さない。
    private void ResetStaleAttackTriggers(BossState next)
    {
        if (m_animator == null) return;

        switch (next)
        {
            case BossState.Stunned:
            case BossState.Stagger:
                m_animator.ResetAttack();
                m_animator.ResetMissile();
                m_animator.ResetAttackRush();
                break;
            case BossState.AttackStance:
                m_animator.ResetMissile();
                m_animator.ResetAttackRush();
                m_animator.ResetAttackFinished();
                break;
            case BossState.Rush:
                m_animator.ResetAttack();
                m_animator.ResetMissile();
                m_animator.ResetAttackRushFinished();
                break;
            case BossState.Missile:
                m_animator.ResetAttack();
                m_animator.ResetAttackRush();
                m_animator.ResetMissileFinished();
                break;
        }
    }

    private void ClearStaminaFlags()
    {
        if (m_animator == null) return;

        m_animator.SetIsStunnedFalse();
        // Animator のトランジション遅延中はまだ Stun/Stagger ステートに残っている。
        // ここで false 固定すると、次フレームで TickStunEntry/TickStaggerEntry が立ち上がりエッジを
        // 誤検出して再入場し、Stagger(Stun) がループする。実ステートに同期させてエッジ誤検出を防ぐ。
        m_wasInStunAnim = m_animator.IsStunned;
        m_wasInStaggerAnim = m_animator.IsInStagger;
        m_staminaBreakEndRequested = false;
    }

    // === 各状態の Tick ===

    private void TickIdle(float dt)
    {
        // Stunned/Stagger の入場検知は TickStunEntry/TickStaggerEntry が一元担当する。
        // ここで再検出すると Animator のトランジション遅延中に二重発火してループする

        m_boss.SlowDown(dt);

        float distance = DistanceToPlayer();

        // プレイヤーが起動範囲の外なら、向き直りも攻撃もせず Idle のまま待機する
        if (distance > m_settings.activationRange)
            return;

        FacePlayer(dt, m_settings.faceDeadZoneDeg);

        if (m_cooldownTimer > 0f)
            return;

        if (distance <= m_settings.attackRange)
        {
            m_animator.TriggerAttack();
            ChangeState(BossState.AttackStance);
            return;
        }

        if (m_settings.rushAttackRange < distance && distance <= m_settings.missileAttackRange)
        {
            m_animator.TriggerAttackRush();
            ChangeState(BossState.Rush);
            return;
        }

        if (m_settings.missileAttackRange < distance)
        {
            if (m_nextLongRangeAttackIsRush)
                m_animator.TriggerAttackRush();
            ChangeState(m_nextLongRangeAttackIsRush ? BossState.Rush : BossState.Missile);
            // 遠距離攻撃が実際に発動したときだけ Rush ↔ Missile を反転させる
            m_nextLongRangeAttackIsRush = !m_nextLongRangeAttackIsRush;
        }
    }

    private void TickAttackStance(float dt)
    {
        FacePlayer(dt, m_settings.faceDeadZoneDeg);
        m_boss.SlowDown(dt);

        if (m_animator.IsStunned) { ChangeState(BossState.Stunned); return; }
        if (m_animator.IsInAttackMotion) { ChangeState(BossState.AttackMotion); return; }
    }

    private void TickAttackMotion(float dt)
    {
        if (m_animator.IsIdle) { ChangeState(BossState.Idle); return; }

        // 普通攻擊移動なし
        m_boss.SlowDown(dt);
        FacePlayer(dt, m_settings.attackMotionFaceDeadZoneDeg);
    }

    private void TickRush(float dt)
    {
        if (!m_animator.IsInRush)
        {
            m_boss.lateralVelocity = Vector3.zero;
            // 入り transition フェーズだけ player 追尾。
            // exit transition フェーズ（rush 後）は回転固定 → rush 方向のまま Idle へ抜ける。
            if (!m_rushHasStarted)
            {
                FacePlayer(dt, m_settings.faceDeadZoneDeg);
                // 入り transition 中は live のプレイヤー位置でターゲットを更新する
                m_rushTargetPosition = m_player.position;
            }
            else if (m_animator.IsIdle)
            {
                ChangeState(BossState.Idle);
            }
            return;
        }

        if (!m_rushHasStarted)
        {
            // Rush 突入の瞬間に初期方向を確定する。
            Vector3 toTarget = m_rushTargetPosition - transform.position;
            toTarget.y = 0f;
            m_rushDirection = toTarget.sqrMagnitude > 0.0001f
                ? toTarget.normalized
                : transform.forward;
            m_rushHasStarted = true;
            m_rushKeepTimer = 0f;
        }

        // Wind 終了後は回復姿勢中。慣性で滑らないよう、Rush の水平移動を停止する。
        if (m_rushMovementStopped)
        {
            m_boss.lateralVelocity = Vector3.zero;
            return;
        }

        m_rushKeepTimer += dt;
        if (ShouldEndRushKeep())
        {
            RequestRushEnd();
            return;
        }

        // Kamikaze と同様に現在のプレイヤー位置を追うが、turningDrag で旋回量を制限して急旋回を防ぐ。
        Vector3 toPlayer = m_player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.0001f)
        {
            float turnRadians = Mathf.Max(0f, m_settings.turningDrag) * Mathf.Deg2Rad * dt;
            m_rushDirection = Vector3.RotateTowards(
                m_rushDirection,
                toPlayer.normalized,
                turnRadians,
                0f
            ).normalized;
        }

        m_boss.AccelerateToward(m_rushDirection, dt, m_settings.rushSpeedMultiplier);
    }

    private bool ShouldEndRushKeep()
    {
        if (m_rushEndRequested || m_rushKeepSeconds <= 0f)
            return false;

        Vector3 toTarget = m_rushTargetPosition - transform.position;
        toTarget.y = 0f;
        float arriveDistance = m_settings != null ? Mathf.Max(0f, m_settings.stopDistance) : 0f;
        if (toTarget.sqrMagnitude <= arriveDistance * arriveDistance)
            return true;

        return m_rushKeepTimer >= m_rushKeepSeconds;
    }

    private void RequestRushEnd()
    {
        m_rushEndRequested = true;
        m_rushMovementStopped = true;
        m_boss.lateralVelocity = Vector3.zero;
        m_animator.TriggerAttackRushFinished();
    }

    public void StopRushMovement()
    {
        if (m_state == BossState.Rush && m_rushHasStarted)
        {
            m_rushMovementStopped = true;
            m_boss.lateralVelocity = Vector3.zero;
        }
    }

    private void TickMissile(float dt)
    {
        m_boss.SlowDown(dt);

        if (!m_missileAnimationStarted)
        {
            if (FacePlayerForMissileStart(dt))
            {
                m_missileAnimationStarted = true;
                m_animator.TriggerMissile();
                return;
            }

            // 振り向き待ちが長引くとボスが棒立ちになるため、上限を超えたらランダムで攻撃を強制開始する
            m_missileFaceWaitTimer += dt;
            if (m_missileFaceWaitTimeoutSeconds > 0f && m_missileFaceWaitTimer >= m_missileFaceWaitTimeoutSeconds)
                ForceAttackAfterFaceWaitTimeout();
            return;
        }

        // Idle→MissileReady のブレンド中は current state が Idle のまま報告されるため、
        // ミサイル系ステートへ入ったのを見届けるまで IsIdle を終了判定に使わない
        if (!m_missileAnimEntered)
        {
            if (m_animator.IsInMissile)
                m_missileAnimEntered = true;
            return;
        }

        if (m_animator.IsIdle) { ChangeState(BossState.Idle); return; }
    }

    // 振り向き待ちがタイムアウトした時の強制攻撃。完全に向き直るのを諦め、
    // ミサイル即発射かラッシュ切替をランダムに選んで攻撃フェーズへ入る。
    private void ForceAttackAfterFaceWaitTimeout()
    {
        if (UnityEngine.Random.Range(0, 2) == 0)
        {
            m_missileAnimationStarted = true;
            m_animator.TriggerMissile();
            ChannelLogger.Log("EnemyBossA", "[FaceWait] 振り向き待ちタイムアウト → ミサイルを強制発射");
        }
        else
        {
            ChannelLogger.Log("EnemyBossA", "[FaceWait] 振り向き待ちタイムアウト → ラッシュへ切替");
            m_animator.TriggerAttackRush();
            ChangeState(BossState.Rush);
        }
    }

    private void TickStunned(float dt)
    {
        m_boss.SlowDown(dt);
    }

    private void TickStagger(float dt)
    {
        m_boss.SlowDown(dt);
        // Stagger 中はプレイヤーに向き直らない（Stunned と同じ挙動）
    }

    private void TickStanding(float dt)
    {
        m_boss.SlowDown(dt);
        if (m_animator != null && (m_animator.IsStunned || m_animator.IsInStagger || m_animator.IsStanding))
            return;

        ChangeState(BossState.Idle);
    }

    // === 公開コールバック (Animator → AI) ===

    /// <summary>AttackMotion clip 末尾の AnimEvent から呼ばれる。</summary>
    public void OnAttackFinished()
    {
        Debug.Log("[EnemyBossAI] OnAttackFinished called");
        if (m_state == BossState.AttackMotion)
            ChangeState(BossState.Idle);
    }

    /// <summary>AttackStun clip 末尾の AnimEvent から呼ばれる。</summary>
    public void OnRushFinished()
    {
        Debug.Log("[EnemyBossAI] OnRushFinished called");
        if (m_state == BossState.Rush)
            ChangeState(BossState.Idle);
    }

    /// <summary>AttackStun clip 末尾の AnimEvent から呼ばれる。</summary>
    public void OnMissileFinished()
    {
        if (m_state == BossState.Missile)
            ChangeState(BossState.Idle);
    }

    /// <summary>Missile 発射イベント専用。AnimationEvent から呼ばれ、BossMissileLauncher に1波分の発射を委譲する。</summary>
    public void OnMissileFireEvent()
    {
        if (m_missileLauncher != null && m_missileLauncher.FireNextWave())
            m_animator.TriggerMissileFinished();
    }

    // === IStabReceiver 実装 (Player → Boss スタブ受信) ===

    /// <summary>
    /// スタブを受け付ける条件。崩れ中 (Stunned=振り上げカウンター / Stagger=スタンゲージ満タン) に加え、
    /// スタン値が満タン (Stamina.IsBroken) の間も true。満タンはスタブを当てるまで維持されるので、
    /// 崩れアニメが終わって取り逃しても、近づいてスタブを決めれば成立する（ソフトロック防止）。
    /// </summary>
    public bool CanReceiveStab => !m_postStabHoldPending && IsBreakState();

    /// <summary>突き刺し目標。頭ボーン下の StabAnchor（Inspectorアサイン）。未設定なら本体 transform。</summary>
    public Transform StabAnchor => m_stabAnchor != null ? m_stabAnchor : transform;

    /// <summary>演出プロファイル選択。Stagger=0 / Stun=1。崩れでなければ 0。</summary>
    public int StabChoreographyIndex => m_state == BossState.Stunned ? 1 : 0;

    /// <summary>このボス専用のスタブ演出設定。未設定(null)ならプレイヤー共通設定にフォールバックさせる。</summary>
    public StabFinisherSettings StabFinisherSettings => m_stabFinisherSettings;

    /// <summary>
    /// プレイヤーのスタブAnimEventから呼ばれる。HPバー1本分を一気に削る（クールダウン無視）。
    /// data.damage は無視し、EnemyBossSettings.healthBarSegments と MaxHealth からバー境界HPを算出する。
    /// 死亡判定は Health 側で発火する OnDie に任せる。
    /// </summary>
    public void OnStabHit(StabHitData data)
    {
        if (!CanReceiveStab)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Stunned/Stagger 以外のため Stab 無効"); return; }

        if (m_health == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Health 未取得"); return; }

        if (m_settings == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "EnemyBossSettings 未取得"); return; }

        int segments = Mathf.Max(1, m_settings.healthBarSegments);
        int maxHp = m_health.MaxHealth;
        int curHp = m_health.CurrentHealth;

        // 現在残っているバー本数 → 1本減らした残数まで HP を一気に落とす
        int currentBarsRemaining = Mathf.CeilToInt((float)curHp * segments / maxHp);
        int targetBarsRemaining = Mathf.Max(0, currentBarsRemaining - 1);
        int targetHp = Mathf.FloorToInt((float)targetBarsRemaining * maxHp / segments);
        int damage = Mathf.Max(0, curHp - targetHp);

        if (damage <= 0)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Stab 算出ダメージ0"); return; }

        m_health.DamageIgnoreCooldown(damage);
        OnStabHitSucceeded?.Invoke();
        ChannelLogger.Log("EnemyBossA", $"[Stab] bar {currentBarsRemaining}→{targetBarsRemaining} dmg={damage} src={(data.source != null ? data.source.name : "null")} hp={m_health.CurrentHealth}/{maxHp}");

        // スタブを当てたらスタン値を0にリセットし、崩れを終了させる（1回のスタンにつきスタブ1回）。
        EndBreakAfterStab();
    }

    /// <summary>フィニッシャー演出の開始/終了を受け取り、演出中の崩れ回復を止める。</summary>
    public void BeginStabFinisher() { m_stabFinisherActive = true; m_stabFinisherFacingLock = true; m_stabFinisherFrozenY = transform.position.y; }
    public void EndStabFinisher() { m_stabFinisherActive = false; m_stabFinisherFacingLock = false; }
    public void PlayStandingImpactEffect()
    {
        if (m_animator != null)
            m_animator.PlayStandImpactEffect();

        if (m_player == null)
            return;

        Vector3 toPlayer = m_player.position - transform.position;
        toPlayer.y = 0f;
        float radius = Mathf.Max(0f, m_standImpactRadius);
        if (toPlayer.sqrMagnitude > radius * radius)
            return;

        ApplyPlayerKnockback(toPlayer, m_standImpactHorizontalForce, m_standImpactUpwardForce);
    }

    public void TryApplyRushKnockback(Collider other, Vector3 origin, Vector3 fallbackDirection)
    {
        if (other == null || other.gameObject.layer != PhysicsLayers.Player)
            return;

        Vector3 horizontalDirection = other.transform.position - origin;
        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude <= 0.0001f)
            horizontalDirection = fallbackDirection;

        ApplyPlayerKnockback(horizontalDirection, m_rushKnockbackHorizontalForce, m_rushKnockbackUpwardForce);
    }

    // スタブ成功時：スタン値を0に戻し、これ以上スタブできないようにする（1回のスタンにつき1回）。
    // ただしすぐには起き上がらせず、postStabDownDuration の間ダウンを保持する（命中直後の起き上がりが速すぎる対策）。
    // 実際の起き上がり（崩れ終了＋Idle復帰）は保持経過後に TickStaminaBreakTimer が RecoverFromStab で行う。
    private void EndBreakAfterStab()
    {
        //if (m_stamina != null)
            //m_stamina.ResetStamina();

        m_postStabHoldTimer = Mathf.Max(0f, m_settings != null ? m_settings.postStabDownDuration : 0f);
        m_postStabHoldPending = true;
    }

    // スタブ命中後のダウン保持が経過したら起き上がる。崩れを終了して Idle へ戻す。
    private void RecoverFromStab()
    {
        m_postStabHoldPending = false;
        m_staminaBreakEndRequested = true;
        m_stabFinisherActive = false; // 立ち上がったので演出ロック解除
        // sssWind
        EndBreakAnimations();
        ChangeState(BossState.Standing);
    }

    private bool IsBreakOrStandingState()
    {
        return IsBreakState() || m_state == BossState.Standing;
    }

    private bool IsBreakState()
    {
        return m_state == BossState.Stunned || m_state == BossState.Stagger;
    }

    private void UpdateStabReadyEffect()
    {
        bool visible = IsBreakState()
            && !m_stabFinisherActive
            && Time.time >= m_breakAnimationStartedTime + Mathf.Max(0f, m_stabReadyEffectDelayAfterBreak);

        SetStabReadyEffectVisible(visible);
        if (!visible || m_stabReadyEffectRoot == null) return;

        float cycles = Mathf.Max(0f, m_stabReadyEffectBlinkCyclesPerSecond);
        float wave = cycles > 0f
            ? (Mathf.Sin(Time.time * cycles * Mathf.PI * 2f) + 1f) * 0.5f
            : 1f;
        float alpha = Mathf.Lerp(Mathf.Clamp01(m_stabReadyEffectMinAlpha), 1f, wave) * m_stabReadyEffectColor.a;
        Color color = m_stabReadyEffectColor;
        color.a = alpha;

        float scale = 1f + wave * Mathf.Max(0f, m_stabReadyEffectPulseScale);
        if (!m_usePlacedStabReadyEffect)
        {
            m_stabReadyEffectRoot.transform.localPosition = m_stabReadyEffectLocalOffset;
            m_stabReadyEffectRoot.transform.localRotation = Quaternion.Euler(m_stabReadyEffectLocalEulerAngles);
            m_stabReadyEffectBaseLocalScale = m_stabReadyEffectLocalScale;
        }
        m_stabReadyEffectRoot.transform.localScale = Vector3.Scale(m_stabReadyEffectBaseLocalScale, Vector3.one * scale);

        TintStabReadyEffect(color);
        BlinkStabReadyEffectLights(wave);
    }

    private void TintStabReadyEffect(Color color)
    {
        for (int i = 0; i < m_stabReadyEffectParticles.Length; i++)
        {
            ParticleSystem particle = m_stabReadyEffectParticles[i];
            if (particle == null) continue;

            ParticleSystem.MainModule main = particle.main;
            main.startColor = color;
        }

        for (int i = 0; i < m_stabReadyEffectRenderers.Length; i++)
        {
            Renderer effectRenderer = m_stabReadyEffectRenderers[i];
            if (effectRenderer == null) continue;

            Material[] materials = effectRenderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null) continue;

                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", color);
                if (material.HasProperty("_TintColor"))
                    material.SetColor("_TintColor", color);
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", color);
            }
        }
    }

    private void SetStabReadyEffectVisible(bool visible)
    {
        if (visible && m_stabReadyEffectRoot == null)
            BuildStabReadyEffect();

        if (m_stabReadyEffectRoot == null || m_stabReadyEffectRoot.activeSelf == visible)
            return;

        if (visible)
        {
            m_stabReadyEffectRoot.SetActive(true);
            PlayStabReadyEffect();
        }
        else
        {
            StopStabReadyEffect();
            m_stabReadyEffectRoot.transform.localScale = m_stabReadyEffectBaseLocalScale;
            ResetStabReadyEffectLights();
            m_stabReadyEffectRoot.SetActive(false);
        }
    }

    private void BuildStabReadyEffect()
    {
        if (m_stabReadyEffectRoot != null)
            return;

        m_stabReadyEffectRoot = ResolvePlacedStabReadyEffect();
        if (m_stabReadyEffectRoot != null)
        {
            m_usePlacedStabReadyEffect = true;
            m_stabReadyEffectRoot.name = StabReadyEffectName;
            m_stabReadyEffectBaseLocalScale = m_stabReadyEffectRoot.transform.localScale;
            m_stabReadyEffectParticles = m_stabReadyEffectRoot.GetComponentsInChildren<ParticleSystem>(true);
            m_stabReadyEffectRenderers = m_stabReadyEffectRoot.GetComponentsInChildren<Renderer>(true);
            CacheStabReadyEffectLights();
            TintStabReadyEffect(m_stabReadyEffectColor);
            m_stabReadyEffectRoot.SetActive(false);
            return;
        }

        m_stabReadyEffectAnchor = ResolveStabReadyEffectAnchor();
        if (m_stabReadyEffectAnchor == null)
        {
            if (!m_warnedMissingStabReadyEffectAnchor)
            {
                ChannelLogger.LogWarning("EnemyBossA", $"スタブ可能エフェクトの表示先ボーン未検出: {m_stabReadyEffectBonePath}");
                m_warnedMissingStabReadyEffectAnchor = true;
            }
            return;
        }

        if (m_stabReadyEffectPrefab == null)
        {
            if (!m_warnedMissingStabReadyEffectPrefab)
            {
                ChannelLogger.LogWarning("EnemyBossA", "スタブ可能エフェクトPrefab未設定");
                m_warnedMissingStabReadyEffectPrefab = true;
            }
            return;
        }

        m_stabReadyEffectRoot = Instantiate(m_stabReadyEffectPrefab, m_stabReadyEffectAnchor);
        m_usePlacedStabReadyEffect = false;
        m_stabReadyEffectRoot.name = StabReadyEffectName;
        m_stabReadyEffectRoot.transform.SetParent(m_stabReadyEffectAnchor, false);
        m_stabReadyEffectRoot.transform.localPosition = m_stabReadyEffectLocalOffset;
        m_stabReadyEffectRoot.transform.localRotation = Quaternion.Euler(m_stabReadyEffectLocalEulerAngles);
        m_stabReadyEffectRoot.transform.localScale = m_stabReadyEffectLocalScale;
        m_stabReadyEffectBaseLocalScale = m_stabReadyEffectLocalScale;
        m_stabReadyEffectParticles = m_stabReadyEffectRoot.GetComponentsInChildren<ParticleSystem>(true);
        m_stabReadyEffectRenderers = m_stabReadyEffectRoot.GetComponentsInChildren<Renderer>(true);
        CacheStabReadyEffectLights();
        TintStabReadyEffect(m_stabReadyEffectColor);
        m_stabReadyEffectRoot.SetActive(false);
    }

    private void CacheStabReadyEffectLights()
    {
        if (m_stabReadyEffectRoot == null)
        {
            m_stabReadyEffectLights = Array.Empty<Light>();
            m_stabReadyEffectLightBaseIntensities = Array.Empty<float>();
            return;
        }

        m_stabReadyEffectLights = m_stabReadyEffectRoot.GetComponentsInChildren<Light>(true);
        m_stabReadyEffectLightBaseIntensities = new float[m_stabReadyEffectLights.Length];
        for (int i = 0; i < m_stabReadyEffectLights.Length; i++)
        {
            Light effectLight = m_stabReadyEffectLights[i];
            m_stabReadyEffectLightBaseIntensities[i] = effectLight != null ? effectLight.intensity : 0f;
        }
    }

    private void BlinkStabReadyEffectLights(float wave)
    {
        float minMultiplier = Mathf.Clamp01(m_stabReadyEffectMinAlpha);
        for (int i = 0; i < m_stabReadyEffectLights.Length; i++)
        {
            Light effectLight = m_stabReadyEffectLights[i];
            if (effectLight == null) continue;

            float baseIntensity = i < m_stabReadyEffectLightBaseIntensities.Length
                ? m_stabReadyEffectLightBaseIntensities[i]
                : effectLight.intensity;
            effectLight.intensity = baseIntensity * Mathf.Lerp(minMultiplier, 1f, wave);
        }
    }

    private void ResetStabReadyEffectLights()
    {
        for (int i = 0; i < m_stabReadyEffectLights.Length; i++)
        {
            Light effectLight = m_stabReadyEffectLights[i];
            if (effectLight == null || i >= m_stabReadyEffectLightBaseIntensities.Length) continue;

            effectLight.intensity = m_stabReadyEffectLightBaseIntensities[i];
        }
    }

    private void PlayStabReadyEffect()
    {
        if (m_stabReadyEffectRoot != null)
            m_stabReadyEffectBaseLocalScale = m_stabReadyEffectRoot.transform.localScale;

        for (int i = 0; i < m_stabReadyEffectParticles.Length; i++)
        {
            ParticleSystem particle = m_stabReadyEffectParticles[i];
            if (particle == null) continue;

            particle.Play(true);
        }
    }

    private void StopStabReadyEffect()
    {
        for (int i = 0; i < m_stabReadyEffectParticles.Length; i++)
        {
            ParticleSystem particle = m_stabReadyEffectParticles[i];
            if (particle == null) continue;

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private Transform ResolveStabReadyEffectAnchor()
    {
        if (m_stabReadyEffectSceneAnchor != null)
            return m_stabReadyEffectSceneAnchor;

        Transform anchor = null;
        if (!string.IsNullOrEmpty(m_stabReadyEffectBonePath))
        {
            anchor = transform.Find(m_stabReadyEffectBonePath);
            if (anchor == null && m_stabReadyEffectBonePath.Contains("/Oelvis/"))
                anchor = transform.Find(m_stabReadyEffectBonePath.Replace("/Oelvis/", "/Pelvis/"));
            if (anchor == null && m_stabReadyEffectBonePath.Contains("/Pelvis/"))
                anchor = transform.Find(m_stabReadyEffectBonePath.Replace("/Pelvis/", "/Oelvis/"));
        }

        return anchor != null ? anchor : FindChildRecursive(transform, "Body_Tube_1");
    }

    private GameObject ResolvePlacedStabReadyEffect()
    {
        if (m_stabReadyEffectSceneObject != null)
            return m_stabReadyEffectSceneObject;

        Transform placedEffect = FindChildRecursive(transform, StabReadyEffectName);
        return placedEffect != null ? placedEffect.gameObject : null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null) return null;
        if (root.name == childName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null) return found;
        }

        return null;
    }

    // === ヘルパ ===

    private float DistanceToPlayer()
    {
        return Vector3.Distance(transform.position, m_player.position);
    }

    private void ApplyPlayerKnockback(Vector3 horizontalDirection, float horizontalForce, float upwardForce)
    {
        if (m_player == null)
            return;

        if (!m_player.TryGetComponent(out Entity playerEntity))
            return;

        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude <= 0.0001f)
            horizontalDirection = transform.forward;
        else
            horizontalDirection.Normalize();

        playerEntity.externalVelocity +=
            horizontalDirection * Mathf.Max(0f, horizontalForce)
            + Vector3.up * Mathf.Max(0f, upwardForce);
    }

    private void FacePlayer(float dt, float deadZoneDeg = 0f)
    {
        if (m_stabFinisherFacingLock) return; // スタブ演出中(開始〜終了)はプレイヤーを追尾して回頭しない（命中後の立ち上がり中も維持）
        Vector3 look = m_player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude <= 0.0001f)
            return;

        if (m_attackMissTurnTimer > 0f)
        {
            FacePlayerAfterAttackMiss(look.normalized, dt);
            return;
        }

        m_boss.FaceToward(look.normalized, dt, deadZoneDeg);
    }

    private bool FacePlayerForMissileStart(float dt)
    {
        if (m_stabFinisherFacingLock)
            return true;

        Vector3 look = m_player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude <= 0.0001f)
            return true;

        Vector3 direction = look.normalized;

        if (m_attackMissTurnTimer > 0f)
        {
            FacePlayerAfterAttackMiss(direction, dt);
            return false;
        }

        m_boss.FaceToward(direction, dt);
        return IsFacingDirection(direction, Mathf.Max(0f, m_missileFaceReadyAngleDeg));
    }

    private bool IsFacingDirection(Vector3 direction, float angleDeg)
    {
        Vector3 currentForward = transform.forward;
        currentForward.y = 0f;
        direction.y = 0f;

        if (currentForward.sqrMagnitude <= 0.0001f || direction.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Angle(currentForward.normalized, direction.normalized) <= angleDeg;
    }

    private bool DidArmAttackHit()
    {
        return m_attackHitboxes != null && m_attackHitboxes.ArmHitThisAttack;
    }

    private void BeginAttackMissTurn()
    {
        if (m_player == null)
            return;

        Vector3 look = m_player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude <= 0.0001f)
            return;

        Vector3 currentForward = transform.forward;
        currentForward.y = 0f;
        if (currentForward.sqrMagnitude <= 0.0001f)
            currentForward = transform.forward;

        float angle = Vector3.Angle(currentForward.normalized, look.normalized);
        if (angle <= Mathf.Max(0f, m_settings.faceDeadZoneDeg))
            return;

        m_attackMissTurnTimer = AttackMissTurnDuration;
        m_attackMissTurnSpeedDegPerSec = angle / AttackMissTurnDuration;
    }

    private void FacePlayerAfterAttackMiss(Vector3 direction, float dt)
    {
        m_attackMissTurnTimer = Mathf.Max(0f, m_attackMissTurnTimer - dt);

        Quaternion targetRotation = Quaternion.LookRotation(direction, transform.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            m_attackMissTurnSpeedDegPerSec * dt
        );
    }
}

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
internal static class EnemyBossAIStabReadyEffectEditorBootstrap
{
    private static bool s_applyQueued;

    static EnemyBossAIStabReadyEffectEditorBootstrap()
    {
        UnityEditor.EditorApplication.delayCall += QueueApplyToLoadedScenes;
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (_, _) => QueueApplyToLoadedScenes();
        UnityEditor.EditorApplication.hierarchyChanged += QueueApplyToLoadedScenes;
    }

    [UnityEditor.MenuItem("Tools/MagnetRush/Apply Stab Ready Magnet Effect To Loaded Scenes")]
    private static void QueueApplyToLoadedScenes()
    {
        if (s_applyQueued || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        s_applyQueued = true;
        UnityEditor.EditorApplication.delayCall += ApplyToLoadedScenes;
    }

    private static void ApplyToLoadedScenes()
    {
        s_applyQueued = false;
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnemyBossAI[] bosses = UnityEngine.Resources.FindObjectsOfTypeAll<EnemyBossAI>();
        for (int i = 0; i < bosses.Length; i++)
        {
            EnemyBossAI boss = bosses[i];
            if (boss == null) continue;
            if (UnityEditor.EditorUtility.IsPersistent(boss)) continue;
            if (!boss.gameObject.scene.IsValid() || !boss.gameObject.scene.isLoaded) continue;

            boss.EnsureStabReadyEffectInEditor();
        }
    }
}
#endif
