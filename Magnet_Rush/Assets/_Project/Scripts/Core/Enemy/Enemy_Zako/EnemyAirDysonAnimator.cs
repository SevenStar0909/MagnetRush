using UnityEngine;

/// <summary>
/// Dyson air enemy animator driver.
/// AI owns the timing; the controller only chains start -> loop and end -> idle.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAirBase))]
public class EnemyAirDysonAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Driven Animator. If unset, the first child Animator is used.")]
    [SerializeField] private Animator m_animator;

    [Header("Animator State Names")]
    [SerializeField] private string m_idleStateName = "Idel";
    [SerializeField] private string m_suctionStartStateName = "Dyson_Start";
    [SerializeField] private string m_suctionLoopStateName = "Dyson";
    [SerializeField] private string m_suctionEndStateName = "Dyson_End";

    [Header("Blend")]
    [SerializeField] private float m_crossFadeTime = 0.08f;

    private int m_idleStateHash;
    private int m_suctionStartStateHash;
    private int m_suctionLoopStateHash;
    private int m_suctionEndStateHash;
    private bool m_isSuctioning;

    private void Awake()
    {
        if (m_animator == null)
            m_animator = GetComponentInChildren<Animator>(true);

        m_idleStateHash = Animator.StringToHash(m_idleStateName);
        m_suctionStartStateHash = Animator.StringToHash(m_suctionStartStateName);
        m_suctionLoopStateHash = Animator.StringToHash(m_suctionLoopStateName);
        m_suctionEndStateHash = Animator.StringToHash(m_suctionEndStateName);

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", $"[{nameof(EnemyAirDysonAnimator)}] {name}: Animator was not found.");
            enabled = false;
            return;
        }

        PlayIdle();
    }

    public void PlayIdle()
    {
        m_isSuctioning = false;
        CrossFade(m_idleStateHash);
    }

    public void TriggerSuctionStart()
    {
        if (m_isSuctioning)
            return;

        m_isSuctioning = true;
        CrossFade(m_suctionStartStateHash);
    }

    public void TriggerSuctionEnd()
    {
        if (!m_isSuctioning)
            return;

        m_isSuctioning = false;
        CrossFade(m_suctionEndStateHash);
    }

    public bool IsSuctioning => m_isSuctioning;

    public bool IsInSuctionLoop
    {
        get
        {
            if (m_animator == null)
                return false;

            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == m_suctionLoopStateHash;
        }
    }

    private void CrossFade(int stateHash)
    {
        if (m_animator == null)
            return;

        if (m_crossFadeTime > 0f)
            m_animator.CrossFade(stateHash, m_crossFadeTime, 0);
        else
            m_animator.Play(stateHash, 0, 0f);
    }
}
