using System;
using UnityEngine;

/// <summary>
/// 当たり判定の親コンテナ。Hurtbox/Pushbox などの子コライダーを束ねる。
/// IHittable を実装し、子のどのコライダーから OnHit が呼ばれてもここに集約される。
/// 攻撃側は other.GetComponentInParent&lt;IHittable&gt;() で必ず到達できる。
/// </summary>
public class Hitbox : MonoBehaviour, IHittable
{
    [SerializeField] private Health m_health;
    [SerializeField] private Stamina m_stamina;

    public event Action<HitData> OnHitEvent;

    void Awake()
    {
        if (m_health == null)
            m_health = GetComponentInParent<Health>();

        if (m_stamina == null)
            m_stamina = GetComponentInParent<Stamina>();
    }

    public void OnHit(HitData hit)
    {
        if (m_health == null && m_stamina == null)
        {
            ChannelLogger.LogGuardReturn("Entity", "Health/Stamina 未設定");
            return;
        }

        // Boss等: Stamina がある場合はまず Stamina を削る
        if (m_stamina != null)
        {
            bool wasBroken = m_stamina.IsBroken;

            int staminaDamage = 35; // 固定値でスタミナを削る (例: ダメージ5でもスタミナ35消費)
            m_stamina.Consume(staminaDamage);

            // Stamina が「今」0になった瞬間だけ Health -1
            if (!wasBroken && m_stamina.IsBroken && m_health != null)
                m_health.DamageIgnoreCooldown(1);

            ChannelLogger.Log(
                "EnemyBossA", $"[Hitbox] StaminaDamage: src={(hit.source != null ? hit.source.name : "null")} " +
                $"rawDmg={hit.damage} staminaDmg={staminaDamage} ");

            OnHitEvent?.Invoke(hit);
            return;
        }

        // 通常: Health を削る
        if (m_health == null) { ChannelLogger.LogGuardReturn("Entity", "Health未設定"); return; }
        m_health.Damage(hit.damage);

        OnHitEvent?.Invoke(hit);
    }
}
