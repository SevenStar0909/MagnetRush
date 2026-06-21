using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ボスAI。6状態FSM (Idle/Chase/AttackStance/AttackMotion/Stunned/Stagger)。
/// NavMeshAgent は経路計算のみ (updatePosition=false, updateRotation=false)、実移動は EnemyBossBase.AccelerateToward 経由で EntityController が処理する。
/// AI → Animator: 状態読み取り (IsAttacking/IsInAttackMotion/IsStunned) のみ。書き込みは Animator が自分で行う。
/// Animator → AI: AnimEvent 経由の OnAttackFinished/OnStunEnd コールバック。
/// 依存: EnemyBossBase, NavMeshAgent, EnemyBossBaseA_Animator
/// </summary>
[RequireComponent(typeof(EnemyBossBase))]
public class EnemyBossAI : MonoBehaviour, IStabReceiver, IDamageGuard
{
    public enum BossState { Idle, AttackStance, AttackMotion, Rush, Missile, Stunned, Stagger, Standing }

    [Header("References")]
    [SerializeField] private EnemyBossBaseA_Animator m_animator;

    [Tooltip("スタブの突き刺し目標。頭ボーン下に置いた空オブジェクト（StabAnchor）をアサインする")]
    [SerializeField] private Transform m_stabAnchor;

    [Tooltip("このボス専用のスタブ演出設定（数値＋カメラTimeline）。未設定ならプレイヤー共通設定を使う")]
    [SerializeField] private StabFinisherSettings m_stabFinisherSettings;

    [Header("Missile")]
    [Tooltip("生成するミサイルPrefab")]
    [SerializeField] private EnemyMissile m_missilePrefab;

    [Tooltip("ミサイル生成位置。未設定ならこのオブジェクト位置を使用")]
    [SerializeField] private Transform[] m_missileSpawnPoints;

    [Tooltip("各生成位置のローカルオフセット。m_missileSpawnPoints と同じ順番で指定")]
    [SerializeField] private Vector3[] m_missileSpawnOffsets;

    [Tooltip("ONでミサイルを 上2発→左上/右上1発ずつ の順に撃つ。OFFなら従来通り前方へ撃つ")]
    [SerializeField] private bool m_fireLobMissiles = true;

    [Tooltip("分岐後の上ミサイル角度(度)。前方から上へ傾ける。90で真上")]
    [SerializeField] private float m_missileLobAngle = 45f;

    [Tooltip("発射口から控えめな角度で出てから、上/左右上の形に分岐し始めるまでの時間(秒)")]
    [SerializeField] private float m_missileLobRiseTime = 0.45f;

    [Tooltip("左右上ミサイルの横方向への開き角度(度)。0なら真上、90なら真横")]
    [SerializeField] private float m_missileSideLobAngle = 35f;

    [Tooltip("発射口から出る瞬間だけ使う控えめな上向き角度(度)。分岐前の見た目用")]
    [SerializeField] private float m_missileLaunchAngle = 12f;

    [Tooltip("弧の頂点の高さ。大きいほどミサイル同士が空中で離れやすい")]
    [SerializeField] private float m_missileArcHeight = 8f;

    [Tooltip("分岐方向へ膨らませる距離。大きいほど横/前方に広い弧を描く")]
    [SerializeField] private float m_missileArcSpreadDistance = 8f;

    [Tooltip("上2発の弧を左右にずらす距離。0で同じライン")]
    [SerializeField] private float m_missileArcLaneSpacing = 3f;

    [Tooltip("発射してからこの秒数はミサイルがボス本体に当たらない(発射直後の自爆防止)。経過後はボスにも当たる=磁力で撃ち返せる。0で即当たる")]
    [SerializeField] private float m_missileCollisionGrace = 3f;

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

    // 次の OnMissileFireEvent で左右上ミサイルを撃つか。アニメの2イベントで 上→左右上 と切り替える
    private bool m_nextMissileIsSideLob;

    [Header("Debug")]
    [SerializeField] private bool m_logStateChange = true;

