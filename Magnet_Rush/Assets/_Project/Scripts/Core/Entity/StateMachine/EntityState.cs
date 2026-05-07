using UnityEngine;

/// <summary>
/// エンティティステートの基底クラス（MonoBehaviourではない）。
/// Template Method パターン: Enter/Exit/UpdateState/OnContact は基底側で固定、
/// 派生は OnEnter/OnExit/OnStep/(必要なら) OnContact だけを実装する。
/// PLAYER TWO Platformer Project の EntityState&lt;T&gt; と同じ構造。
/// </summary>
public abstract class EntityState<T> where T : Entity
{
    /// <summary>現在のステートに入ってからの経過時間。基底側の UpdateState で自動加算される。</summary>
    public float timeSinceEntered { get; private set; }

    public void Enter(T entity)
    {
        timeSinceEntered = 0f;
        OnEnter(entity);
    }

    public void Exit(T entity)
    {
        OnExit(entity);
    }

    public void UpdateState(T entity, float dt)
    {
        OnStep(entity, dt);
        timeSinceEntered += dt;
    }

    public void OnContact(T entity, Collider other)
    {
        OnContactImpl(entity, other);
    }

    /// <summary>進入時のフック。派生で必須実装。</summary>
    protected abstract void OnEnter(T entity);

    /// <summary>退出時のフック。派生で必須実装。</summary>
    protected abstract void OnExit(T entity);

    /// <summary>毎フレームのフック。派生で必須実装。dt は Player.Update() のスロー時/通常時切り替えに合わせてクランプ済みの値が渡る。</summary>
    protected abstract void OnStep(T entity, float dt);

    /// <summary>コリジョン通知のフック。デフォルトは何もしない。必要な派生だけ override する。</summary>
    // 公開側 OnContact を非 virtual に保ち、将来基底側でフック前処理を挟める余地を残す
    protected virtual void OnContactImpl(T entity, Collider other) { }
}
