/// <summary>
/// プレイヤーの待機ステート。移動入力で移動ステートに遷移する。
/// </summary>
public class IdlePlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player) { }
    protected override void OnExit(Player player) { }

    protected override void OnStep(Player player, float dt)
    {
        player.TickAllAbilities();
        player.SlowDown(dt);

        if (player.input.MoveInput.sqrMagnitude > 0.01f)
        {
            player.states.Change<MovePlayerState>();
        }

        if(!player.IsGrounded)
        {
            player.states.Change<FallPlayerState>();
        }
    }
}
