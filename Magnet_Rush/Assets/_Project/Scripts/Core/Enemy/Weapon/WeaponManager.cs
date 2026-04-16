using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-30)]
public class WeaponManager : Singleton<WeaponManager>
{
    // アクティブな武器のリスト。PickableWeaponが自身を登録/解除する。
    private readonly List<WeaponStateController> m_activeWeapons = new();

    protected override void Awake()
    {
        base.Awake();

        // OnEnableの初期化順序でRegisterが漏れた武器を回収
        var existing = FindObjectsByType<WeaponStateController>(FindObjectsSortMode.None);
        foreach (var w in existing)
            Register(w);
    }

    // アクティブな武器の読み取り専用リスト。外部からアクセスするためのプロパティ。
    public IReadOnlyList<WeaponStateController> ActiveWeapons => m_activeWeapons;

    // 武器をアクティブリストに登録する。PickableWeaponから呼び出される。
    public void Register(WeaponStateController weapon)
    {
        if (weapon == null || m_activeWeapons.Contains(weapon)) { ChannelLogger.LogGuardReturn("Enemy", "武器null/登録済み"); return; }
        m_activeWeapons.Add(weapon);
    }

    // 武器をアクティブリストから解除する。PickableWeaponから呼び出される。
    public void Unregister(WeaponStateController weapon)
    {
        if (weapon == null || !m_activeWeapons.Contains(weapon)) { ChannelLogger.LogGuardReturn("Enemy", "武器null/未登録"); return; }
        m_activeWeapons.Remove(weapon);
    }

    // 指定した位置と半径内で最も近い拾える武器を見つける。拾える武器がない場合はnullを返す。
    public WeaponStateController FindNearestPickableWeapon(Vector3 position, float searchRadius, bool excludeMagnetAffected = false)
    {
        WeaponStateController nearestWeapon = null;
        float nearestSqr = searchRadius * searchRadius;

        for (int i = 0; i < m_activeWeapons.Count; i++)
        {
            WeaponStateController weapon = m_activeWeapons[i];
            if (weapon == null) continue;
            if (!weapon.CanBePickedUp) continue;
            if (excludeMagnetAffected && weapon.IsMagnetAffected) continue;

            float sqr = (weapon.transform.position - position).sqrMagnitude;
            if (sqr > nearestSqr) continue;

            nearestSqr = sqr;
            nearestWeapon = weapon;
        }

        return nearestWeapon;
    }
}