/// <summary>
/// エイム状態。カメラ方向を向きながらストレイフ移動する。
/// </summary>
public class AimPlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player) { }
    protected override void OnExit(Player player) { }

    protected override void OnStep(Player player, float dt)
    {
        // ストレイフ移動：カメラ方向を向き、速度半減
        player.MoveWithInputStrafe(dt);

        if (player.input.MoveInput.sqrMagnitude < 0.01f)
        {
            player.SlowDown(dt);
        }
    }
}
