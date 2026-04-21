/// <summary>
/// プレイヤーの移動ステート。入力がなくなると待機ステートに遷移する。
/// </summary>
public class MovePlayerState : EntityState<Player>
{
    public override void UpdateState(float dt)
    {
        m_entity.AccelerateToInputDirection(dt);
        m_entity.SwitchPole();
        m_entity.HandleAimInput();
        m_entity.Fire();
        m_entity.SelfFire();
        m_entity.Reload();

        if (m_entity.input.MoveInput.sqrMagnitude < 0.01f)
        {
            m_manager.Change<IdlePlayerState>();
        }
    }
}
