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
/// エンティティステートを管理する。サブクラスが GetStateList() で使用する State 一覧を返す。
/// 初期化は Start() で自動実行（entity = GetComponent&lt;T&gt;()、リスト登録、先頭ステートへ遷移）。
/// </summary>
public abstract class EntityStateManager<T> : EntityStateManagerBase where T : Entity
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
    /// サブクラスが返すステートリスト。Inspector 駆動なら CreateListFromStringArray を使う。
    /// 固定 new 登録のサブクラスは new List を直接返す。
    /// </summary>
    protected abstract List<EntityState<T>> GetStateList();

    protected virtual void Start()
    {
        m_entity = GetComponent<T>();
        if (m_entity == null)
        {
            Debug.LogError($"[{GetType().Name}] {typeof(T).Name} コンポーネントが見つかりません。", this);
            return;
        }

        var list = GetStateList();
        foreach (var state in list)
        {
            RegisterState(state);
        }

        if (m_list.Count > 0)
        {
            Change(m_list[0].GetType());
        }
    }

    /// <summary>ステートを辞書とリストに登録する。同じ型の二重登録は無視する。</summary>
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
        Change(typeof(TState));
    }

    /// <summary>
    /// 型を直接指定してステートに遷移する。GetStateList や Reflection 経由の遷移に使う。
    /// </summary>
    public void Change(Type type)
    {
        if (!m_states.TryGetValue(type, out var next))
        {
            Debug.LogError($"State {type.Name} が登録されていません。");
            return;
        }

        if (current != null)
        {
            current.Exit(m_entity);
            InvokeStateExit(current.GetType());
            last = current;
        }

        current = next;
        current.Enter(m_entity);
        InvokeStateEnter(current.GetType());
        InvokeStateChanged();
    }

    /// <summary>
    /// 現在ステートのUpdateStateを実行する。timeSinceEntered の加算は基底 EntityState&lt;T&gt;.UpdateState 側で行う。
    /// </summary>
    public void UpdateState(float dt)
    {
        if (current != null)
        {
            current.UpdateState(m_entity, dt);
        }
    }

    /// <summary>
    /// 現在ステートにコリジョン通知を送る。
    /// </summary>
    public void OnContact(Collider other)
    {
        if (current != null) current.OnContact(m_entity, other);
    }

    /// <summary>
    /// 現在のステートが指定した型かどうかを返す。
    /// </summary>
    public bool IsCurrentOfType<TState>() where TState : EntityState<T>
    {
        return current != null && current.GetType() == typeof(TState);
    }

    /// <summary>指定型のステートが登録されているか返す。</summary>
    public bool ContainsStateOfType(Type type)
    {
        return type != null && m_states.ContainsKey(type);
    }

    /// <summary>
    /// 文字列配列（クラス名 + アセンブリ名）から State リストを生成する。
    /// Inspector 駆動のサブクラスで GetStateList から呼ぶヘルパー。
    /// </summary>
    protected static List<EntityState<T>> CreateListFromStringArray(string[] array)
    {
        var list = new List<EntityState<T>>();
        if (array == null) return list;

        foreach (var typeName in array)
        {
            if (string.IsNullOrEmpty(typeName)) continue;
            var type = Type.GetType(typeName);
            if (type == null)
            {
                Debug.LogError($"型 '{typeName}' が見つかりません。アセンブリ名を確認してください。");
                continue;
            }
            var instance = Activator.CreateInstance(type) as EntityState<T>;
            if (instance == null)
            {
                Debug.LogError($"'{typeName}' を EntityState<{typeof(T).Name}> として生成できませんでした。");
                continue;
            }
            list.Add(instance);
        }
        return list;
    }
}
