using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 指定したステートへの遷移時にUnityEventを発火する。
/// コード不要でVFX/SE/アニメーションをステート遷移に接続できる。
/// </summary>
public class EntityStateManagerListener : MonoBehaviour
{
    [Tooltip("監視するステートのクラス名（例: MovePlayerState）")]
    public List<string> states;

    [Tooltip("指定ステートに入った時に発火")]
    public UnityEvent onEnter;

    [Tooltip("指定ステートから出た時に発火")]
    public UnityEvent onExit;

    private EntityStateManagerBase m_manager;

    void Start()
    {
        m_manager = GetComponentInParent<EntityStateManagerBase>();
        if (m_manager != null)
        {
            m_manager.OnStateEnter += OnEnter;
            m_manager.OnStateExit += OnExit;
        }
        else
        {
            Debug.LogWarning($"[EntityStateManagerListener] {gameObject.name}: EntityStateManagerBaseが見つかりません", this);
        }
    }

    void OnDestroy()
    {
        if (m_manager != null)
        {
            m_manager.OnStateEnter -= OnEnter;
            m_manager.OnStateExit -= OnExit;
        }
    }

    private void OnEnter(Type state)
    {
        if (states.Contains(state.Name))
            onEnter?.Invoke();
    }

    private void OnExit(Type state)
    {
        if (states.Contains(state.Name))
            onExit?.Invoke();
    }
}
