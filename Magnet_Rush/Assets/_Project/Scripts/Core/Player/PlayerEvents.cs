using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// プレイヤーアクションのイベントハブ。
/// VFX/SE/アニメ等がInspectorから繋げるようUnityEvent化。
/// 極性情報は OnPoleSwitch 発火後に Player.CurrentPole から読む。
/// </summary>
public class PlayerEvents : MonoBehaviour
{
    [Tooltip("通常射撃時に発火")]
    [FormerlySerializedAs("OnShoot")]
    public UnityEvent onShoot;

    [Tooltip("セルフファイア時に発火")]
    [FormerlySerializedAs("OnSelfShoot")]
    public UnityEvent onSelfShoot;

    [Tooltip("磁極切替時に発火。極は Player.CurrentPole から取得")]
    [FormerlySerializedAs("OnPolaritySwitch")]
    [FormerlySerializedAs("OnPoleSwitch")]
    public UnityEvent onPoleSwitch;

    [Tooltip("リロード時に発火")]
    [FormerlySerializedAs("OnReload")]
    public UnityEvent onReload;

    public void FireShoot() => onShoot?.Invoke();
    public void FireSelfShoot() => onSelfShoot?.Invoke();
    public void FirePoleSwitch() => onPoleSwitch?.Invoke();
    public void FireReload() => onReload?.Invoke();
}
