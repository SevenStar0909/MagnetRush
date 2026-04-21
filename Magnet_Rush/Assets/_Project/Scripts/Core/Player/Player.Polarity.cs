using System;

/// <summary>
/// Player の磁極制御部分（partial）。
/// Y 入力で S/N を切り替え、UI がイベント購読する。
/// </summary>
public partial class Player
{
    // --- 磁極制御 ---

    /// <summary>現在の磁極（S または N）。</summary>
    public MagneticPole CurrentPole { get; private set; } = MagneticPole.S;

    /// <summary>磁極切替時に発火。UI 等が購読。</summary>
    public event Action<MagneticPole> OnPolarityChanged;

    /// <summary>
    /// Y 入力があれば磁極を切り替える。毎フレーム呼ぶ前提。
    /// </summary>
    public void SwitchPole()
    {
        if (!input.ConsumeSwitchPole()) return;
        CurrentPole = CurrentPole == MagneticPole.S ? MagneticPole.N : MagneticPole.S;
        OnPolarityChanged?.Invoke(CurrentPole);
        events?.FirePolaritySwitch();
    }
}
