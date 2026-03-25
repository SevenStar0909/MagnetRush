/// <summary>
/// エンティティステートの基底クラス。純粋なC#オブジェクト（MonoBehaviourではない）。
/// </summary>
public abstract class EntityState<T> where T : Entity
{
    protected T entity;
    protected EntityStateManager<T> manager;

    public virtual void Enter(T entity, EntityStateManager<T> manager)
    {
        this.entity = entity;
        this.manager = manager;
    }

    public virtual void Exit() { }

    public virtual void Step(float dt) { }
}
