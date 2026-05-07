using UnityEngine;
using UnityEngine.Events;
/// <summary>
/// プレイヤーアクションのイベントハブ。
/// VFX/SE/アニメ等がInspectorから繋げるようUnityEvent化。
/// 極性情報は OnPoleSwitch 発火後に PoleAbility.CurrentPole から読む。
/// </summary>
public class PlayerEvents : MonoBehaviour
{
    [Tooltip("通常射撃時に発火")]
    public UnityEvent onShoot;

    [Tooltip("セルフファイア時に発火")]
    public UnityEvent onSelfShoot;

    [Tooltip("磁極切替時に発火。極は PoleAbility.CurrentPole から取得")]
    public UnityEvent onPoleSwitch;

    [Tooltip("リロード時に発火")]
    public UnityEvent onReload;

    [Tooltip("スタブ攻撃ヒット時に発火")]
    public UnityEvent onStab;

    public void FireShoot() => onShoot?.Invoke();
    public void FireSelfShoot() => onSelfShoot?.Invoke();
    public void FirePoleSwitch() => onPoleSwitch?.Invoke();
    public void FireReload() => onReload?.Invoke();
    public void FireStab() => onStab?.Invoke();
}
