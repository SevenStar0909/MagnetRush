/// <summary>
/// プレイヤーの移動ステート。入力がなくなると待機ステートに遷移する。
/// </summary>
public class MovePlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player) { }
    protected override void OnExit(Player player) { }

    protected override void OnStep(Player player, float dt)
    {
        player.TickAllAbilities();
        player.AccelerateToInputDirection(dt);

        if (player.input.MoveInput.sqrMagnitude < 0.01f)
        {
            player.states.Change<IdlePlayerState>();
        }

        if (!player.IsGrounded)
        {
            player.states.Change<FallPlayerState>();
        }
    }
}
