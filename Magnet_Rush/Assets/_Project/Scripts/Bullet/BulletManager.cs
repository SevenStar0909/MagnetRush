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
    /// 全磁力効果をリセットする（リロード）。
    /// 弾の消去 + 全MagnetFieldの期限切れ発火（各自の後始末が走る）。
    /// </summary>
    /// <summary>
    /// 全磁力効果をリセットする（リロード）。
    /// 全MagnetFieldをForceExpire（後始末コールバック発火）してから弾リストをクリア。
    /// </summary>
    public void ClearAll()
    {
        // 全MagnetFieldをForceExpire → 各自の後始末が走る
        // パターン1: OnFieldExpired → Destroy(bulletGO)
        // パターン2: OnFieldExpired → Deactivate + Destroy(visualizer)
        var fields = FindObjectsByType<MagnetField>(FindObjectsSortMode.None);
        foreach (var field in fields)
        {
            if (field != null)
                field.ForceExpire();
        }

        // ForceExpireでパターン1の弾GOが消えるが、念のため残りも消す
        var copy = new List<MagnetBullet>(activeBullets);
        foreach (var bullet in copy)
        {
            if (bullet != null)
                Destroy(bullet.gameObject);
        }
        activeBullets.Clear();

        OnBulletCountChanged?.Invoke(0);
    }
}
