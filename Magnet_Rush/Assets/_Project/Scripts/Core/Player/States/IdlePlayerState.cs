/// <summary>
/// プレイヤーの待機ステート。移動入力で移動ステートに遷移する。
/// </summary>
public class IdlePlayerState : EntityState<Player>
{
    public override void UpdateState(float dt)
    {
        m_entity.SlowDown(dt);
        m_entity.SwitchPole();
        m_entity.HandleAimInput();
        m_entity.Fire();
        m_entity.SelfFire();
        m_entity.Reload();

        if (m_entity.input.MoveInput.sqrMagnitude > 0.01f)
        {
            m_manager.Change<MovePlayerState>();
        }
    }
}
