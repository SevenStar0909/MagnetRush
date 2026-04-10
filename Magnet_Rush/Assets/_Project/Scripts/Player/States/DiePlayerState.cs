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

        // 全速度を即座にゼロ化（重力蓄積を防ぐ）
        entity.velocity = UnityEngine.Vector3.zero;
        entity.externalVelocity = UnityEngine.Vector3.zero;
        entity.input.enabled = false;

        var collider = entity.GetComponent<UnityEngine.Collider>();
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

        // velocityを直接リセット（lateralVelocity/verticalVelocityはtransform.up経由の変換なので不完全になりうる）
        entity.velocity = UnityEngine.Vector3.zero;
        entity.externalVelocity = UnityEngine.Vector3.zero;
        entity.health.ResetHealth();
        manager.Change<IdlePlayerState>();
    }
}
