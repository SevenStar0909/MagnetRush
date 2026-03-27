using System;
using UnityEngine;

/// <summary>
/// ダメージクールダウン付きのHP管理。
/// </summary>
public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float damageCooldown = 1f;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;
    public bool IsRecovering => Time.time < lastDamageTime + damageCooldown;

    private float lastDamageTime = -999f;

    public event Action<int> OnDamage;
    public event Action OnDie;
    public event Action<int> OnHeal;

    void Awake()
    {
        CurrentHealth = maxHealth;
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
        lastDamageTime = Time.time;
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

        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
        OnHeal?.Invoke(amount);
    }

    /// <summary>
    /// HPを最大値にリセットする。ダメージクールダウンも解除する。
    /// </summary>
    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        lastDamageTime = -999f;
    }
}
