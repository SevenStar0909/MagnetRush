/// <summary>
/// スタブ攻撃モーション中の状態。AnimEvent or タイムアウトで通常状態へ復帰する。
/// PR0 (jump-stab-prep) では空。実装は feature/stab で行う。
/// 基底: EntityState&lt;Player&gt;
/// </summary>
public class StabPlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player) { /* feature/stab PR で実装する */ }
    protected override void OnExit(Player player)  { /* feature/stab PR で実装する */ }
    protected override void OnStep(Player player, float dt)
    {
        // 実装は feature/stab で追加する。
    }
}
