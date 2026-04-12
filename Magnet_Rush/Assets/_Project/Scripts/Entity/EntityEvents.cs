using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Entity基底のInspector接続可能イベント。
/// MonoBehaviourではなく[Serializable]クラス。Entity.csのSerializeFieldとして使用。
/// コード購読用のevent Action（PlayerEvents等）とは別の責務。
/// </summary>
[Serializable]
public class EntityEvents
{
    [Tooltip("接地した瞬間に発火")]
    public UnityEvent onGroundEnter;

    [Tooltip("地面から離れた瞬間に発火")]
    public UnityEvent onGroundExit;
}
