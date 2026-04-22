using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// プレイヤーアクションのイベントハブ。
/// VFX/SE/アニメ等がInspectorから繋げるようUnityEvent化。
/// 極性情報は OnPoleSwitch 発火後に PoleController.CurrentPole から読む。
/// </summary>
public class PlayerEvents : MonoBehaviour
{
    [Tooltip("通常射撃時に発火")]
    public UnityEvent OnShoot;

    [Tooltip("セルフファイア時に発火")]
    public UnityEvent OnSelfShoot;

    [Tooltip("磁極切替時に発火。極は PoleController.CurrentPole から取得")]
    [FormerlySerializedAs("OnPolaritySwitch")]
    public UnityEvent OnPoleSwitch;

    [Tooltip("リロード時に発火")]
    public UnityEvent OnReload;

    public void FireShoot() => OnShoot?.Invoke();
    public void FireSelfShoot() => OnSelfShoot?.Invoke();
    public void FirePoleSwitch() => OnPoleSwitch?.Invoke();
    public void FireReload() => OnReload?.Invoke();
}
