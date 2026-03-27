/// <summary>
/// プレイヤーの移動ステート。入力がなくなると待機ステートに遷移する。
/// </summary>
public class MovePlayerState : EntityState<Player>
{
    public override void Step(float dt)
    {
        entity.MoveWithInput(dt);

        if (entity.input.MoveInput.sqrMagnitude < 0.01f)
        {
            manager.Change<IdlePlayerState>();
        }
    }
}
