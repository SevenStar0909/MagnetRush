using UnityEngine;

/// <summary>
/// 弾同士の近接検出用インターフェース。
/// MagnetManagerが距離ベースで弾同士の衝突を処理するために使う。
/// </summary>
public interface IBulletProximity
{
    MagneticPole Pole { get; }
    bool IsStuck { get; }
    Transform transform { get; }
    GameObject gameObject { get; }
    MagnetField GetField();
}
