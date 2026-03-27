using UnityEngine;

/// <summary>
/// エンティティステートの基底クラス（MonoBehaviourではない）。
/// </summary>
public abstract class EntityState<T> where T : Entity
{
    protected T entity;
    protected EntityStateManager<T> manager;

    /// <summary>現在のステートに入ってからの経過時間。</summary>
    public float timeSinceEntered { get; set; }

    public virtual void Enter(T entity, EntityStateManager<T> manager)
    {
        this.entity = entity;
        this.manager = manager;
        timeSinceEntered = 0f;
    }

    public virtual void Exit() { }

    public virtual void Step(float dt) { }

    /// <summary>エンティティが他のColliderと接触した時に呼ばれる。</summary>
    public virtual void OnContact(Collider other) { }
}
