/// <summary>
/// 被弾可能なオブジェクトのインターフェース。
/// Hurtboxにアタッチしたコンポーネントが実装する。
/// </summary>
public interface IHittable
{
    void OnHit(HitData hit);
}
