using UnityEngine;
using MagnetRush.Common;

/// <summary>
/// 2つのMagnetizable間の磁力ペア情報。
/// </summary>
public struct MagnetPair
{
    public Magnetizable a;
    public Magnetizable b;

    public MagnetPair(Magnetizable a, Magnetizable b)
    {
        this.a = a;
        this.b = b;
    }

    /// <summary>同極（反発）かどうか</summary>
    public bool IsSamePole => a.Pole == b.Pole;

    /// <summary>異極（引力）かどうか</summary>
    public bool IsOppositePole => a.Pole != b.Pole && a.Pole != MagneticPole.None && b.Pole != MagneticPole.None;

    /// <summary>2オブジェクト間の距離</summary>
    public float Distance => Vector3.Distance(a.transform.position, b.transform.position);

    /// <summary>AからBへの方向ベクトル（正規化済み）</summary>
    public Vector3 DirectionAtoB => (b.transform.position - a.transform.position).normalized;
}
