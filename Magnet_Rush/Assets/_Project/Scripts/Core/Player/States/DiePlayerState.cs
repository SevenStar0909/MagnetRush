using UnityEngine;

/// <summary>
/// プレイヤーの死亡ステート。
/// 入力・コライダー・速度を抑止するだけで、リスポーンの timing と実処理は持たない。
/// 死亡 → 待機 → リスポーン演出のオーケストレーションは GameManager が担当する。
/// 実際のリセットは Player.Respawn() に集約されている。
/// </summary>
public class DiePlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player)
    {
        player.velocity = Vector3.zero;
        player.externalVelocity = Vector3.zero;
        player.input.ClearBuffers();
        player.input.enabled = false;

        var controller = player.GetComponent<EntityController>();
        if (controller != null)
        {
            var controllerCollider = controller.GetComponent<Collider>();
            if (controllerCollider != null) controllerCollider.enabled = false;
        }
    }

    protected override void OnExit(Player player)
    {
        player.input.enabled = true;

        var controller = player.GetComponent<EntityController>();
        if (controller != null)
        {
            var controllerCollider = controller.GetComponent<Collider>();
            if (controllerCollider != null) controllerCollider.enabled = true;
        }
    }

    protected override void OnStep(Player player, float dt) { }
}
