using MagnetRush.Entity;

namespace MagnetRush.Player.States
{
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
}