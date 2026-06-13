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

        // 磁力で引かれて壁・地面に密着したらビタ止め状態へ
        if (player.CanMagnetStick())
        {
            player.states.Change<MagnetStickPlayerState>();
            return;
        }

        player.AccelerateToInputDirection(dt);
        player.TickAllAbilities();

        if(player.IsGrounded)
        {
            if (player.input.MoveInput.sqrMagnitude > 0.01f)
            {
                player.states.Change<MovePlayerState>();
            }
            else
            {
                player.states.Change<IdlePlayerState>();
            }
        }
    }
}
