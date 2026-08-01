using UnityEngine;

[DisallowMultipleComponent]
public class EnemyBoss02Animator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator m_animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string m_idleTriggerName = "Idle";
    [SerializeField] private string m_attackTriggerName = "Attack";
    [SerializeField] private string m_moveTriggerName = "Move";
    [SerializeField] private string m_moveEndTriggerName = "MoveEnd";
    [SerializeField] private string m_rushTriggerName = "Rush";
    [SerializeField] private string m_rushEndTriggerName = "RushEnd";
    [SerializeField] private string m_downTriggerName = "Down";
    [SerializeField] private string m_downEndTriggerName = "DownEnd";

    private int m_idleTriggerHash;
    private int m_attackTriggerHash;
    private int m_moveTriggerHash;
    private int m_moveEndTriggerHash;
    private int m_rushTriggerHash;
    private int m_rushEndTriggerHash;
    private int m_downTriggerHash;
    private int m_downEndTriggerHash;

    private static readonly int s_idleState = Animator.StringToHash("Idle");
    private static readonly int s_attackState = Animator.StringToHash("Attack");
    private static readonly int s_moveStanceState = Animator.StringToHash("MoveStance");
    private static readonly int s_moveCycleState = Animator.StringToHash("MoveCycle");
    private static readonly int s_moveEndState = Animator.StringToHash("MoveEnd");
    private static readonly int s_rushStanceState = Animator.StringToHash("RushStance");
    private static readonly int s_rushEndState = Animator.StringToHash("RushEnd");
    private static readonly int s_downStanceState = Animator.StringToHash("DownStance");
    private static readonly int s_downEndState = Animator.StringToHash("DownEnd");

    private void Awake()
    {
        if (m_animator == null)
            m_animator = GetComponentInChildren<Animator>(true);

        m_idleTriggerHash = Animator.StringToHash(m_idleTriggerName);
        m_attackTriggerHash = Animator.StringToHash(m_attackTriggerName);
        m_moveTriggerHash = Animator.StringToHash(m_moveTriggerName);
        m_moveEndTriggerHash = Animator.StringToHash(m_moveEndTriggerName);
        m_rushTriggerHash = Animator.StringToHash(m_rushTriggerName);
        m_rushEndTriggerHash = Animator.StringToHash(m_rushEndTriggerName);
        m_downTriggerHash = Animator.StringToHash(m_downTriggerName);
        m_downEndTriggerHash = Animator.StringToHash(m_downEndTriggerName);

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("EnemyBoss02", $"[{nameof(EnemyBoss02Animator)}] Animator was not found.");
            enabled = false;
        }
    }

    public void TriggerIdle() => SetTrigger(m_idleTriggerHash);
    public void TriggerAttack() => SetTrigger(m_attackTriggerHash);
    public void TriggerMove() => SetTrigger(m_moveTriggerHash);
    public void TriggerMoveEnd() => SetTrigger(m_moveEndTriggerHash);
    public void TriggerRush() => SetTrigger(m_rushTriggerHash);
    public void TriggerRushEnd() => SetTrigger(m_rushEndTriggerHash);
    public void TriggerDown() => SetTrigger(m_downTriggerHash);
    public void TriggerDownEnd() => SetTrigger(m_downEndTriggerHash);

    public bool IsIdle => IsState(s_idleState);
    public bool IsAttack => IsState(s_attackState);
    public bool IsMoveStance => IsState(s_moveStanceState);
    public bool IsMoveCycle => IsState(s_moveCycleState);
    public bool IsMoveEnd => IsState(s_moveEndState);
    public bool IsRushStance => IsState(s_rushStanceState);
    public bool IsRushEnd => IsState(s_rushEndState);
    public bool IsDownStance => IsState(s_downStanceState);
    public bool IsDownEnd => IsState(s_downEndState);

    private void SetTrigger(int hash)
    {
        if (m_animator != null)
            m_animator.SetTrigger(hash);
    }

    private bool IsState(int hash)
    {
        if (m_animator == null)
            return false;

        return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash;
    }
}
