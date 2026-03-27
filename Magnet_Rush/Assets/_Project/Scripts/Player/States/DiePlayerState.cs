using UnityEngine;

/// <summary>
/// プレイヤーの死亡ステート。一定時間後にリスポーンする。
/// </summary>
public class DiePlayerState : EntityState<Player>
{
    private float m_respawnTimer;

    public override void Enter(Player entity, EntityStateManager<Player> manager)
    {
        base.Enter(entity, manager);
        entity.lateralVelocity = Vector3.zero;
        entity.input.enabled = false;

        var collider = entity.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        m_respawnTimer = entity.Settings.respawnDelay;
    }

    public override void Step(float dt)
    {
        m_respawnTimer -= dt;
        if (m_respawnTimer <= 0f)
        {
            Respawn();
        }
    }

    public override void Exit()
    {
        entity.input.enabled = true;

        var collider = entity.GetComponent<Collider>();
        if (collider != null) collider.enabled = true;
    }

    private void Respawn()
    {
        // スポーン地点にテレポート
        if (GameManager.Instance != null)
        {
            entity.transform.position = GameManager.Instance.GetSpawnPosition();
        }

        entity.health.ResetHealth();
        entity.lateralVelocity = Vector3.zero;
        entity.verticalVelocity = 0f;
        entity.externalVelocity = Vector3.zero;
        manager.Change<IdlePlayerState>();
    }
}
