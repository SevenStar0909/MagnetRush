/// <summary>
/// スタブ攻撃モーション中の状態。AnimEvent or タイムアウトで通常状態へ復帰する。
/// PR0 (jump-stab-prep) では空。実装は feature/stab で行う。
/// State-driven 構造により OnStep が空 = 全能力呼び出しスキップ = スタブモーション中の他入力ロックが自動実現される。
/// 基底: EntityState&lt;Player&gt;
/// </summary>
public class StabPlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player) { /* feature/stab で実装する */ }
    protected override void OnExit(Player player)  { /* feature/stab で実装する */ }
    protected override void OnStep(Player player, float dt)
    {
        // 実装は feature/stab で追加する。
        // OnStep を空のままにすることで、State-driven な能力呼び出しが
        // すべてスキップされ、スタブ中の他入力ロックが自動的に実現される。
    }
}