    private EnemyBossBase m_boss;
    private NavMeshAgent m_agent;
    private Transform m_player;
    private EnemyBossSettings m_settings;
    private Stamina m_stamina;
    private Health m_health;

    // ボス本体（各ボーン）の Hitbox。物理オブジェクト接触でスタンゲージを溜める（機構1）。
    private Hitbox[] m_bodyHitboxes;
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

    public event Action OnStabHitSucceeded;   // スタブが成功したときに発火

    public BossState State => m_state;
    public bool IsInvincibleState => m_state == BossState.Standing;
    public bool CanTakeDamage(HitData hit) => !IsInvincibleState;

    public EnemyBossSettings Settings => m_settings;

    public Stamina Stamina => m_stamina;

    void Awake()
    {
        m_boss = GetComponent<EnemyBossBase>();
        m_agent = GetComponent<NavMeshAgent>();
        m_stamina = GetComponent<Stamina>();
        m_health = GetComponent<Health>();

        // 右手の ArmStunHitbox は Hitbox 派生ではないので含まれない（＝カウンター経路と分離される）。
        m_bodyHitboxes = GetComponentsInChildren<Hitbox>(true);

        if (m_animator == null)
            m_animator = GetComponentInChildren<EnemyBossBaseA_Animator>();

        if (m_agent != null)
        {
            // Boss AI no longer uses NavMeshAgent; keep the component inert if it remains on old prefabs.
            m_agent.enabled = false;
            m_agent = null;
        }
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
    }

    void Start()
    {
        m_player = m_boss.Player;
        m_settings = m_boss.StatusData;

        if (m_agent != null && m_settings != null)
        {
            m_agent.speed = m_settings.moveSpeed;
            m_agent.stoppingDistance = m_settings.stopDistance;
            m_agent.acceleration = m_settings.acceleration;
        }

        if (m_animator == null)
            ChannelLogger.LogError("Enemy", $"[EnemyBossAI] {name}: EnemyBossBaseA_Animator 未設定");
    }

