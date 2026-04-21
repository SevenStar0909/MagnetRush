using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// プレイヤーアクションのイベントハブ。
/// VFX/SE/アニメ等がInspectorから繋げるようUnityEvent化。
/// 極性情報は OnPolaritySwitch 発火後に Player.CurrentPole から読む。
/// </summary>
public class PlayerEvents : MonoBehaviour
{
    [Tooltip("通常射撃時に発火")]
    public UnityEvent OnShoot;

    [Tooltip("セルフファイア時に発火")]
    public UnityEvent OnSelfShoot;

    [Tooltip("磁極切替時に発火。極は Player.CurrentPole から取得")]
    public UnityEvent OnPolaritySwitch;

    [Tooltip("リロード時に発火")]
    public UnityEvent OnReload;

    public void FireShoot() => OnShoot?.Invoke();
    public void FireSelfShoot() => OnSelfShoot?.Invoke();
    public void FirePolaritySwitch() => OnPolaritySwitch?.Invoke();
    public void FireReload() => OnReload?.Invoke();
}
