using UnityEngine;

/// <summary>
/// 子の Animator が持つ root motion を、親 EntityController の collide-and-slide 経由でルートに適用する。
/// OnAnimatorMove を実装すると Unity 既定の root motion 適用（Animator の GameObject へ直書き・衝突無視）が
/// 無効になるので、それを上書きして root motion 中（Rush 等）に壁を貫通しないようにする。
/// Animator が子に付いていてルート（EntityController）と別 Transform の場合に必要。
/// 依存: 同じ GameObject の Animator、親階層の EntityController。
/// </summary>
[RequireComponent(typeof(Animator))]
public class RootMotionToController : MonoBehaviour
{
    private Animator m_animator;
    private EntityController m_controller;
    private Transform m_root;

    private void Awake()
    {
        m_animator = GetComponent<Animator>();
        m_controller = GetComponentInParent<EntityController>();
        if (m_controller != null)
            m_root = m_controller.transform;
    }

    // OnAnimatorMove を実装すると Unity は root motion を自動適用しなくなる。
    // applyRootMotion=true のステート（Rush 等）の deltaPosition をルートへ collide-and-slide 経由で渡し、
    // 壁ですり抜けず止まるようにする。
    private void OnAnimatorMove()
    {
        // root motion 無効時は毎フレーム呼ばれるので、ログを出さず静かに抜ける
        if (m_animator == null || !m_animator.applyRootMotion)
            return;

        if (m_controller == null || m_root == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", "RootMotionToController: 親に EntityController が無い");
            return;
        }

        Vector3 delta = m_animator.deltaPosition;
        if (delta.sqrMagnitude < 1e-10f)
            return;

        m_root.position = m_controller.Move(m_root.position, delta);
    }
}
