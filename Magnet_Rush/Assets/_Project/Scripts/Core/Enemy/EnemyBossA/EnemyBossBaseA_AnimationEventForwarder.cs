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

    public void EnableArmHitboxEvent()
    {
        if (m_target != null) m_target.EnableArmHitboxEvent();

        ChannelLogger.Log("EnemyBoss", $"BossArmEvent step1 is Triggered ");
    }

    public void DisableArmHitboxEvent()
    {
        if (m_target != null) m_target.DisableArmHitboxEvent();
    }

    public void EnableRushHitboxEvent()
    {
        if (m_target != null) m_target.EnableRushHitboxEvent();

        ChannelLogger.Log("EnemyBoss", $"BossRushEvent step1 is Triggered ");
    }

    public void DisableRushHitboxEvent()
    {
        if (m_target != null) m_target.DisableRushHitboxEvent();
    }

    public void EnableWindEffectEvent()
    {
        if (m_target != null) m_target.EnableWindEffectEvent();
    }

    public void DisableWindEffectEvent()
    {
        if (m_target != null) m_target.DisableWindEffectAnimationEvent();
    }

    public void EnableDustEffectEvent()
    {
        if (m_target != null) m_target.EnableDustEffectEvent();
    }

    public void OnAttackFinishedEvent()
    {
        if (m_target != null) m_target.OnAttackFinishedEvent();
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
