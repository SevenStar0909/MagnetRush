/// <summary>
/// 磁極の有無と極性を問い合わせるための軽量インターフェース。
/// Magnetizable が実装し、Magnet asmdef を参照できない下位モジュール
/// (Entity の EntityController 等) から磁気状態を判定できるようにする。
/// </summary>
public interface IMagnetPoleProvider
{
    MagneticPole Pole { get; }
    bool IsActive { get; }
}
