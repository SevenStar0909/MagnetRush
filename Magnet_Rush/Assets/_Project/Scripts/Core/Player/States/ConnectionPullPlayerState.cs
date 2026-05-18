using UnityEngine;

public class ConnectionPullPlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player) { }
    protected override void OnExit(Player player) { }

    protected override void OnStep(Player player, float dt)
    {
        player.UpdateAim();
        player.AccelerateToInputDirection(dt);
    }
}
