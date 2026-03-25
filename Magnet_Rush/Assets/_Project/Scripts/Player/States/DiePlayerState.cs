using UnityEngine;

public class DiePlayerState : EntityState<Player>
{
    private float respawnTimer;

    public override void Enter(Player entity, EntityStateManager<Player> manager)
    {
        base.Enter(entity, manager);
        entity.lateralVelocity = Vector3.zero;
        entity.input.enabled = false;

        var collider = entity.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        respawnTimer = entity.Settings.respawnDelay;
    }

    public override void Step(float dt)
    {
        respawnTimer -= dt;
        if (respawnTimer <= 0f)
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
            var cc = entity.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            entity.transform.position = GameManager.Instance.GetSpawnPosition();
            if (cc != null) cc.enabled = true;
        }

        entity.health.ResetHealth();
        entity.lateralVelocity = Vector3.zero;
        entity.verticalVelocity = 0f;
        entity.externalVelocity = Vector3.zero;
        manager.Change<IdlePlayerState>();
    }
}
