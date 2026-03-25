using System;
using UnityEngine;
using MagnetRush.Common;

namespace MagnetRush.Player
{
    /// <summary>
    /// プレイヤーアクションのイベントハブ。他のシステムがこれらのイベントを購読する。
    /// </summary>
    public class PlayerEvents : MonoBehaviour
    {
        public event Action OnShoot;
        public event Action<MagneticPole> OnPolaritySwitch;
        public event Action OnReload;
        public event Action<int> OnHurt;
        public event Action OnDie;

        public void FireShoot() => OnShoot?.Invoke();
        public void FirePolaritySwitch(MagneticPole pole) => OnPolaritySwitch?.Invoke(pole);
        public void FireReload() => OnReload?.Invoke();
        public void FireHurt(int damage) => OnHurt?.Invoke(damage);
        public void FireDie() => OnDie?.Invoke();
    }
}