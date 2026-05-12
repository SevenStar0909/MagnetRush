/// <summary>
/// エイム状態。カメラ方向を向きながらストレイフ移動する。
/// </summary>
public class AimPlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player) { }
    protected override void OnExit(Player player) { }

    protected override void OnStep(Player player, float dt)
    {
        player.TickAllAbilities();

        // ストレイフ移動：カメラ方向を向き、速度半減
        player.MoveWithInputStrafe(dt);

        if (player.input.MoveInput.sqrMagnitude < 0.01f)
        {
            player.SlowDown(dt);
        }
        // 空中でも Aim 維持する。重力は Player.UpdateEntity 経由で適用される。
        // Aim 解除時 (LT release) の遷移は AimAbility.StopAim() が IsGrounded で分岐する。
    }
}
