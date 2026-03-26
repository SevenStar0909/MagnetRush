public class IdlePlayerState : EntityState<Player>
{
    public override void Step(float dt)
    {
        entity.SlowDown(dt);

        // 磁力場による壁面歩行遷移
        if (entity.magnetField != null && !entity.magnetField.IsDestroyed
            && entity.magnetField.GetStrengthAt(entity.transform.position) > 0.3f)
        {
            manager.Change<MagnetWalkPlayerState>();
            return;
        }

        if (entity.input.MoveInput.sqrMagnitude > 0.01f)
        {
            manager.Change<MovePlayerState>();
        }
    }
}
