using System;
using UnityEngine;

/// <summary>
/// 当たり判定の親コンテナ。Hurtbox/Pushbox などの子コライダーを束ねる。
/// IHittable を実装し、被弾時は Health を削るだけのシンプルな経路。
/// Stamina 等の追加効果は専用 Hitbox 派生（ArmStunHitbox 等）で扱う。
/// 攻撃側は other.GetComponentInParent&lt;IHittable&gt;() で必ず到達できる。
/// </summary>
public class Hitbox : MonoBehaviour, IHittable
{
    [SerializeField] private Health m_health;

    public event Action<HitData> OnHitEvent;

    void Awake()
    {
        if (m_health == null)
            m_health = GetComponentInParent<Health>();
    }

    public void OnHit(HitData hit)
    {
        if (m_health == null)
        {
            ChannelLogger.LogGuardReturn("Entity", "Health未設定");
            return;
        }

        m_health.Damage(hit.damage);
        OnHitEvent?.Invoke(hit);
    }
}
