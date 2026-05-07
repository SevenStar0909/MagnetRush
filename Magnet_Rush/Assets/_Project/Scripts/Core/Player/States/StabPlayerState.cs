/// <summary>
/// スタブ攻撃モーション中の状態。AnimEvent or タイムアウトで通常状態へ復帰する。
/// PR0 (jump-stab-prep) では空。実装は feature/stab で行う。
/// 基底: EntityState&lt;Player&gt;
/// </summary>
public class StabPlayerState : EntityState<Player>
{
    // 実装は feature/stab で UpdateState を追加する
}