    void Update()
    {
        if (m_player == null || m_settings == null || m_animator == null)
        { ChannelLogger.LogGuardReturn("Enemy", "プレイヤー/Settings/Agent/Animator未取得"); return; }

        float dt = Time.deltaTime;
        m_cooldownTimer = Mathf.Max(0f, m_cooldownTimer - dt);

        TickStunEntry(); // Stunアニメーションの開始を検知してStunned状態に入り、回復タイマーを開始する
        TickStaggerEntry(); // Staggerアニメーションの開始を検知してStagger状態に入り、回復タイマーを開始する
        TickStaminaBreakTimer(dt); // Stunned/Staggered 共通の回復タイマー

        TryRecoverAgent();
        SyncAgentToBody();

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

    // スタンゲージが満タン（＝Stamina 0）になった時に Stamina.OnBreak から呼ばれる。
    // よろけ（Stagger）を1回だけ発火する。OnBreak はゲージが 0 に落ちた瞬間に1度だけ発火するのでループしない。
    // よろけ中もスタブ可（CanReceiveStab が Stagger を含む）。スタン（振り上げカウンター=ArmStunHitbox）でも同様にスタブできる。
    private void HandleStaminaBreak()
    {
        if (m_animator == null) return;
        if (IsInvincibleState) return;

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

        // 崩れ（Stun/Stagger）中だけ接地スナップを切る。崩れアニメで胴体が沈む間の下押しを止め、
        // 本体が地面にめり込むのを防ぐ。崩れを抜けた瞬間（Idle 等）に false へ戻して通常の接地に復帰する。
        if (m_boss != null)
            m_boss.SuppressGroundSnap = next == BossState.Stunned || next == BossState.Stagger || next == BossState.Standing;

        TryEmitInterruptShockWave(prev, next);

        if (next == BossState.Rush)
        {
            m_rushTargetPosition = m_player.position;
            m_boss.lateralVelocity = Vector3.zero;
            m_rushHasStarted = false; // 入り transition フェーズへ。Animator が IsInRush=true に入った時点で true 化
            m_rushMovementStopped = false;
            m_rushEndRequested = false;
            m_rushKeepTimer = 0f;
        }

        if (next == BossState.Idle)
        {
            ClearStaminaFlags();
            // Idle に戻る時は Wind/Dust を必ず止める。Rush の DisableWindEffectEvent が
            // 中断（被弾→Stagger 等）で発火しないまま Idle へ戻ると Wind が出続けるため、保険として停止する。
            // Wind/Dust は Rush/Stun 中しか点かないので Idle で消すのは常に正しい。
            if (m_animator != null)
            {
                m_animator.DisableWindEffectEvent();
                m_animator.DisableDustEffectEvent();
            }
        }

        // ミサイル攻撃の入り口でトグルをリセット（必ず 1発目=上波 から始める）
        if (next == BossState.Missile)
            m_nextMissileIsSideLob = false;

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

        // ここではスタン値（Stamina）をリセットしない。スタン値はスタブを当てるまで減らない仕様。
        // リセットは OnStabHit（スタブ成功時）の EndBreakAfterStab でのみ行う。
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
            else
                m_animator.TriggerMissile();
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
        if (m_animator.IsIdle) { ChangeState(BossState.Idle); return; }

        m_boss.SlowDown(dt);
        FacePlayer(dt, m_settings.faceDeadZoneDeg);
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

    /// <summary>Missile 発射イベント専用。AnimationEvent から呼ばれ、各 SpawnPoint からミサイルを生成する。</summary>
    public void OnMissileFireEvent()
    {
        if (m_missilePrefab == null)
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "EnemyBossAI.m_missilePrefab が未アサインです");
            return;
        }

        // アニメの2イベントで 1回目=上2発 / 2回目=左上1発+右上1発 と撃つ（計4発）
        MissileWavePattern pattern = m_fireLobMissiles
            ? (m_nextMissileIsSideLob ? MissileWavePattern.LeftRightUp : MissileWavePattern.Up)
            : MissileWavePattern.Forward;

        FireMissileWave(pattern);
        m_nextMissileIsSideLob = !m_nextMissileIsSideLob;
        m_animator.TriggerMissileFinished();
    }

    private enum MissileWavePattern
    {
        Forward,
        Up,
        LeftRightUp
    }

    /// <summary>1波分。全発射点からパターンに応じた初期方向で1発ずつ撃つ。</summary>
    private void FireMissileWave(MissileWavePattern pattern)
    {
        if (m_missilePrefab == null) return;

        if (!HasAnyMissileSpawnPoint())
        {
            SpawnMissileAt(this.transform, Vector3.zero, pattern, 0, pattern == MissileWavePattern.LeftRightUp ? 2 : 1);
            if (pattern == MissileWavePattern.LeftRightUp)
                SpawnMissileAt(this.transform, Vector3.zero, pattern, 1, 2);
            return;
        }

        int spawnCount = m_missileSpawnPoints.Length;
        for (int i = 0; i < m_missileSpawnPoints.Length; i++)
        {
            Transform spawnPoint = m_missileSpawnPoints[i];
            if (spawnPoint == null) continue;

            Vector3 offset = Vector3.zero;
            if (m_missileSpawnOffsets != null && i < m_missileSpawnOffsets.Length)
                offset = m_missileSpawnOffsets[i];

            SpawnMissileAt(spawnPoint, offset, pattern, i, spawnCount);
        }
    }

    private bool HasAnyMissileSpawnPoint()
    {
        if (m_missileSpawnPoints == null || m_missileSpawnPoints.Length == 0)
            return false;

        for (int i = 0; i < m_missileSpawnPoints.Length; i++)
        {
            if (m_missileSpawnPoints[i] != null)
                return true;
        }

        return false;
    }

    private Vector3 ComputeMissileDirection(MissileWavePattern pattern, int spawnIndex)
    {
        switch (pattern)
        {
            case MissileWavePattern.Up:
                return ComputeUpDirection();

            case MissileWavePattern.LeftRightUp:
                return ComputeSideUpDirection(spawnIndex);

            default:
                return transform.forward;
        }
    }

