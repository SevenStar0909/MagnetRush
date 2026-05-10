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

    void Awake()
    {
        if (m_events   == null) m_events   = GetComponentInParent<PlayerEvents>();
        if (m_input    == null) m_input    = GetComponentInParent<PlayerInputHandler>();
        if (m_states   == null) m_states   = GetComponentInParent<PlayerStateManager>();
        if (m_player   == null) m_player   = GetComponentInParent<Player>();
        if (m_aim      == null) m_aim      = GetComponentInParent<AimAbility>();

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

        ValidateAnimatorParameters();
        StartCoroutine(ValidateStateOrderDelayed());
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
        }
        if (m_states != null)
        {
            m_states.OnStateChanged += HandleStateChange;
        }
    }

    void OnDisable()
    {
        if (m_events != null)
        {
            m_events.onShoot.RemoveListener(HandleShoot);
            m_events.onReload.RemoveListener(HandleReload);
        }
        if (m_states != null)
        {
            m_states.OnStateChanged -= HandleStateChange;
        }
    }

    void LateUpdate()
    {
        if (m_animator == null) return;

        if (m_player != null)
        {
            m_animator.SetFloat(m_hMoveSpeed, m_player.lateralVelocity.magnitude);
            m_animator.SetBool(m_hIsGrounded, m_player.IsGrounded);
        }

        if (m_input != null)
        {
            var mv = m_input.MoveInput;
            m_animator.SetFloat(m_hMoveInputX, mv.x);
            m_animator.SetFloat(m_hMoveInputZ, mv.y);
            m_animator.SetFloat(m_hVerticalSpeed, m_player.velocity.y);
        }

        if (m_aim != null)
        {
            m_animator.SetBool(m_hIsAiming, m_aim.IsAiming);
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

    private void HandleShoot()  { if (m_animator != null) m_animator.SetTrigger(m_hShoot); }
    private void HandleReload() { if (m_animator != null) m_animator.SetTrigger(m_hReload); }

    private void ResetTriggersExceptStateChange()
    {
        if (m_animator == null) return;
        m_animator.ResetTrigger(m_hShoot);
        m_animator.ResetTrigger(m_hReload);
    }
}
