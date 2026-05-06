using System;
using UnityEngine;

/// <summary>
/// 磁極制御コンポーネント。Y 入力で S/N を切り替え、UI 等へイベント通知する。
/// 依存: PlayerInputHandler, PlayerEvents（同 GameObject 上）
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
public class PoleController : MonoBehaviour
{
    /// <summary>現在の磁極（S または N）。</summary>
    public MagneticPole CurrentPole { get; private set; } = MagneticPole.S;

    /// <summary>磁極切替時に発火。UI 等が購読する。</summary>
    public event Action<MagneticPole> OnPoleChanged;

    private PlayerInputHandler m_input;
    private PlayerEvents m_events;

    void Awake()
    {
        m_input = GetComponent<PlayerInputHandler>();
        m_events = GetComponent<PlayerEvents>();
    }

    private int m_dbgCallCount;
    private float m_dbgLastReport;

    /// <summary>Y 入力があれば磁極を切り替える。毎フレーム呼ぶ前提。</summary>
    public void Switch()
    {
        m_dbgCallCount++;
        if (Time.time - m_dbgLastReport > 2f)
        {
            Debug.Log($"[PoleController-DEBUG] Switch() 呼び出し回数（直近2秒）={m_dbgCallCount}, m_input={(m_input != null ? "OK" : "null")}, IsPressed={m_input?.IsSwitchPolePressed}", this);
            m_dbgCallCount = 0;
            m_dbgLastReport = Time.time;
        }

        if (!m_input.IsSwitchPolePressed) return;
        Debug.Log($"[PoleController-DEBUG] Q入力検出 → 切替開始 (現在={CurrentPole})", this);
        m_input.ConsumeSwitchPole();
        CurrentPole = CurrentPole == MagneticPole.S ? MagneticPole.N : MagneticPole.S;
        Debug.Log($"[PoleController-DEBUG] 切替後={CurrentPole}, OnPoleChanged subs={(OnPoleChanged != null ? OnPoleChanged.GetInvocationList().Length : 0)}", this);
        OnPoleChanged?.Invoke(CurrentPole);
        m_events.FirePoleSwitch();
    }
}
