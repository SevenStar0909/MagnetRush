using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// エンティティステートを管理する。ステートはRegisterState()で登録される
/// </summary>
public class EntityStateManager<T> : MonoBehaviour where T : Entity
{
    private readonly Dictionary<Type, EntityState<T>> states = new();
    private T entity;

    public EntityState<T> current { get; private set; }
    public EntityState<T> last { get; private set; }

    /// <summary>
    /// 全ステート登録後にサブクラスのAwake()から呼び出す。
    /// </summary>
    public void Initialize(T entity)
    {
        this.entity = entity;
    }

    public void RegisterState(EntityState<T> state)
    {
        states[state.GetType()] = state;
    }

    /// <summary>ステート進入時に発火。引数は新ステートのType。</summary>
    public event Action<Type> OnStateEnter;

    /// <summary>ステート退出時に発火。引数は旧ステートのType。</summary>
    public event Action<Type> OnStateExit;

    /// <summary>ステート変更時に発火。</summary>
    public event Action OnStateChanged;

    public void Change<TState>() where TState : EntityState<T>
    {
        var type = typeof(TState);
        if (!states.TryGetValue(type, out var next))
        {
            Debug.LogError($"State {type.Name} not registered.");
            return;
        }

        if (current != null)
        {
            current.Exit();
            OnStateExit?.Invoke(current.GetType());
            last = current;
        }

        current = next;
        current.Enter(entity, this);
        OnStateEnter?.Invoke(current.GetType());
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 現在ステートのStepを実行し、timeSinceEnteredを更新する。
    /// Player.Update()から呼び出す。
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

    public bool IsCurrentOfType<TState>() where TState : EntityState<T>
    {
        return current != null && current.GetType() == typeof(TState);
    }
}
