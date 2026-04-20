using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// EntityStateManagerの非ジェネリック基底クラス。
/// EntityStateManagerListenerがイベントを購読するために使用。
/// </summary>
public abstract class EntityStateManagerBase : MonoBehaviour
{
    /// <summary>ステート進入時に発火。引数は新ステートのType。</summary>
    public event Action<Type> OnStateEnter;

    /// <summary>ステート退出時に発火。引数は旧ステートのType。</summary>
    public event Action<Type> OnStateExit;

    /// <summary>ステート変更時に発火。</summary>
    public event Action OnStateChanged;

    /// <summary>
    /// Inspectorから接続可能なステート変化イベント。
    /// コード購読はOnStateChangedを使うこと。
    /// </summary>
    [Tooltip("Inspectorから接続可能なステート変化イベント。コード購読はOnStateChangedを使う")]
    public UnityEvent onStateChanged;

    protected void InvokeStateEnter(Type type) => OnStateEnter?.Invoke(type);
    protected void InvokeStateExit(Type type) => OnStateExit?.Invoke(type);
    protected void InvokeStateChanged()
    {
        OnStateChanged?.Invoke();
        onStateChanged?.Invoke();
    }
}

/// <summary>
/// エンティティステートを管理する。ステートはRegisterState()で登録される。
/// </summary>
public class EntityStateManager<T> : EntityStateManagerBase where T : Entity
{
    private readonly Dictionary<Type, EntityState<T>> m_states = new();
    private readonly List<EntityState<T>> m_list = new();
    private T m_entity;

    public EntityState<T> current { get; private set; }
    public EntityState<T> last { get; private set; }

    /// <summary>現在ステートの登録順 index。未登録 or 未初期化時は -1。</summary>
    public int index => current != null ? m_list.IndexOf(current) : -1;

    /// <summary>前回ステートの登録順 index。未初期化時は -1。</summary>
    public int lastIndex => last != null ? m_list.IndexOf(last) : -1;

    /// <summary>
    /// 全ステート登録後にサブクラスのAwake()から呼び出す。
    /// </summary>
    public void Initialize(T entity)
    {
        m_entity = entity;
    }

    /// <summary>
    /// ステートを辞書とリストに登録する。同じ型の二重登録は無視する。
    /// </summary>
    public void RegisterState(EntityState<T> state)
    {
        var type = state.GetType();
        if (m_states.ContainsKey(type)) return;
        m_states[type] = state;
        m_list.Add(state);
    }

    /// <summary>
    /// 指定したステートに遷移する。現在ステートのExit→新ステートのEnterを実行する。
    /// </summary>
    public void Change<TState>() where TState : EntityState<T>
    {
        var type = typeof(TState);
        if (!m_states.TryGetValue(type, out var next))
        {
            Debug.LogError($"State {type.Name} not registered.");
            return;
        }

        if (current != null)
        {
            current.Exit();
            InvokeStateExit(current.GetType());
            last = current;
        }

        current = next;
        current.Enter(m_entity, this);
        InvokeStateEnter(current.GetType());
        InvokeStateChanged();
    }

    /// <summary>
    /// 現在ステートのStepを実行し、timeSinceEnteredを更新する。
    /// </summary>
    public void Step(float dt)
    {
        if (current != null)
        {
            current.Step(dt);
            current.timeSinceEntered += dt;
        }
    }

    /// <summary>
    /// 現在ステートにコリジョン通知を送る。
    /// </summary>
    public void OnContact(Collider other)
    {
        if (current != null)
        {
            current.OnContact(other);
        }
    }

    /// <summary>
    /// 現在のステートが指定した型かどうかを返す。
    /// </summary>
    public bool IsCurrentOfType<TState>() where TState : EntityState<T>
    {
        return current != null && current.GetType() == typeof(TState);
    }
}
