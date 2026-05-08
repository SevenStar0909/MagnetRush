/// <summary>
/// 空中状態（ジャンプ経由・段差落下経由両対応）。
/// PR0 (jump-stab-prep) では空。実装は feature/jump で行う。
/// 基底: EntityState&lt;Player&gt;
/// </summary>
public class FallPlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player) { /* feature/jump で実装する */ }
    protected override void OnExit(Player player)  { /* feature/jump で実装する */ }
    protected override void OnStep(Player player, float dt)
    {
        // feature/jump で player.TickAllAbilities() 等を追加する。
        // 空中ジャンプは JumpAbility 内部の IsGrounded ガードで自動的に no-op になるため、
        // TickAllAbilities() で全許可しても二重ジャンプは発動しない。
    }
}
