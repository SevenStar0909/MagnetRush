using UnityEngine;

/// <summary>
/// スタブのダメージを受け取るためのインターフェース。
/// 実装側は CanReceiveStab でスタブ可能条件 (体幹ブレイク中など) を表現する。
/// </summary>
public interface IStabReceiver
{
    /// <summary>true のときだけ Player は StabPlayerState へ遷移できる。</summary>
    bool CanReceiveStab { get; }

    /// <summary>突き刺し瞬間の AnimEvent から呼ばれる。実ダメージ処理を行う。</summary>
    void OnStabHit(StabHitData data);
}

/// <summary>
/// スタブのダメージを受け取るためのデータ構造。
/// </summary>
public struct StabHitData
{
    public int damage;
    public Vector3 hitPoint;
    public GameObject source;
}