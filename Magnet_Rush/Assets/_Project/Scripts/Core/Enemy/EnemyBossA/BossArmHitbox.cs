using UnityEngine;

/// <summary>
/// ボス腕骨の子GameObjectに配置する近接ヒットボックス。
/// AnimationEvent からの EnableHitbox/DisableHitbox 呼び出しでスイング窓だけTrigger Colliderを有効化する。
/// 対象判定はLayer Matrix任せ（MeleeHitbox×Player=ON, ×Enemy=OFF）。
/// 自傷防止のためコード側に陣営判定は持たない（collision-design-principles 原則4）。
/// 依存: Trigger Collider (Layer=MeleeHitbox), EnemyBossSettings(=damage), EnemyBossBase(=source)
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossArmHitbox : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ダメージ値の参照元 SO")]
    [SerializeField] private EnemyBossSettings m_settings;

    [Tooltip("HitData.source に渡す加害者 GameObject。未設定なら親階層の EnemyBossBase を使う")]
    [SerializeField] private EnemyBossBase m_owner;

    private Collider m_collider;
    private readonly System.Collections.Generic.HashSet<Health> m_hitTargets = new();

    void Awake()
    {
        m_collider = GetComponent<Collider>();
        if (m_collider != null) m_collider.isTrigger = true;
        if (m_collider != null) m_collider.enabled = false;

        if (m_owner == null)
            m_owner = GetComponentInParent<EnemyBossBase>();

        if (m_settings == null && m_owner != null)
            m_settings = m_owner.StatusData;

        if (m_settings == null)
            ChannelLogger.LogError("Enemy", $"[BossArmHitbox] {name}: EnemyBossSettings 未設定");
    }

    /// <summary>スイング窓開始。重複ヒット防止 HashSet をクリアして Collider を有効化する。</summary>
    public void EnableHitbox()
    {
        m_hitTargets.Clear();
        if (m_collider != null) m_collider.enabled = true;
    }

    /// <summary>スイング窓終了。Collider を無効化する。</summary>
    public void DisableHitbox()
    {
        if (m_collider != null) m_collider.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (m_collider == null || !m_collider.enabled) { ChannelLogger.LogGuardReturn("Enemy", "腕Hitbox無効"); return; }
        if (m_settings == null) { ChannelLogger.LogGuardReturn("Enemy", "Settings未設定"); return; }
        if (other == null) { ChannelLogger.LogGuardReturn("Enemy", "Collider未設定"); return; }

        var hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null) { ChannelLogger.LogGuardReturn("Enemy", "IHittable未実装"); return; }

        var health = other.GetComponentInParent<Health>();
        if (health != null && !m_hitTargets.Add(health)) { ChannelLogger.LogGuardReturn("Enemy", "同一スイング重複ヒット"); return; }

        hittable.OnHit(new HitData
        {
            damage = m_settings.attackDamage,
            hitPoint = other.ClosestPoint(transform.position),
            knockbackDir = (other.transform.position - transform.position).normalized,
            source = m_owner != null ? m_owner.gameObject : gameObject
        });
    }
}
