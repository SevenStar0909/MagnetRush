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
        if (IsDead) return;
        if (IsRecovering) return;
        if (amount <= 0) return;

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
        if (IsDead) return;
        if (amount <= 0) return;

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
