using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーの死亡ステート。一定時間後にリスポーンする。
/// Respawn コルーチンが OnEnter のスコープを抜けて走るため、
/// エンティティ参照は m_player フィールドにキャプチャしておく。
/// </summary>
public class DiePlayerState : EntityState<Player>
{
    private Player m_player;

    protected override void OnEnter(Player player)
    {
        m_player = player;

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

        // OnEnter のコールスタック上で Change<IdlePlayerState>() すると
        // EntityStateManager.Change の last/current 代入と OnStateChanged 発火が再入し
        // Die ステートをスキップして購読者に伝わってしまう。1 フレーム遅延で回避。
        player.StartCoroutine(RespawnNextFrame());
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

    private IEnumerator RespawnNextFrame()
    {
        yield return null;
        Respawn();
    }

    private void Respawn()
    {
        if (m_player == null) return;

        // スポーン地点にテレポート
        if (GameManager.Instance != null)
        {
            m_player.transform.position = GameManager.Instance.GetSpawnPosition();
        }

        // velocity を直接リセット（lateralVelocity/verticalVelocity は transform.up 経由の変換なので不完全になりうる）
        m_player.velocity = Vector3.zero;
        m_player.externalVelocity = Vector3.zero;
        m_player.m_health.ResetHealth();
        m_player.states.Change<IdlePlayerState>();
    }
}
