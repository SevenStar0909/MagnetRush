using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// ダメージクールダウン付きのHP管理。
/// </summary>
public class Health : MonoBehaviour
{
    [FormerlySerializedAs("maxHealth")]
    [SerializeField] private int m_maxHealth = 3;
    [FormerlySerializedAs("damageCooldown")]
    [SerializeField] private float m_damageCooldown = 1f;

    public int MaxHealth => m_maxHealth;
    public int CurrentHealth { get; private set; }
    public float HealthRatio => m_maxHealth > 0 ? (float)CurrentHealth / m_maxHealth : 0f;
    public bool IsDead => CurrentHealth <= 0;
    public bool IsRecovering => Time.time < m_lastDamageTime + m_damageCooldown;

    private float m_lastDamageTime = -999f;

    public event Action<int> OnDamage;
    public event Action OnDie;
    public event Action<int> OnHeal;

    void Awake()
    {
        CurrentHealth = m_maxHealth;
    }

    /// <summary>
    /// ダメージを与える。クールダウン中・死亡中は無視される。
    /// </summary>
    public void Damage(int amount)
    {
        if (IsDead) { ChannelLogger.LogGuardReturn("Entity", "既に死亡"); return; }
        if (IsRecovering) { ChannelLogger.LogGuardReturn("Entity", "無敵時間中"); return; }
        if (amount <= 0) { ChannelLogger.LogGuardReturn("Entity", "ダメージ量0以下"); return; }

        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0);
        m_lastDamageTime = Time.time;
        OnDamage?.Invoke(amount);

        if (IsDead)
        {
            OnDie?.Invoke();
        }
    }

    /// <summary>
    /// HPを回復する。最大HPを超えない。
    /// </summary>
    public void Heal(int amount)
    {
        if (IsDead) { ChannelLogger.LogGuardReturn("Entity", "死亡中は回復不可"); return; }
        if (amount <= 0) { ChannelLogger.LogGuardReturn("Entity", "回復量0以下"); return; }

        CurrentHealth = Mathf.Min(CurrentHealth + amount, m_maxHealth);
        OnHeal?.Invoke(amount);
    }

    /// <summary>
    /// HPを最大値にリセットする。ダメージクールダウンも解除する。
    /// </summary>
    public void ResetHealth()
    {
        CurrentHealth = m_maxHealth;
        m_lastDamageTime = -999f;
    }
}
