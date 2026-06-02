using System;
using UnityEngine;

/// <summary>
/// 体幹ゲージ（スタミナ）管理。
/// 消費（被弾等）後、一定時間経過すると自動回復する。
/// </summary>
public class Stamina : MonoBehaviour
{
    private int m_maxStamina = 10;
    private float m_recoveryPerSecond = 0f;
    private float m_recoveryCooldown = 0f;

    public int MaxStamina => m_maxStamina;
    public int CurrentStamina { get; private set; }
    public float StaminaRatio => m_maxStamina > 0 ? (float)CurrentStamina / m_maxStamina : 0f;

    public bool IsBroken => CurrentStamina <= 0;
    public bool IsRecoveryCoolingDown => Time.time < m_lastConsumeTime + m_recoveryCooldown;

    private float m_lastConsumeTime = -999f;
    private float m_recoveryBuffer;
    private bool m_wasBroken;

    public event Action<int> OnConsume;
    public event Action<int> OnRecover;
    public event Action OnBreak;

    void Awake()
    {
        if (m_maxStamina <= 0)
            m_maxStamina = 1;

        CurrentStamina = m_maxStamina;
        m_wasBroken = IsBroken;
    }

    void Update()
    {
        if (m_recoveryPerSecond <= 0f) return;
        if (m_maxStamina <= 0) return;
        if (IsRecoveryCoolingDown) return;
        if (CurrentStamina >= m_maxStamina) return;

        m_recoveryBuffer += m_recoveryPerSecond * Time.deltaTime;

        int amount = Mathf.FloorToInt(m_recoveryBuffer);
        if (amount <= 0) return;

        m_recoveryBuffer -= amount;
        Recover(amount);
    }

    public void SetMaxStamina(int maxStamina, bool resetCurrent = true)
    {
        if (maxStamina <= 0)
        {
            ChannelLogger.LogGuardReturn("Entity", "maxStamina が 0 以下です");
            return;
        }

        m_maxStamina = maxStamina;

        if (resetCurrent)
            CurrentStamina = m_maxStamina;
        else
            CurrentStamina = Mathf.Min(CurrentStamina, m_maxStamina);
    }

    public void SetRecovery(float recoveryPerSecond, float recoveryCooldown)
    {
        if (recoveryPerSecond < 0f)
        {
            ChannelLogger.LogGuardReturn("Entity", "recoveryPerSecond が 0 未満です");
            recoveryPerSecond = 0f;
        }

        if (recoveryCooldown < 0f)
        {
            ChannelLogger.LogGuardReturn("Entity", "recoveryCooldown が 0 未満です");
            recoveryCooldown = 0f;
        }

        m_recoveryPerSecond = recoveryPerSecond;
        m_recoveryCooldown = recoveryCooldown;
    }

    /// <summary>
    /// スタミナを消費する（被弾など）。
    /// 消費した時刻を記録し、回復クールダウンを開始する。
    /// </summary>
    public void Consume(int amount)
    {
        if (amount <= 0) { ChannelLogger.LogGuardReturn("Entity", "消費量0以下"); return; }
        if (m_maxStamina <= 0) { ChannelLogger.LogGuardReturn("Entity", "maxStamina が 0 以下"); return; }

        CurrentStamina = Mathf.Max(CurrentStamina - amount, 0);

        m_lastConsumeTime = Time.time;
        m_recoveryBuffer = 0f;

        OnConsume?.Invoke(amount);

        bool brokenNow = IsBroken;
        if (!m_wasBroken && brokenNow)
            OnBreak?.Invoke();

        m_wasBroken = brokenNow;
    }

    /// <summary>スタミナを回復する。最大値を超えない。</summary>
    public void Recover(int amount)
    {
        if (amount <= 0) { ChannelLogger.LogGuardReturn("Entity", "回復量0以下"); return; }
        if (m_maxStamina <= 0) { ChannelLogger.LogGuardReturn("Entity", "maxStamina が 0 以下"); return; }

        int prev = CurrentStamina;
        CurrentStamina = Mathf.Min(CurrentStamina + amount, m_maxStamina);

        int delta = CurrentStamina - prev;
        if (delta > 0)
            OnRecover?.Invoke(delta);

        m_wasBroken = IsBroken;
    }

    public void ResetStamina()
    {
        CurrentStamina = m_maxStamina;
        m_lastConsumeTime = -999f;
        m_recoveryBuffer = 0f;
        m_wasBroken = IsBroken;
    }
}