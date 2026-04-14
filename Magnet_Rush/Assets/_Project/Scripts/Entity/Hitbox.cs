using UnityEngine;

/// <summary>
/// 当たり判定の親コンテナ。Hurtbox/Pushbox などの子コライダーを束ねる。
/// IHittable を実装し、子のどのコライダーから OnHit が呼ばれてもここに集約される。
/// 攻撃側は other.GetComponentInParent&lt;IHittable&gt;() で必ず到達できる。
/// </summary>
public class Hitbox : MonoBehaviour, IHittable
{
    [SerializeField] private Health m_health;

    void Awake()
    {
        if (m_health == null)
            m_health = GetComponentInParent<Health>();
    }

    public void OnHit(HitData hit)
    {
        if (m_health == null) return;
        m_health.Damage(hit.damage);
    }
}
