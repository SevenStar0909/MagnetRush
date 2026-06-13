using UnityEngine;

/// <summary>
/// 子 AttackBox の Trigger を親の BossAttackHitboxes へ転送する。
/// </summary>
public sealed class BossAttackHitboxRelay : MonoBehaviour
{
    private BossAttackHitboxes m_owner;
    private Collider m_attackCollider;
    private bool m_isArm;

    internal void Initialize(BossAttackHitboxes owner, Collider attackCollider, bool isArm)
    {
        m_owner = owner;
        m_attackCollider = attackCollider;
        m_isArm = isArm;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_owner != null)
            m_owner.HandleTriggerEnter(m_attackCollider, other, m_isArm);
    }
}
