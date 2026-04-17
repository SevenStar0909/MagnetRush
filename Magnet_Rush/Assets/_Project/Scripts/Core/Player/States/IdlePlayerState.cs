/// <summary>
/// プレイヤーの待機ステート。移動入力で移動ステートに遷移する。
/// </summary>
public class IdlePlayerState : EntityState<Player>
{
    public override void Step(float dt)
    {
        m_entity.SlowDown(dt);

        if (m_entity.input.MoveInput.sqrMagnitude > 0.01f)
        {
            m_manager.Change<MovePlayerState>();
        }
    }
}
