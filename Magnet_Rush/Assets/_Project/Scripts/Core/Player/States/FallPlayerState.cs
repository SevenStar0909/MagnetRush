/// <summary>
/// 空中状態（ジャンプ経由・段差落下経由両対応）。
/// PR0 (jump-stab-prep) では空。実装は feature/jump で行う。
/// 基底: EntityState&lt;Player&gt;
/// </summary>
public class FallPlayerState : EntityState<Player>
{
    // 実装は feature/jump で UpdateState を追加する
}
