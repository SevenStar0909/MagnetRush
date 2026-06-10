using System;
using UnityEngine;

/// <summary>
/// ボス右手専用の被弾判定。AttackStance中のみ Stamina も削る。
/// それ以外（非AttackStance）は通常の Health ダメージのみ。
/// IHittable を実装し、GetComponentInParent&lt;IHittable&gt; で右手子コライダー側から最近祖として捕捉される。
/// Stamina が 0 になると Stamina.OnBreak 経由で EnemyBossAI が Stun ステートに遷移させる。
/// 依存: Health, Stamina, EnemyBossBaseA_Animator, EnemyBossSettings
/// </summary>
public sealed class ArmStunHitbox : MonoBehaviour, IHittable
{
    [SerializeField] private Health m_health;
    [SerializeField] private Stamina m_stamina;
    [SerializeField] private EnemyBossBaseA_Animator m_animator;
    [SerializeField] private EnemyBossSettings m_settings;

    public event Action<HitData> OnHitEvent;

    void Awake()
    {
        if (m_health == null)
            m_health = GetComponentInParent<Health>();

        if (m_stamina == null)
            m_stamina = GetComponentInParent<Stamina>();

        if (m_animator == null)
            m_animator = GetComponentInParent<EnemyBossBaseA_Animator>();

        if (m_settings == null)
        {
            // EnemyBossSettings は SO のため GetComponentInParent 不可。EnemyBossBase 経由で取得する
            var boss = GetComponentInParent<EnemyBossBase>();
            if (boss != null) m_settings = boss.StatusData;
        }

        if (m_health == null)
            ChannelLogger.LogError("EnemyBossA", $"[ArmStunHitbox] {name}: Health 未取得");
        if (m_stamina == null)
            ChannelLogger.LogError("EnemyBossA", $"[ArmStunHitbox] {name}: Stamina 未取得");
        if (m_animator == null)
            ChannelLogger.LogError("EnemyBossA", $"[ArmStunHitbox] {name}: EnemyBossBaseA_Animator 未取得");
        if (m_settings == null)
            ChannelLogger.LogError("EnemyBossA", $"[ArmStunHitbox] {name}: EnemyBossSettings 未取得");
    }

    public void OnHit(HitData hit)
    {
        if (m_health == null)
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "Health未設定");
            return;
        }

        // HPダメージは全状態で適用
        m_health.Damage(hit.damage);

        // 既にStamina切れの間は追加効果なし（HPダメージのみ）
        if (m_stamina != null && m_stamina.IsBroken)
        {
            OnHitEvent?.Invoke(hit);
            return;
        }

        // AttackStance / AttackMotion 中のみ: スタミナ消費 + Stagger 発火
        // 中立・Rush・Missile・Stunned・Stagger 中は HPダメージのみ
        if (m_animator != null && (m_animator.IsInAttackStance || m_animator.IsInAttackMotion))
        {
            if (m_stamina != null && m_settings != null)
            {
                int dmg = m_settings.armStunStaminaDamage;
                if (dmg > 0)
                {
                    m_stamina.Consume(dmg);
                    ChannelLogger.Log("EnemyBossA",
                        $"[ArmStunHitbox] AttackState hit src={(hit.source != null ? hit.source.name : "null")} " +
                        $"staminaDmg={dmg} remain={m_stamina.CurrentStamina}/{m_stamina.MaxStamina}");
                }
            }

            m_animator.SetIsStaggerTrue();
            m_animator.TriggerBeInterrupted();
        }

        OnHitEvent?.Invoke(hit);
    }
}
