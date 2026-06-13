using UnityEngine;

/// <summary>
/// チュートリアル等で個別ロックできるプレイヤー機能の種類。
/// Move / Camera は LockAll の対象外（個別指定でのみロックされる）。
/// 既存プレハブのシリアライズ値を壊さないため、新メンバは必ず末尾に追加する。
/// InspectorName で Inspector のドロップダウンには日本語で表示される。
/// </summary>
public enum PlayerAbilityType
{
    /// <summary>エイム（LT 長押しのスロー照準）。</summary>
    [InspectorName("エイム（LT長押しでゆっくり狙う）")]
    Aim,

    /// <summary>通常射撃（RT）。</summary>
    [InspectorName("通常射撃（RTで磁力弾を撃つ）")]
    Fire,

    /// <summary>セルフファイア（自分に磁極を付与）。</summary>
    [InspectorName("セルフファイア（自分に磁極を付ける）")]
    SelfFire,

    /// <summary>リロード（撃った弾を全部消す）。</summary>
    [InspectorName("リロード（撃った弾を全部消す）")]
    Reload,

    /// <summary>磁極切替（S/N を入れ替える）。</summary>
    [InspectorName("磁極切替（SとNを入れ替える）")]
    PoleSwitch,

    /// <summary>ジャンプ。</summary>
    [InspectorName("ジャンプ")]
    Jump,

    /// <summary>スタブ攻撃（崩れたボスへのとどめ）。</summary>
    [InspectorName("スタブ攻撃（崩れたボスへのとどめ）")]
    Stab,

    /// <summary>移動（左スティック / WASD）。</summary>
    [InspectorName("移動（左スティックで歩く）")]
    Move,

    /// <summary>カメラ操作（右スティック / マウス）。</summary>
    [InspectorName("カメラ（右スティック/マウスで見回す）")]
    Camera,
}
