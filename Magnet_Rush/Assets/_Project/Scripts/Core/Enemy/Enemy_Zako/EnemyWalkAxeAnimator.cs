using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyWalkBase))]
public class EnemyWalkAxeAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Driven Animator. If unset, the first child Animator is used.")]
    [SerializeField] private Animator m_animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string m_isMovingParameterName = "IsMoving";
    [SerializeField] private string m_attackTriggerName = "Attack";

    private int m_isMovingParameterHash;
    private int m_attackTriggerHash;
    private bool m_isMoving;

    private void Awake()
    {
        if (m_animator == null)
            m_animator = GetComponentInChildren<Animator>(true);

        m_isMovingParameterHash = Animator.StringToHash(m_isMovingParameterName);
        m_attackTriggerHash = Animator.StringToHash(m_attackTriggerName);

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", $"[{nameof(EnemyWalkAxeAnimator)}] {name}: Animator was not found.");
            enabled = false;
            return;
        }

        EnemyWalkAxeAnimationEventRelay relay =
            m_animator.GetComponent<EnemyWalkAxeAnimationEventRelay>();
        if (relay == null)
            relay = m_animator.gameObject.AddComponent<EnemyWalkAxeAnimationEventRelay>();

        relay.Initialize(GetComponent<EnemyWalkAxeAi>());
        m_animator.SetBool(m_isMovingParameterHash, false);
    }

    public void SetMoving(bool isMoving)
    {
        if (m_animator == null || m_isMoving == isMoving)
            return;

        m_isMoving = isMoving;
        m_animator.SetBool(m_isMovingParameterHash, isMoving);
    }

    public void TriggerAttack()
    {
        if (m_animator == null)
            return;

        SetMoving(false);
        m_animator.SetTrigger(m_attackTriggerHash);
    }

    public bool IsMoving => m_isMoving;
}

[DisallowMultipleComponent]
public class EnemyWalkAxeAnimationEventRelay : MonoBehaviour
{
    private EnemyWalkAxeAi m_target;

    public void Initialize(EnemyWalkAxeAi target)
    {
        m_target = target;
    }

    private void Awake()
    {
        if (m_target == null)
            m_target = GetComponentInParent<EnemyWalkAxeAi>();
    }

    public void OnAttackHitStartEvent()
    {
        m_target?.OnAttackHitStartEvent();
    }

    public void OnAttackHitEndEvent()
    {
        m_target?.OnAttackHitEndEvent();
    }

    public void OnAttackFinishedEvent()
    {
        m_target?.OnAttackFinishedEvent();
    }
}
