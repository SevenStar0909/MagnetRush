using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 弾の生存管理。4発上限、登録/解除、全消去を行う。
/// </summary>
[DefaultExecutionOrder(-30)]
public class BulletManager : Singleton<BulletManager>
{
    [SerializeField] private BulletSettings settings;

    private readonly List<MagnetBullet> activeBullets = new();

    public int MaxBullets => settings != null ? settings.maxBullets : 4;
    public int CurrentCount => activeBullets.Count;

    public event Action<int> OnBulletCountChanged;

    public bool CanShoot()
    {
        return activeBullets.Count < MaxBullets;
    }

    public void Register(MagnetBullet bullet)
    {
        if (bullet == null || activeBullets.Contains(bullet)) return;
        activeBullets.Add(bullet);
        OnBulletCountChanged?.Invoke(activeBullets.Count);
    }

    public void Unregister(MagnetBullet bullet)
    {
        if (bullet == null || !activeBullets.Contains(bullet)) return;
        activeBullets.Remove(bullet);
        OnBulletCountChanged?.Invoke(activeBullets.Count);
    }

    /// <summary>
    /// 全弾を消去する（リロード）。
    /// リスト変更中のイテレーション例外防止のためコピーしてからイテレートする。
    /// </summary>
    public void ClearAll()
    {
        var copy = new List<MagnetBullet>(activeBullets);
        foreach (var bullet in copy)
        {
            if (bullet != null)
            {
                Destroy(bullet.gameObject);
            }
        }
        activeBullets.Clear();
        OnBulletCountChanged?.Invoke(0);
    }
}
