using UnityEngine;

/// <summary>
/// EnemyBossBaseA_Animator のアニメーションイベントを受け取って転送するコンポーネント。
/// </summary>
public class EnemyBossBaseA_AnimationEventForwarder : MonoBehaviour
{
    [SerializeField] private EnemyBossBaseA_Animator m_target;

    void Awake()
    {
        if (m_target == null)
            m_target = GetComponentInParent<EnemyBossBaseA_Animator>();

        if (m_target == null)
            ChannelLogger.LogGuardReturn("Enemy", "EnemyBossBaseA_AnimationEventForwarder.m_target が未アサインです");
    }

    public void TriggerAttack()
    {
        if (m_target != null) m_target.TriggerAttack();
    }

    public void TriggerAttackFinished()
    {
        if (m_target != null) m_target.TriggerAttackFinished();
    }

    public void TriggerBeInterrupted()
    {
        if (m_target != null) m_target.TriggerBeInterrupted();
    }

    public void TriggerStunEnd()
    {
        if (m_target != null) m_target.TriggerStunEnd();
    }

    public void TriggerAttackRush()
    {
        if (m_target != null) m_target.TriggerAttackRush();
    }

    public void TriggerMissile()
    {
        if (m_target != null) m_target.TriggerMissile();
    }

    public void SetCanInterruptTrue()
    {
        if (m_target != null) m_target.SetCanInterruptTrue();
    }

    public void SetCanInterruptFalse()
    {
        if (m_target != null) m_target.SetCanInterruptFalse();
    }

    public void SetIsStunnedTrue()
    {
        if (m_target != null) m_target.SetIsStunnedTrue();
    }

    public void SetIsStunnedFalse()
    {
        if (m_target != null) m_target.SetIsStunnedFalse();
    }

    public void EnableArmHitboxEvent()
    {
        if (m_target != null) m_target.EnableArmHitboxEvent();
    }

    public void DisableArmHitboxEvent()
    {
        if (m_target != null) m_target.DisableArmHitboxEvent();
    }

    public void OnAttackFinishedEvent()
    {
        if (m_target != null) m_target.OnAttackFinishedEvent();
    }

    public void OnStunEndEvent()
    {
        if (m_target != null) m_target.OnStunEndEvent();
    }

    public void OnRushFinishedEvent()
    {
        if (m_target != null) m_target.OnRushFinishedEvent();
    }

    public void OnMissileFinishedEvent()
    {
        if (m_target != null) m_target.OnMissileFinishedEvent();
    }
}
