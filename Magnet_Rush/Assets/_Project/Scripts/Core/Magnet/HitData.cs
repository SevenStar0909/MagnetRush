using UnityEngine;

/// <summary>
/// 衝突情報を構造化する。弾・近接・接触ダメージ全てで共通。
/// </summary>
public struct HitData
{
    public int damage;
    public Vector3 hitPoint;
    public Vector3 knockbackDir;
    public MagneticPole pole;
    public GameObject source;
}
