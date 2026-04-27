using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーの死亡ステート。一定時間後にリスポーンする。
/// </summary>
public class DiePlayerState : EntityState<Player>
{
    public override void Enter(Player entity, EntityStateManager<Player> manager)
    {
        base.Enter(entity, manager);

        entity.velocity = UnityEngine.Vector3.zero;
        entity.externalVelocity = UnityEngine.Vector3.zero;
        entity.input.ClearBuffers();
        entity.input.enabled = false;

        var controller = entity.GetComponent<EntityController>();
        if (controller != null && controller.collider != null)
            controller.collider.enabled = false;

        // Enter() のコールスタック上で Change<IdlePlayerState>() すると
        // EntityStateManager.Change の last/current 代入と OnStateChanged 発火が再入し
        // Die ステートをスキップして購読者に伝わってしまう。1 フレーム遅延で回避。
        entity.StartCoroutine(RespawnNextFrame());
    }

    public override void UpdateState(float dt) { }

    public override void Exit()
    {
        m_entity.input.enabled = true;

        var controller = m_entity.GetComponent<EntityController>();
        if (controller != null && controller.collider != null)
            controller.collider.enabled = true;
    }

    private IEnumerator RespawnNextFrame()
    {
        yield return null;
        Respawn();
    }

    private void Respawn()
    {
        // スポーン地点にテレポート
        if (GameManager.Instance != null)
        {
            m_entity.transform.position = GameManager.Instance.GetSpawnPosition();
        }

        // velocityを直接リセット（lateralVelocity/verticalVelocityはtransform.up経由の変換なので不完全になりうる）
        m_entity.velocity = UnityEngine.Vector3.zero;
        m_entity.externalVelocity = UnityEngine.Vector3.zero;
        m_entity.m_health.ResetHealth();
        m_manager.Change<IdlePlayerState>();
    }
}
