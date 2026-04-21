/// <summary>
/// プレイヤーの待機ステート。移動入力で移動ステートに遷移する。
/// </summary>
public class IdlePlayerState : EntityState<Player>
{
    public override void UpdateState(float dt)
    {
        m_entity.SlowDown(dt);
        m_entity.pole.Switch();
        m_entity.aim.HandleAimInput();
        m_entity.shooting.Fire();
        m_entity.shooting.SelfFire();
        m_entity.shooting.Reload();

        if (m_entity.input.MoveInput.sqrMagnitude > 0.01f)
        {
            m_manager.Change<MovePlayerState>();
        }
    }
}
