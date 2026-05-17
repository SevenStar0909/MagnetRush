using UnityEngine;

/// <summary>
/// このアニメステート在中の間だけ Animator.applyRootMotion を有効化する。
/// Generic Avatar で root motion source bone の Y/Z curve（倒れる/突進など）を生かしたいステート用。
/// OnStateExit で前の applyRootMotion 値に戻す。
/// </summary>
public class EnableRootMotionStateBehaviour : StateMachineBehaviour
{
    [SerializeField] private bool m_enableRootMotion = true;

    private bool m_previousApplyRootMotion;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        m_previousApplyRootMotion = animator.applyRootMotion;
        animator.applyRootMotion = m_enableRootMotion;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.applyRootMotion = m_previousApplyRootMotion;
    }
}
