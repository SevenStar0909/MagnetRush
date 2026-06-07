/// <summary>
/// ヒット解決上の役割グループ。陣営ではなく「当たり判定でどう扱うか」の分類。
/// 攻撃側と被弾側の HitGroup を比較し、異なるときだけダメージを通す（同グループ＝自傷・同士討ちを弾く）。
/// Physics（物理オブジェクト）は Player でも Enemy でもないので、両方に当たる。
///
/// 設計: 陣営をレイヤーから引き剥がして書き換え可能なデータにしたもの。
/// 実行中に変えられる（磁力で寝返った弾の HitGroup を書き換える等）。詳細は collision-design-principles.md。
/// </summary>
public enum HitGroup
{
    Player,
    Enemy,
    Physics,
}
