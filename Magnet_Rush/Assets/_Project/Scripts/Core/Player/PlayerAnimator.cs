using UnityEngine;

/// <summary>
/// プレイヤーの Animator を駆動する専用コンポーネント。
/// PlayerEvents を購読して射撃系 Trigger を、LateUpdate で連続値を、
/// ステート変化で State(Int)+OnStateChanged(Trigger) を更新する。
/// Animator の直接操作はこのクラスのみに集約し、他からは触らない。
/// 依存: PlayerEvents, PlayerInputHandler, PlayerStateManager, Player
/// 設計: 駆動対象の Animator は FBX 子オブジェクトに付くため、m_animator は Inspector で明示アサイン必須。
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("駆動対象の Animator（通常は FBX モデル子オブジェクトの Animator）。Inspector で必ずアサインする。")]
    [SerializeField] private Animator m_animator;

    [Tooltip("イベントハブ。未設定なら親の GetComponentInParent<PlayerEvents>()")]
    [SerializeField] private PlayerEvents m_events;

    [Tooltip("入力ハンドラ。未設定なら親の GetComponentInParent<PlayerInputHandler>()")]
    [SerializeField] private PlayerInputHandler m_input;

    [Tooltip("ステートマネージャ。未設定なら親の GetComponentInParent<PlayerStateManager>()")]
    [SerializeField] private PlayerStateManager m_states;

    [Tooltip("Player 本体。未設定なら親の GetComponentInParent<Player>()")]
    [SerializeField] private Player m_player;

    [Tooltip("エイム Ability。未設定なら親の GetComponentInParent<AimAbility>()")]
    [SerializeField] private AimAbility m_aim;

    [Tooltip("スタブ Ability。未設定なら親の GetComponentInParent<StabAbility>()")]
    [SerializeField] private StabAbility m_stab;

    [Tooltip("体力。被弾アニメのトリガーに使う。未設定なら親の GetComponentInParent<Health>()")]
    [SerializeField] private Health m_health;

    [Header("Animator Parameter Names (Inspector 単一箇所管理)")]
    [SerializeField] private string m_stateName = "State";
    [SerializeField] private string m_lastStateName = "LastState";
    [SerializeField] private string m_onStateChangedName = "OnStateChanged";
    [SerializeField] private string m_moveSpeedName = "MoveSpeed";
    [SerializeField] private string m_moveInputXName = "MoveInputX";
    [SerializeField] private string m_moveInputZName = "MoveInputZ";
    [SerializeField] private string m_isAimingName = "IsAiming";
    [SerializeField] private string m_isGroundedName = "IsGrounded";
    [SerializeField] private string m_shootName = "Shoot";
    [SerializeField] private string m_reloadName = "Reload";
    [SerializeField] private string m_verticalSpeedName = "VerticalSpeed";
    [SerializeField] private string m_stabName = "Stab";
    [SerializeField] private string m_hitName = "Hit";
    [SerializeField] private string m_hitUpperName = "HitUpper";

    [Header("Layer Names (Inspector 単一箇所管理)")]
    [Tooltip("エイム時に有効化する上半身 Layer 名。Animator Controller のレイヤー名と一致させる。")]
    [SerializeField] private string m_aimLayerName = "Upper Body";
    [Tooltip("Aim 切替の補間時間 (秒)。短いほどパッと、長いほど滑らかに切り替わる。")]
    [SerializeField] private float m_aimLayerFadeTime = 0.15f;
    private int m_aimLayerIndex = -1;

    [Tooltip("射撃時に上半身レイヤーを上げ続ける時間 (秒)。移動しながら撃つとき、下半身は歩いたまま上半身だけ撃たせるため。")]
    [SerializeField] private float m_shootUpperBodyHold = 0.6f;
    private float m_shootHoldTimer;

    [Tooltip("空中被弾時に上半身レイヤーを上げ続ける時間 (秒)。空中姿勢を保ったまま上半身だけ食らいモーションを重ねるため。")]
    [SerializeField] private float m_hitUpperBodyHold = 0.6f;
    private float m_hitHoldTimer;

    /// <summary>
    /// State 型 → Animator の State Int 値への固定マッピング。
    /// 新 State を Animator と連動させたい場合はここに追加し、PlayerStateIndex enum にも対応値を定義。
    /// 未登録型は -1 を返す（Animator 側では遷移条件に合致せず無視される）。
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<System.Type, int> s_stateTypeToIndex
        = new System.Collections.Generic.Dictionary<System.Type, int>
    {
        { typeof(IdlePlayerState), (int)PlayerStateIndex.Idle       },
        { typeof(MovePlayerState), (int)PlayerStateIndex.Move       },
        { typeof(DiePlayerState),  (int)PlayerStateIndex.Die        },
        { typeof(AimPlayerState),  (int)PlayerStateIndex.Aim        },
        { typeof(FallPlayerState), (int)PlayerStateIndex.Fall       },
        { typeof(StabPlayerState), (int)PlayerStateIndex.StabAttack },
        // 演出ステートはひとまず StabAttack(5) として提示し、パイル(突き刺し)を再生させる。
        // ジャンプ表現は後段でフェーズ別 State 駆動に差し替える。
        { typeof(BossStabFinisherState), (int)PlayerStateIndex.StabAttack },
    };

    private static int GetStateIndex(System.Type type)
    {
        if (type == null) return -1;
        return s_stateTypeToIndex.TryGetValue(type, out var idx) ? idx : -1;
    }

    private int m_hState;
    private int m_hLastState;
    private int m_hOnStateChanged;
    private int m_hMoveSpeed;
    private int m_hMoveInputX;
    private int m_hMoveInputZ;
    private int m_hIsAiming;
    private int m_hIsGrounded;
    private int m_hShoot;
    private int m_hReload;
    private int m_hVerticalSpeed;
    private int m_hStab;
    private int m_hHit;
    private int m_hHitUpper;
    private int m_lastFinisherPhaseIndex = int.MinValue;

    void Awake()
    {
        if (m_events   == null) m_events   = GetComponentInParent<PlayerEvents>();
        if (m_input    == null) m_input    = GetComponentInParent<PlayerInputHandler>();
        if (m_states   == null) m_states   = GetComponentInParent<PlayerStateManager>();
        if (m_player   == null) m_player   = GetComponentInParent<Player>();
        if (m_aim      == null) m_aim      = GetComponentInParent<AimAbility>();
        if (m_stab     == null) m_stab     = GetComponentInParent<StabAbility>();
        if (m_health   == null) m_health   = GetComponentInParent<Health>();

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("Game", "PlayerAnimator.m_animator が未アサイン（Inspectorで FBX 子の Animator を割り当ててください）");
            enabled = false;
        }
    }

    void Start()
    {
        m_hState           = Animator.StringToHash(m_stateName);
        m_hLastState       = Animator.StringToHash(m_lastStateName);
        m_hOnStateChanged  = Animator.StringToHash(m_onStateChangedName);
        m_hMoveSpeed       = Animator.StringToHash(m_moveSpeedName);
        m_hMoveInputX      = Animator.StringToHash(m_moveInputXName);
        m_hMoveInputZ      = Animator.StringToHash(m_moveInputZName);
        m_hIsAiming        = Animator.StringToHash(m_isAimingName);
        m_hIsGrounded      = Animator.StringToHash(m_isGroundedName);
        m_hShoot           = Animator.StringToHash(m_shootName);
        m_hReload          = Animator.StringToHash(m_reloadName);
        m_hVerticalSpeed   = Animator.StringToHash(m_verticalSpeedName);
        m_hStab            = Animator.StringToHash(m_stabName);
        m_hHit             = Animator.StringToHash(m_hitName);
        m_hHitUpper        = Animator.StringToHash(m_hitUpperName);

        ValidateAnimatorParameters();
        StartCoroutine(ValidateStateOrderDelayed());

        if (m_animator != null && !string.IsNullOrEmpty(m_aimLayerName))
        {
            m_aimLayerIndex = m_animator.GetLayerIndex(m_aimLayerName);
        }
    }

    private System.Collections.IEnumerator ValidateStateOrderDelayed()
    {
        yield return null;
        ValidateStateOrder();
    }

    /// <summary>
    /// Animator Controller に必要なパラメータ名が全て定義されているか検証する。
    /// 欠落していれば LogError でガード（silent SetFloat/SetBool を防ぐ）。
    /// Controller 未割当時はスキップ（メンバーが後からアサインする前提）。
    /// </summary>
    private void ValidateAnimatorParameters()
    {
        if (m_animator == null || m_animator.runtimeAnimatorController == null) return;

        var expected = new (string name, string purpose)[]
        {
            (m_stateName,           "State (Int)"),
            (m_lastStateName,       "LastState (Int)"),
            (m_onStateChangedName,  "OnStateChanged (Trigger)"),
            (m_moveSpeedName,       "MoveSpeed (Float)"),
            (m_moveInputXName,      "MoveInputX (Float)"),
            (m_moveInputZName,      "MoveInputZ (Float)"),
            (m_isAimingName,        "IsAiming (Bool)"),
            (m_isGroundedName,      "IsGrounded (Bool)"),
            (m_shootName,           "Shoot (Trigger)"),
            (m_reloadName,          "Reload (Trigger)"),
            (m_verticalSpeedName,   "VerticalSpeed (Float)"),
            (m_stabName,            "Stab (Trigger)"),
            (m_hitName,             "Hit (Trigger)"),
            (m_hitUpperName,        "HitUpper (Trigger)"),
        };

        var existing = new System.Collections.Generic.HashSet<string>();
        foreach (var p in m_animator.parameters)
            existing.Add(p.name);

        foreach (var (name, purpose) in expected)
        {
            if (!existing.Contains(name))
                Debug.LogError(
                    $"[PlayerAnimator] Animator パラメータ '{name}' ({purpose}) が Controller に定義されていません。" +
                    "Inspector の Animator Parameter Names 欄か Animator Controller の Parameters タブを確認してください。",
                    this);
        }
    }

    /// <summary>
    /// PlayerStateManager.states に登録されている State が s_stateTypeToIndex のエントリを
    /// 全て持っているか検証する。登録漏れがあれば LogError。
    /// （Inspector 順と enum 値の一致までは検証しない。State Int は enum で固定されているので Inspector 順非依存）
    /// </summary>
    private void ValidateStateOrder()
    {
        if (m_states == null) return;

        foreach (var kv in s_stateTypeToIndex)
        {
            if (!m_states.ContainsStateOfType(kv.Key))
                Debug.LogError(
                    $"[PlayerAnimator] Type '{kv.Key.Name}' (expected Int = {kv.Value}) が " +
                    "PlayerStateManager.states に登録されていません。Inspector で追加するか、" +
                    "s_stateTypeToIndex から該当エントリを削除してください。",
                    this);
        }
    }

    void OnEnable()
    {
        if (m_events != null)
        {
            m_events.onShoot.AddListener(HandleShoot);
            m_events.onReload.AddListener(HandleReload);
            m_events.onStab.AddListener(HandleStab);
        }
        if (m_states != null)
        {
            m_states.OnStateChanged += HandleStateChange;
        }
        if (m_health != null)
        {
            m_health.OnDamage += HandleDamage;
        }
    }

    void OnDisable()
    {
        if (m_events != null)
        {
            m_events.onShoot.RemoveListener(HandleShoot);
            m_events.onReload.RemoveListener(HandleReload);
            m_events.onStab.RemoveListener(HandleStab);
        }
        if (m_states != null)
        {
            m_states.OnStateChanged -= HandleStateChange;
        }
        if (m_health != null)
        {
            m_health.OnDamage -= HandleDamage;
        }
    }

    void LateUpdate()
    {
        if (m_animator == null) return;

        if (m_player != null)
        {
            m_animator.SetFloat(m_hMoveSpeed, m_player.lateralVelocity.magnitude);

            // 物理 IsGrounded は externalVelocity (磁力等) による持ち上がりを検知しないため、
            // 上向き外力が SnapForce を超えるときは Animator 上「離地」扱いにして Airborne 層へ遷移させる。
            float snapForce = m_player.Settings != null ? m_player.Settings.snapForce : 0f;
            bool airborneByExternal = m_player.externalVelocity.y > snapForce;
            m_animator.SetBool(m_hIsGrounded, m_player.IsGrounded && !airborneByExternal);
        }

        if (m_input != null)
        {
            var mv = m_input.MoveInput;
            m_animator.SetFloat(m_hMoveInputX, mv.x);
            m_animator.SetFloat(m_hMoveInputZ, mv.y);
            // 接地中は velocity.y が -SnapForce 固定なので externalVelocity.y を加算して
            // 実効的な垂直速度を Animator に渡す (Jump/Fall サブ遷移を磁力上昇でも駆動する)。
            m_animator.SetFloat(m_hVerticalSpeed, m_player.velocity.y + m_player.externalVelocity.y);
        }

        // 演出ステート: フェーズに応じて State Int / IsGrounded を上書きし、既存の AnyState 遷移
        // (State==N + OnStateChanged) で 接近=Idle → 跳び上がり=Fall → 突き下ろし=StabAttack(パイル) を順に再生する。
        if (m_states != null && m_states.current is BossStabFinisherState finisher)
        {
            m_animator.SetBool(m_hIsGrounded, !finisher.IsAirbornePhase);
            int phaseIdx = finisher.AnimatorPhaseIndex;
            if (phaseIdx != m_lastFinisherPhaseIndex)
            {
                m_lastFinisherPhaseIndex = phaseIdx;
                m_animator.SetInteger(m_hState, phaseIdx);
                m_animator.SetTrigger(m_hOnStateChanged);
            }
        }
        else
        {
            m_lastFinisherPhaseIndex = int.MinValue;
        }

        bool aiming = m_aim != null && m_aim.IsAiming;
        if (m_aim != null)
            m_animator.SetBool(m_hIsAiming, aiming);

        // 射撃直後は一定時間だけ上半身レイヤーを上げてホールドする。
        // これで移動中に撃っても下半身は歩いたまま、上半身だけ射撃モーションを重ねられる。
        if (m_shootHoldTimer > 0f) m_shootHoldTimer -= Time.deltaTime;
        if (m_hitHoldTimer > 0f) m_hitHoldTimer -= Time.deltaTime;

        // 上半身 Aim Layer の重みを補間切替（エイム中 or 射撃直後 or 空中被弾直後は上半身だけポーズを重ねる）
        if (m_aimLayerIndex >= 0)
        {
            float current = m_animator.GetLayerWeight(m_aimLayerIndex);
            float target = (aiming || m_shootHoldTimer > 0f || m_hitHoldTimer > 0f) ? 1f : 0f;
            float maxDelta = (m_aimLayerFadeTime > 0f) ? (Time.deltaTime / m_aimLayerFadeTime) : 1f;
            m_animator.SetLayerWeight(m_aimLayerIndex, Mathf.MoveTowards(current, target, maxDelta));
        }
    }

    private void HandleStateChange()
    {
        if (m_animator == null || m_states == null) return;

        int currentIdx = GetStateIndex(m_states.current?.GetType());
        int lastIdx    = GetStateIndex(m_states.last?.GetType());

        m_animator.SetInteger(m_hState, currentIdx);
        m_animator.SetInteger(m_hLastState, lastIdx);
        ResetTriggersExceptStateChange();
        m_animator.SetTrigger(m_hOnStateChanged);
    }

    private void HandleShoot()  { if (m_animator != null) { m_animator.SetTrigger(m_hShoot); m_shootHoldTimer = m_shootUpperBodyHold; } }
    private void HandleReload() { if (m_animator != null) m_animator.SetTrigger(m_hReload); }
    private void HandleStab()   { if (m_animator != null) m_animator.SetTrigger(m_hStab); }

    // 被弾アニメ。Health.OnDamage は実ダメージが通った時だけ発火する（無敵時間中は鳴らない）。
    // 致死ダメージ時は死亡アニメを優先したいので IsDead ならスキップ。
    // 空中では全身 HitReact を出すと落下姿勢が壊れるため、上半身レイヤーだけに食らいを重ねる。
    private void HandleDamage(int amount)
    {
        if (m_animator == null || m_health == null || m_health.IsDead) return;

        if (IsGroundedForAnimator())
        {
            m_animator.SetTrigger(m_hHit);
        }
        else
        {
            m_animator.SetTrigger(m_hHitUpper);
            m_hitHoldTimer = m_hitUpperBodyHold;
        }
    }

    // LateUpdate の IsGrounded 判定と同じ基準（磁力等の上向き外力で持ち上がっている間は離地扱い）。
    private bool IsGroundedForAnimator()
    {
        if (m_player == null) return true;
        float snapForce = m_player.Settings != null ? m_player.Settings.snapForce : 0f;
        bool airborneByExternal = m_player.externalVelocity.y > snapForce;
        return m_player.IsGrounded && !airborneByExternal;
    }

    // Stab はステート遷移と同時に FireStab → SetTrigger(Stab) するため、ここでリセットすると直後に消える。
    // 1フレーム内で State 変化 → ResetTriggers → Enter → SetTrigger(Stab) の順なら問題ないが、
    // 現状 EntityStateManager は Enter → OnStateChanged の順で発火するため Stab を除外する。
    private void ResetTriggersExceptStateChange()
    {
        if (m_animator == null) return;
        m_animator.ResetTrigger(m_hShoot);
        m_animator.ResetTrigger(m_hReload);
    }

    /// <summary>
    /// 突き刺し瞬間の AnimEvent → StabAbility へ転送。
    /// </summary>
    public void OnStabHitEvent()
    {
        m_stab?.OnStabHitEvent();
    }

    /// <summary>
    /// スタブモーション終了の AnimEvent → StabPlayerState へ転送。
    /// State が StabPlayerState でなければ無視。
    /// </summary>
    public void OnStabMotionFinishedEvent()
    {
        if (m_states?.current is StabPlayerState stabState)
            stabState.OnStabMotionFinished(m_player);
    }
}
