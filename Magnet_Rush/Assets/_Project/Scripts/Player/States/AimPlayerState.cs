using MagnetRush.Entity;

namespace MagnetRush.Player.States
{
    /// <summary>
    /// エイム状態。カメラ方向を向きながらストレイフ移動する。
    /// </summary>
    public class AimPlayerState : EntityState<Player>
    {
        public override void Step(float dt)
        {
            // ストレイフ移動：カメラ方向を向き、速度半減
            entity.MoveWithInputStrafe(dt);

            if (entity.input.MoveInput.sqrMagnitude < 0.01f)
            {
                entity.SlowDown(dt);
            }
        }
    }
}
