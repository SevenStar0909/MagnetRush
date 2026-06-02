using UnityEngine;

/// <summary>
/// Animator と PlayerAnimator が別 GameObject に分かれているための AnimationEvent リレー。
/// AnimationEvent は Animator と同じ GameObject の MonoBehaviour しか呼べないため、
/// このコンポーネントを Animator と同じ GO (Player_T_) に置き、親の PlayerAnimator に転送する。
/// </summary>
public class PlayerAnimEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerAnimator m_playerAnimator;

    void Awake()
    {
        if (m_playerAnimator == null)
            m_playerAnimator = GetComponentInParent<PlayerAnimator>();
    }

    public void OnStabHitEvent()
    {
        if (m_playerAnimator != null) m_playerAnimator.OnStabHitEvent();
    }

    public void OnStabMotionFinishedEvent()
    {
        if (m_playerAnimator != null) m_playerAnimator.OnStabMotionFinishedEvent();
    }
}
