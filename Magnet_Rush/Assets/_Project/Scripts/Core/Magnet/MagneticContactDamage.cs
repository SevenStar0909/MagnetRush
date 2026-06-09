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

    /// <summary>この物理オブジェクトがボス本体に当たった時に与えるスタン値の蓄積率（％）。設定SOから取得。</summary>
    public int StunGaugePercent => m_settings != null ? m_settings.stunGaugePercent : 0;

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
        if (m_magnetizable == null || !m_magnetizable.IsActive) { ChannelLogger.LogGuardReturn("ContactDmg", "磁化非アクティブ"); return; }
        if (m_settings == null) { ChannelLogger.LogGuardReturn("ContactDmg", "設定なし"); return; }

        float vel = collision.relativeVelocity.magnitude;
        if (vel < m_settings.minVelocity) { ChannelLogger.LogGuardReturn("ContactDmg", $"速度不足 {vel:F1}<{m_settings.minVelocity}"); return; }

        if (collision.collider.transform.IsChildOf(m_magnetizable.transform)) { ChannelLogger.LogGuardReturn("ContactDmg", "自己衝突"); return; }

        var hittable = collision.collider.GetComponentInParent<IHittable>();
        if (hittable == null && collision.rigidbody != null)
            hittable = collision.rigidbody.GetComponentInChildren<IHittable>();
        if (hittable == null) { ChannelLogger.LogGuardReturn("ContactDmg", $"IHittable なし: {collision.collider.name}"); return; }

        // プレイヤーには物理オブジェクトの接触ダメージを与えない（ボスのスタン蓄積・敵への加害は維持）
        if (hittable.HitGroup == HitGroup.Player) { ChannelLogger.LogGuardReturn("ContactDmg", "プレイヤーへの接触ダメージは無効"); return; }

        // HIT のみ目立つログで残す
        Debug.Log($"<color=#FF5722>[ContactDmg]</color> {name} → {((MonoBehaviour)hittable).name} dmg={m_settings.damage} vel={vel:F1}");

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
