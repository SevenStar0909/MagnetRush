using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 磁力で加速されたPhysicsObjectがEntityBodyに衝突した際にダメージを与える。
/// OnCollisionEnterで検出。PhysicsObject×EntityBody=ONのMatrix制御。
/// 子コライダーから親のHitbox(IHittable)を辿ってOnHit()を呼ぶ。
/// </summary>
public class MagneticContactDamage : MonoBehaviour
{
    [SerializeField] private ContactDamageSettings m_settings;

    private Magnetizable m_magnetizable;
    private Rigidbody m_rb;
    private readonly HashSet<IHittable> m_hitTargets = new HashSet<IHittable>();
    private bool m_wasActive;

    void Awake()
    {
        m_magnetizable = GetComponentInParent<Magnetizable>();
        m_rb = GetComponentInParent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (m_magnetizable == null) { ChannelLogger.LogGuardReturn("Magnet", "Magnetizableなし"); return; }

        // 磁力が切れたらヒット記録をリセット（再度当たれるように）
        if (!m_magnetizable.IsActive && m_wasActive)
        {
            m_hitTargets.Clear();
            m_wasActive = false;
        }
        else if (m_magnetizable.IsActive)
        {
            m_wasActive = true;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[ContactDmg] {name} hit {collision.collider.name} vel={collision.relativeVelocity.magnitude:F2} mag={(m_magnetizable != null ? m_magnetizable.IsActive.ToString() : "null")}");

        if (m_magnetizable == null || !m_magnetizable.IsActive) { Debug.Log("[ContactDmg] -> skip: magnetizable inactive"); return; }
        if (m_settings == null) { Debug.Log("[ContactDmg] -> skip: settings null"); return; }

        if (collision.relativeVelocity.magnitude < m_settings.minVelocity) { Debug.Log($"[ContactDmg] -> skip: velocity {collision.relativeVelocity.magnitude:F2} < {m_settings.minVelocity}"); return; }

        if (collision.collider.transform.IsChildOf(m_magnetizable.transform)) { Debug.Log("[ContactDmg] -> skip: self"); return; }

        var hittable = collision.collider.GetComponentInParent<IHittable>();
        if (hittable == null && collision.rigidbody != null)
            hittable = collision.rigidbody.GetComponentInChildren<IHittable>();
        if (hittable == null) { Debug.Log($"[ContactDmg] -> skip: no IHittable on {collision.collider.name}"); return; }

        Debug.Log($"[ContactDmg] -> HIT! target={((MonoBehaviour)hittable).name} damage={m_settings.damage}");

        // 同一対象への重複ダメージ防止（磁力切れるまで1回のみ）
        if (m_hitTargets.Add(hittable))
        {
            hittable.OnHit(new HitData
            {
                damage = m_settings.damage,
                hitPoint = collision.GetContact(0).point,
                knockbackDir = collision.relativeVelocity.normalized,
                source = gameObject
            });
        }
    }
}
