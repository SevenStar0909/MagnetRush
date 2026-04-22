/// <summary>
/// プレイヤーの移動ステート。入力がなくなると待機ステートに遷移する。
/// </summary>
public class MovePlayerState : EntityState<Player>
{
    public override void UpdateState(float dt)
    {
        m_entity.AccelerateToInputDirection(dt);
        m_entity.pole.Switch();
        m_entity.aim.HandleAimInput();
        m_entity.shooting.Fire();
        m_entity.shooting.SelfFire();
        m_entity.shooting.Reload();

        if (m_entity.input.MoveInput.sqrMagnitude < 0.01f)
        {
            m_manager.Change<IdlePlayerState>();
        }
    }
}
