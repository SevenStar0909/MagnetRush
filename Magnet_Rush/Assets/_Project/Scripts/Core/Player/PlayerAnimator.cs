using UnityEngine;

/// <summary>
/// プレイヤーの Animator を駆動する専用コンポーネント。
/// PlayerEvents を購読して射撃系 Trigger を、LateUpdate で連続値を、
/// ステート変化で State(Int)+OnStateChanged(Trigger) を更新する。
/// Animator の直接操作はこのクラスのみに集約し、他からは触らない。
/// 依存: Animator, PlayerEvents, PlayerInputHandler, PlayerStateManager, Entity, Player
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("このプレイヤーの Animator。未設定なら自身の GetComponent<Animator>()")]
    [SerializeField] private Animator m_animator;

    [Tooltip("イベントハブ。未設定なら親の GetComponentInParent<PlayerEvents>()")]
    [SerializeField] private PlayerEvents m_events;

    [Tooltip("入力ハンドラ。未設定なら親の GetComponentInParent<PlayerInputHandler>()")]
    [SerializeField] private PlayerInputHandler m_input;

    [Tooltip("ステートマネージャ。未設定なら親の GetComponentInParent<PlayerStateManager>()")]
    [SerializeField] private PlayerStateManager m_states;

    [Tooltip("Entity。未設定なら親の GetComponentInParent<Entity>()")]
    [SerializeField] private Entity m_entity;

    [Tooltip("Player 本体。未設定なら親の GetComponentInParent<Player>()")]
    [SerializeField] private Player m_player;

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
    [SerializeField] private string m_selfShootName = "SelfShoot";
    [SerializeField] private string m_reloadName = "Reload";

    private int m_hState;
    private int m_hLastState;
    private int m_hOnStateChanged;
    private int m_hMoveSpeed;
    private int m_hMoveInputX;
    private int m_hMoveInputZ;
    private int m_hIsAiming;
    private int m_hIsGrounded;
    private int m_hShoot;
    private int m_hSelfShoot;
    private int m_hReload;

    void Awake()
    {
        if (m_animator == null) m_animator = GetComponent<Animator>();
        if (m_events   == null) m_events   = GetComponentInParent<PlayerEvents>();
        if (m_input    == null) m_input    = GetComponentInParent<PlayerInputHandler>();
        if (m_states   == null) m_states   = GetComponentInParent<PlayerStateManager>();
        if (m_entity   == null) m_entity   = GetComponentInParent<Entity>();
        if (m_player   == null) m_player   = GetComponentInParent<Player>();
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
        m_hSelfShoot       = Animator.StringToHash(m_selfShootName);
        m_hReload          = Animator.StringToHash(m_reloadName);

        if (m_states != null)
        {
            m_states.OnStateChanged += HandleStateChange;
        }

        ValidateAnimatorParameters();
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
            (m_selfShootName,       "SelfShoot (Trigger)"),
            (m_reloadName,          "Reload (Trigger)"),
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

    void OnEnable()
    {
        if (m_events == null) return;
        m_events.OnShoot.AddListener(HandleShoot);
        m_events.OnSelfShoot.AddListener(HandleSelfShoot);
        m_events.OnReload.AddListener(HandleReload);
    }

    void OnDisable()
    {
        if (m_events == null) return;
        m_events.OnShoot.RemoveListener(HandleShoot);
        m_events.OnSelfShoot.RemoveListener(HandleSelfShoot);
        m_events.OnReload.RemoveListener(HandleReload);
    }

    void OnDestroy()
    {
        if (m_states != null)
            m_states.OnStateChanged -= HandleStateChange;
    }

    void LateUpdate()
    {
        if (m_animator == null) return;

        if (m_entity != null)
        {
            m_animator.SetFloat(m_hMoveSpeed, m_entity.lateralVelocity.magnitude);
            m_animator.SetBool(m_hIsGrounded, m_entity.IsGrounded);
        }

        if (m_input != null)
        {
            var mv = m_input.MoveInput;
            m_animator.SetFloat(m_hMoveInputX, mv.x);
            m_animator.SetFloat(m_hMoveInputZ, mv.y);
        }

        if (m_player != null)
        {
            m_animator.SetBool(m_hIsAiming, m_player.IsAiming);
        }
    }

    private void HandleStateChange()
    {
        if (m_animator == null || m_states == null) return;
        m_animator.SetInteger(m_hState, m_states.index);
        m_animator.SetInteger(m_hLastState, m_states.lastIndex);
        ResetTriggersExceptStateChange();
        m_animator.SetTrigger(m_hOnStateChanged);
    }

    private void HandleShoot()     { if (m_animator != null) m_animator.SetTrigger(m_hShoot); }
    private void HandleSelfShoot() { if (m_animator != null) m_animator.SetTrigger(m_hSelfShoot); }
    private void HandleReload()    { if (m_animator != null) m_animator.SetTrigger(m_hReload); }

    private void ResetTriggersExceptStateChange()
    {
        if (m_animator == null) return;
        m_animator.ResetTrigger(m_hShoot);
        m_animator.ResetTrigger(m_hSelfShoot);
        m_animator.ResetTrigger(m_hReload);
    }
}
