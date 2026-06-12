/// <summary>
/// チュートリアル等で個別ロックできるプレイヤー機能の種類。
/// 移動とカメラはロック対象外（常に操作可能）のため含めない。
/// </summary>
public enum PlayerAbilityType
{
    /// <summary>エイム（LT 長押しのスロー照準）。</summary>
    Aim,

    /// <summary>通常射撃（RT）。</summary>
    Fire,

    /// <summary>セルフファイア（自分に磁極を付与）。</summary>
    SelfFire,

    /// <summary>リロード（撃った弾を全部消す）。</summary>
    Reload,

    /// <summary>磁極切替（S/N を入れ替える）。</summary>
    PoleSwitch,

    /// <summary>ジャンプ。</summary>
    Jump,

    /// <summary>スタブ攻撃（崩れたボスへのとどめ）。</summary>
    Stab,
}
