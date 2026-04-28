using UnityEngine;

/// <summary>
/// ボスの腕に追従する打撃ヒットボックス。AnimEvent で ON/OFF され、
/// Player Hurtbox に重なれば IHittable.OnHit() でダメージを通達する。
/// 自傷防止は Layer Matrix (MeleeHitbox x Enemy = OFF) に任せる。
/// 依存: Collider (Trigger), EnemyBossSettings (ダメージ値の参照)
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossArmHitbox : MonoBehaviour
{
    [Tooltip("ダメージ値の参照元 SO。未設定なら親 EnemyBossBase から取得")]
    [SerializeField] private EnemyBossSettings m_settings;

    [Tooltip("攻撃元ボス。HitData.source として使われる")]
    [SerializeField] private GameObject m_owner;

    private Collider m_collider;

    void Awake()
    {
        m_collider = GetComponent<Collider>();
        if (!m_collider.isTrigger)
        {
            Debug.LogError("[BossArmHitbox] Collider は isTrigger=true で配置してください", this);
        }

        if (m_settings == null)
        {
            var boss = GetComponentInParent<EnemyBossBase>();
            if (boss != null) m_settings = boss.StatusData;
        }

        if (m_owner == null)
        {
            var boss = GetComponentInParent<EnemyBossBase>();
            if (boss != null) m_owner = boss.gameObject;
        }

        DisableHitbox();
    }

    /// <summary>
    /// アニメーションイベントから呼ぶ。ヒットボックスを有効化する。
    /// </summary>
    public void EnableHitbox()
    {
        if (m_collider == null) { ChannelLogger.LogGuardReturn("Enemy", "Collider 未取得"); return; }
        m_collider.enabled = true;
    }

    /// <summary>
    /// アニメーションイベントから呼ぶ。ヒットボックスを無効化する。
    /// </summary>
    public void DisableHitbox()
    {
        if (m_collider == null) { ChannelLogger.LogGuardReturn("Enemy", "Collider 未取得"); return; }
        m_collider.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (m_settings == null) { ChannelLogger.LogGuardReturn("Enemy", "EnemyBossSettings 未設定"); return; }

        var hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null) { ChannelLogger.LogGuardReturn("Enemy", "IHittable 不在"); return; }

        var hit = new HitData
        {
            damage = m_settings.attackDamage,
            source = m_owner,
            hitPoint = transform.position,
        };
        hittable.OnHit(hit);
    }
}
