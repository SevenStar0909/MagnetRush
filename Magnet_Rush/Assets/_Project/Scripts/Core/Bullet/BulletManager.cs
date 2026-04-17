using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 弾の生存管理。4発上限、登録/解除、全消去を行う。
/// 残弾はリロード（X）でのみ回復する。弾が着弾で消えても残弾は回復しない。
/// </summary>
[DefaultExecutionOrder(-30)]
public class BulletManager : Singleton<BulletManager>
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private BulletSettings m_settings;

    private readonly List<MagnetBullet> m_activeBullets = new();
    private int m_shotCount;

    /// <summary>
    /// 同時に存在できる弾の最大数。
    /// </summary>
    public int MaxBullets => m_settings != null ? m_settings.maxBullets : 4;

    /// <summary>
    /// 現在のアクティブな弾の数。
    /// </summary>
    public int CurrentCount => m_activeBullets.Count;

    /// <summary>アクティブな弾のリストを返す（読み取り専用）。</summary>
    public IReadOnlyList<MagnetBullet> ActiveBullets => m_activeBullets;

    /// <summary>
    /// 撃った弾数（リロードでリセット）。
    /// </summary>
    public int ShotCount => m_shotCount;

    /// <summary>
    /// 弾数が変化したときに発火するイベント。引数は撃った弾数。
    /// </summary>
    public event Action<int> OnBulletCountChanged;

    /// <summary>
    /// 弾数上限に達していなければtrueを返す。
    /// </summary>
    public bool CanShoot()
    {
        return m_shotCount < MaxBullets;
    }

    /// <summary>
    /// 弾をアクティブリストに登録する。撃った弾数を加算。
    /// </summary>
    public void Register(MagnetBullet bullet)
    {
        if (bullet == null || m_activeBullets.Contains(bullet)) { ChannelLogger.LogGuardReturn("Bullet", "弾がnullまたは既に登録済み"); return; }
        m_activeBullets.Add(bullet);
        m_shotCount++;
        OnBulletCountChanged?.Invoke(m_shotCount);
    }

    /// <summary>
    /// 弾を生成せずに撃った弾数だけ加算する（自己射撃用）。
    /// </summary>
    public void IncrementShotCount()
    {
        m_shotCount++;
        OnBulletCountChanged?.Invoke(m_shotCount);
    }

    /// <summary>
    /// 弾をアクティブリストから解除する。撃った弾数は減らさない。
    /// </summary>
    public void Unregister(MagnetBullet bullet)
    {
        if (bullet == null || !m_activeBullets.Contains(bullet)) { ChannelLogger.LogGuardReturn("Bullet", "弾がnullまたは未登録"); return; }
        m_activeBullets.Remove(bullet);
    }

    /// <summary>
    /// 全磁力効果をリセットする（リロード）。
    /// 全MagnetFieldをForceExpire（後始末コールバック発火）してから弾リストをクリア。
    /// </summary>
    public void ClearAll()
    {
        var fields = FindObjectsByType<MagnetField>(FindObjectsSortMode.None);
        foreach (var field in fields)
        {
            if (field != null)
                field.ForceExpire();
        }

        var copy = new List<MagnetBullet>(m_activeBullets);
        foreach (var bullet in copy)
        {
            if (bullet != null)
                Destroy(bullet.gameObject);
        }
        m_activeBullets.Clear();
        m_shotCount = 0;

        OnBulletCountChanged?.Invoke(0);
    }
}
