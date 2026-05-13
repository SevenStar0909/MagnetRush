using UnityEngine;

/// <summary>
/// スタブのダメージを受け取るためのインターフェース
/// </summary>
public interface IStabReceiver
{
    void OnStabHit(StabHitData data);
}

/// <summary>
/// スタブのダメージを受け取るためのデータ構造
/// </summary>
public struct StabHitData
{
    public int damage;
    public Vector3 hitPoint;
    public GameObject source;
}