using MagnetRush.Entity;

namespace MagnetRush.Player.States
{
    public class IdlePlayerState : EntityState<Player>
    {
        public override void Step(float dt)
        {
            entity.SlowDown(dt);

            if (entity.input.MoveInput.sqrMagnitude > 0.01f)
            {
                manager.Change<MovePlayerState>();
            }
        }
    }
}