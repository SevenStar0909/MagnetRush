using System;
using UnityEngine;

/// <summary>
/// Y入力で弾の磁極（S⇔N）を切り替える。
/// </summary>
public class PolarityController : MonoBehaviour
{
    public MagneticPole CurrentPole { get; private set; } = MagneticPole.S;

    public event Action<MagneticPole> OnPolarityChanged;

    private PlayerInputHandler input;
    private PlayerEvents events;

    void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
        events = GetComponent<PlayerEvents>();
    }

    void Update()
    {
        if (!input.ConsumeSwitchPole()) return;

        CurrentPole = CurrentPole == MagneticPole.S ? MagneticPole.N : MagneticPole.S;
        OnPolarityChanged?.Invoke(CurrentPole);
        events?.FirePolaritySwitch(CurrentPole);
    }
}
