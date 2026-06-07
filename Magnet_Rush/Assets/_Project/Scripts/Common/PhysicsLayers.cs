using UnityEngine;

/// <summary>
/// 物理レイヤー番号の一元管理（純レジストリ）。レイヤー名 → 番号を返すだけ。
/// マスク計算は持たない（当たり範囲のマスクは各所の SerializeField LayerMask で持つ）。
/// 当たり判定設計原則: レイヤーは「種類」で割り、マスクはコードで組まず Inspector/SO の LayerMask で持つ。
///
/// NameToLayer で都度解決するので Edit Mode でも正しい番号を返す（旧 RuntimeInitialize 版は PlayMode 前に 0 を返す不具合があった）。
/// レイヤー名は ProjectSettings/TagManager.asset と一致する必要がある。未定義なら -1 を返す。
/// </summary>
public static class PhysicsLayers
{
    // Unity 組み込みレイヤー（生ビット 1<<0 / 1<<2 を直書きしないための命名）
    public static int Default => 0;
    public static int IgnoreRaycast => 2;

    public static int Ground => LayerMask.NameToLayer("Ground");
    public static int Wall => LayerMask.NameToLayer("Wall");
    public static int Player => LayerMask.NameToLayer("Player");
    public static int Enemy => LayerMask.NameToLayer("Enemy");
    public static int PlayerBullet => LayerMask.NameToLayer("PlayerBullet");
    public static int EnemyBullet => LayerMask.NameToLayer("EnemyBullet");
    public static int MeleeHitbox => LayerMask.NameToLayer("MeleeHitbox");
    public static int MagnetField => LayerMask.NameToLayer("MagnetField");
    public static int PhysicsObject => LayerMask.NameToLayer("PhysicsObject");
    public static int EntityBody => LayerMask.NameToLayer("EntityBody");

    /// <summary>-1（未定義レイヤー）を安全にビットへ。未定義は 0（含めない）。</summary>
    public static int Bit(int layer) => layer >= 0 ? (1 << layer) : 0;
}
