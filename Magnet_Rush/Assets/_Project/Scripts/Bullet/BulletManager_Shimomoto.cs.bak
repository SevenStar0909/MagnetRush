using UnityEngine;
using System;
using System.Collections.Generic;

public class BulletManager_Shimomoto : MonoBehaviour
{
    public static BulletManager Instance { get; private set; }

    [SerializeField] private BulletSettings settings;

    private List<MagnetBullet> activeBullets = new List<MagnetBullet>();

    public event Action<int> OnBulletCountChanged;

    private void Awake()
    {
        Instance = this;
    }

    public bool CanShoot()
    {
        return activeBullets.Count < settings.maxBullets;
    }

    public void RegisterBullet(MagnetBullet bullet)
    {
        activeBullets.Add(bullet);
        OnBulletCountChanged?.Invoke(activeBullets.Count);
    }

    public void UnregisterBullet(MagnetBullet bullet)
    {
        if (activeBullets.Contains(bullet))
        {
            activeBullets.Remove(bullet);
            OnBulletCountChanged?.Invoke(activeBullets.Count);
        }
    }
    public int GetBulletCount() => activeBullets.Count;
}