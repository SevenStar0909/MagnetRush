/// <summary>
/// プレイヤーの移動ステート。入力がなくなると待機ステートに遷移する。
/// </summary>
public class MovePlayerState : EntityState<Player>
{
    public override void Step(float dt)
    {
        m_entity.MoveWithInput(dt);

        if (m_entity.input.MoveInput.sqrMagnitude < 0.01f)
        {
            m_manager.Change<IdlePlayerState>();
        }
    }
}
