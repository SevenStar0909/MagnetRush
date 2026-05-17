using UnityEngine;

/// <summary>
/// EnemyBossBaseA_Animator のアニメーションイベントを受け取って転送するコンポーネント。
/// </summary>
public class EnemyBossBaseA_AnimationEventForwarder : MonoBehaviour
{
    [SerializeField] private EnemyBossBaseA_Animator m_target;
    //test comment
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

    public void EnableWindEffectEvent()
    {
        if (m_target != null) m_target.EnableWindEffectEvent();
    }

    public void DisableWindEffectEvent()
    {
        if (m_target != null) m_target.DisableWindEffectEvent();
    }

    public void EnableDustEffectEvent()
    {
        if (m_target != null) m_target.EnableDustEffectEvent();
    }

    public void DisableDustEffectEvent()
    {
        if (m_target != null) m_target.DisableDustEffectEvent();
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

    public void OnMissileFireEvent()
    {
        if (m_target != null) m_target.OnMissileFireEvent();
    }

    /// <summary>
    /// AnimationEvent からの転送。腕上げピーク・地面激突等の特定フレームで Animator を seconds 秒フリーズ。
    /// </summary>
    public void FreezeAnim(float seconds)
    {
        if (m_target != null) m_target.FreezeAnim(seconds);
    }
}