    /// <summary>上ミサイルの初期方向。ボス正面を上へ m_missileLobAngle 度だけ傾ける。</summary>
    private Vector3 ComputeUpDirection()
    {
        Vector3 fwd = transform.forward;
        if (fwd.sqrMagnitude <= 0.0001f) fwd = Vector3.forward;
        return Vector3.RotateTowards(fwd, Vector3.up, m_missileLobAngle * Mathf.Deg2Rad, 0f).normalized;
    }

    /// <summary>左上/右上ミサイルの初期方向。</summary>
    private Vector3 ComputeSideUpDirection(int spawnIndex)
    {
        float sideSign = spawnIndex % 2 == 0 ? -1f : 1f;
        Vector3 side = transform.right * sideSign;
        float angle = Mathf.Clamp(m_missileSideLobAngle, 0f, 89f) * Mathf.Deg2Rad;
        Vector3 direction = Vector3.up * Mathf.Cos(angle) + side * Mathf.Sin(angle);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
    }

    private Vector3 ComputeLaunchDirection(Transform spawnPoint)
    {
        Vector3 bossForward = transform.forward;
        if (bossForward.sqrMagnitude <= 0.0001f)
            bossForward = Vector3.forward;

        Vector3 fwd = spawnPoint != null ? spawnPoint.forward : bossForward;
        if (fwd.sqrMagnitude <= 0.0001f)
            fwd = bossForward;

        // 左右でミラーされた発射口は forward が後ろ向きになることがある。
        // その場合だけボス正面を使い、発射直後に背面へ飛ぶ見た目を防ぐ。
        if (Vector3.Dot(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.ProjectOnPlane(bossForward, Vector3.up)) < 0f)
            fwd = bossForward;

        float launchAngle = Mathf.Clamp(m_missileLaunchAngle, 0f, 89f) * Mathf.Deg2Rad;
        return Vector3.RotateTowards(fwd.normalized, Vector3.up, launchAngle, 0f).normalized;
    }

    private float ComputeArcLaneOffset(MissileWavePattern pattern, int spawnIndex, int spawnCount)
    {
        if (pattern != MissileWavePattern.Up || spawnCount <= 1)
            return 0f;

        float center = (spawnCount - 1) * 0.5f;
        return (spawnIndex - center) * m_missileArcLaneSpacing;
    }

