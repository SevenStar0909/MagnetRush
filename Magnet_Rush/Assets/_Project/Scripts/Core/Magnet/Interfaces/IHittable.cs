/// <summary>
/// 被弾可能なオブジェクトのインターフェース。
/// Hurtboxにアタッチしたコンポーネントが実装する。
/// </summary>
public interface IHittable
{
    /// <summary>ヒット解決上の所属グループ。攻撃側の HitGroup と比較して自傷・同士討ちを弾く。</summary>
    HitGroup HitGroup { get; }

    void OnHit(HitData hit);
}
