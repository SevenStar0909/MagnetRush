using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// エンティティステートを管理する。ステートはRegisterState()で登録される
/// </summary>
public class EntityStateManager<T> : MonoBehaviour where T : Entity
{
    private readonly Dictionary<Type, EntityState<T>> m_states = new();
    private T m_entity;

    public EntityState<T> current { get; private set; }
    public EntityState<T> last { get; private set; }

    /// <summary>
    /// 全ステート登録後にサブクラスのAwake()から呼び出す。
    /// </summary>
    public void Initialize(T entity)
    {
        m_entity = entity;
    }

    /// <summary>
    /// ステートを辞書に登録する。
    /// </summary>
    public void RegisterState(EntityState<T> state)
    {
        m_states[state.GetType()] = state;
    }

    /// <summary>ステート進入時に発火。引数は新ステートのType。</summary>
    public event Action<Type> OnStateEnter;

    /// <summary>ステート退出時に発火。引数は旧ステートのType。</summary>
    public event Action<Type> OnStateExit;

    /// <summary>ステート変更時に発火。</summary>
    public event Action OnStateChanged;

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
            OnStateExit?.Invoke(current.GetType());
            last = current;
        }

        current = next;
        current.Enter(m_entity, this);
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

    /// <summary>
    /// 現在のステートが指定した型かどうかを返す。
    /// </summary>
    public bool IsCurrentOfType<TState>() where TState : EntityState<T>
    {
        return current != null && current.GetType() == typeof(TState);
    }
}
