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

    /// <summary>Y 入力があれば磁極を切り替える。毎フレーム呼ぶ前提。</summary>
    public void Switch()
    {
        if (!m_input.ConsumeSwitchPole()) return;
        CurrentPole = CurrentPole == MagneticPole.S ? MagneticPole.N : MagneticPole.S;
        OnPoleChanged?.Invoke(CurrentPole);
        m_events.FirePoleSwitch();
    }
}
