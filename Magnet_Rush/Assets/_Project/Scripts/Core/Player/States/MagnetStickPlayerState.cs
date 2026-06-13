using UnityEngine;

/// <summary>
/// 磁力で壁・地面に張り付いて静止する状態（ビタ止め）。
/// 止まるのは位置（重力・移動）だけで、向き変更・射撃・エイム等の操作は通常どおり使える。
/// 本ステート中は Player.Update が UpdateEntity をスキップするため位置が完全に固定される。
/// 磁力が切れるか、ジャンプ入力（跳ばずに解除のみ）で剥がれて FallPlayerState に戻る。
/// 基底: EntityState&lt;Player&gt;
/// </summary>
public class MagnetStickPlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player)
    {
        player.velocity = Vector3.zero;
        player.externalVelocity = Vector3.zero;
        player.holdVelocity = Vector3.zero;
    }

    protected override void OnExit(Player player)
    {
        // 張り付き中に磁力が蓄積していても、剥がれた瞬間に吹っ飛ばないようにゼロクリア
        player.velocity = Vector3.zero;
        player.externalVelocity = Vector3.zero;
        player.holdVelocity = Vector3.zero;
    }

    protected override void OnStep(Player player, float dt)
    {
        // 磁力が切れたら解除して空中状態に戻る
        if (player.magnetizable == null || !player.magnetizable.IsActive)
        {
            ChannelLogger.LogGuardReturn("Player", "磁力切れで張り付き解除");
            player.states.Change<FallPlayerState>();
            return;
        }

        // ジャンプ入力で剥がれる（跳びはしない＝上方向の初速は与えない）。
        // 自己磁化が残っていると次フレームで再張り付きするので、MagnetFieldをForceExpireして極・オーラごと解除する。
        // TickAllAbilities より先に判定しないと JumpAbility 側にバッファを消費される
        if (player.input.ConsumeJump())
        {
            foreach (var field in player.GetComponentsInChildren<MagnetField>(true))
            {
                if (field != null) field.ForceExpire();
            }

            player.states.Change<FallPlayerState>();
            return;
        }

        // 位置は固定のまま操作は通常どおり受け付ける。
        // UpdateEntity がスキップされているので速度は位置に反映されず、向き変更（FaceDirection）だけが効く
        player.AccelerateToInputDirection(dt);
        player.TickAllAbilities();
    }
}
