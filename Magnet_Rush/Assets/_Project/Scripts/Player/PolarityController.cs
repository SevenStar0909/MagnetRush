using System;
using UnityEngine;

/// <summary>
/// Y入力で弾の磁極（S/N）を切り替える。
/// </summary>
public class PolarityController : MonoBehaviour
{
    /// <summary>
    /// 現在の磁極（SまたはN）。
    /// </summary>
    public MagneticPole CurrentPole { get; private set; } = MagneticPole.S;

    /// <summary>
    /// 磁極が切り替わったときに発火するイベント。
    /// </summary>
    public event Action<MagneticPole> OnPolarityChanged;

    private PlayerInputHandler m_input;
    private PlayerEvents m_events;

    void Awake()
    {
        m_input = GetComponent<PlayerInputHandler>();
        m_events = GetComponent<PlayerEvents>();
    }

    void Update()
    {
        if (!m_input.ConsumeSwitchPole()) { ChannelLogger.LogGuardReturn("Player", "極性切替入力なし"); return; }

        CurrentPole = CurrentPole == MagneticPole.S ? MagneticPole.N : MagneticPole.S;
        OnPolarityChanged?.Invoke(CurrentPole);
        m_events?.FirePolaritySwitch(CurrentPole);
    }
}