    private void SpawnMissileAt(Transform spawnPoint, Vector3 localOffset, MissileWavePattern pattern, int spawnIndex, int spawnCount)
    {
        Vector3 spawnPos = spawnPoint.position + spawnPoint.TransformDirection(localOffset);
        Vector3 formationDirection = ComputeMissileDirection(pattern, spawnIndex);
        bool useArcFlight = pattern != MissileWavePattern.Forward;
        Vector3 launchDirection = useArcFlight ? ComputeLaunchDirection(spawnPoint) : formationDirection;

        if (launchDirection.sqrMagnitude <= 0.0001f)
            launchDirection = spawnPoint.forward;
        launchDirection = launchDirection.normalized;

        Quaternion rotation = Quaternion.LookRotation(launchDirection, Vector3.up);
        EnemyMissile missile = Instantiate(m_missilePrefab, spawnPos, rotation);
        if (useArcFlight)
        {
            float laneOffset = ComputeArcLaneOffset(pattern, spawnIndex, spawnCount);
            missile.InitializeArc(
                m_player,
                launchDirection,
                formationDirection,
                m_missileLobRiseTime,
                m_missileArcHeight,
                m_missileArcSpreadDistance,
                laneOffset
            );
        }
        else
        {
            missile.Initialize(m_player, launchDirection, -1f);
        }
        // ミサイルは PhysicsObject なので発射元ボスの Pushbox 等と衝突してしまう。spawn 即爆発・自傷を防ぐ。
        missile.IgnoreCollisionsWith(gameObject, m_missileCollisionGrace);
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

    // スタブ成功時：スタン値を0に戻し、これ以上スタブできないようにする（1回のスタンにつき1回）。
    // ただしすぐには起き上がらせず、postStabDownDuration の間ダウンを保持する（命中直後の起き上がりが速すぎる対策）。
    // 実際の起き上がり（崩れ終了＋Idle復帰）は保持経過後に TickStaminaBreakTimer が RecoverFromStab で行う。
    private void EndBreakAfterStab()
    {
        if (m_stamina != null)
            m_stamina.ResetStamina();

        m_postStabHoldTimer = Mathf.Max(0f, m_settings != null ? m_settings.postStabDownDuration : 0f);
        m_postStabHoldPending = true;
    }

    // スタブ命中後のダウン保持が経過したら起き上がる。崩れを終了して Idle へ戻す。
    private void RecoverFromStab()
    {
        m_postStabHoldPending = false;
        m_staminaBreakEndRequested = true;
        m_stabFinisherActive = false; // 立ち上がったので演出ロック解除
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

    // === ヘルパ ===

    private float DistanceToPlayer()
    {
        return Vector3.Distance(transform.position, m_player.position);
    }

    private void FacePlayer(float dt, float deadZoneDeg = 0f)
    {
        if (m_stabFinisherFacingLock) return; // スタブ演出中(開始〜終了)はプレイヤーを追尾して回頭しない（命中後の立ち上がり中も維持）
        Vector3 look = m_player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            m_boss.FaceToward(look.normalized, dt, deadZoneDeg);
    }

    private void SyncAgentToBody()
    {
        if (m_agent == null) return;
        if (!m_agent.enabled || !m_agent.isOnNavMesh) return;
        // EntityController が動かした位置を NavMeshAgent に同期し、内部シミュレーションを抑制
        m_agent.nextPosition = transform.position;
        m_agent.velocity = Vector3.zero;
    }

    private void TryRecoverAgent()
    {
        if (m_agent == null) return;
        if (m_agent.enabled && m_agent.isOnNavMesh) { ChannelLogger.LogGuardReturn("EnemyBossA", "Agent既に有効"); return; }

        const float sampleRadius = 20;

        Vector3 sourcePosition = transform.position;
        if (!NavMesh.SamplePosition(sourcePosition, out var hit, sampleRadius, NavMesh.AllAreas))
        {
            if (m_player != null)
            {
                sourcePosition = m_player.position;
                if (!NavMesh.SamplePosition(sourcePosition, out hit, sampleRadius, NavMesh.AllAreas))
                {
                    ChannelLogger.LogGuardReturn("EnemyBossA",
                        $"NavMeshサンプル失敗 pos={transform.position} player={m_player.position} radius={sampleRadius}");
                    return;
                }
            }
            else
            {
                ChannelLogger.LogGuardReturn("EnemyBossA",
                    $"NavMeshサンプル失敗 pos={transform.position} radius={sampleRadius}");
                return;
            }
        }

        // 突進終了地点。Agent は NavMesh 上でないと有効化できないので一旦サンプル点へ移すが、
        // 最後にここへ戻す。これをしないとボスが NavMesh 最寄り点へ瞬間移動する（突進終了時のワープの原因）。
        Vector3 endPosition = transform.position;

        if (m_agent.enabled)
            m_agent.enabled = false;

        transform.position = hit.position;
        m_agent.enabled = true;

        if (m_agent.isOnNavMesh)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
            m_agent.velocity = Vector3.zero;

            // updatePosition=false なので Agent は内部位置(NavMesh上)を保ったまま、ボスの transform だけ
            // 突進終了地点へ戻す。以降は通常の追従移動で滑らかに NavMesh 上へ戻る（瞬間移動しない）。
            transform.position = endPosition;
            m_agent.nextPosition = endPosition;

            ChannelLogger.Log("EnemyBossA", $"Agent復帰成功 pos={transform.position} hit={hit.position}");
        }
        else
        {
            // 復帰できなかった場合もボスは飛ばさず元の位置へ戻す
            transform.position = endPosition;
            ChannelLogger.LogWarning("EnemyBossA", $"Agent復帰失敗 pos={transform.position}");
        }
    }

}
