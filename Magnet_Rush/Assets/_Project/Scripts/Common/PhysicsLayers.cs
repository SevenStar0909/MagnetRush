using UnityEngine;

/// <summary>
/// 物理レイヤーの一元管理。レイヤー番号と用途別マスクを提供する。
/// RuntimeInitializeOnLoadMethod(SubsystemRegistration)で全Awakeより前に初期化される。
/// レイヤー名はProjectSettings/TagManager.assetと一致する必要がある。
/// </summary>
public static class PhysicsLayers
{
    // --- レイヤー番号（Initialize()で設定。Awakeより前に確定） ---
    public static int Ground { get; private set; }
    public static int Wall { get; private set; }
    public static int Player { get; private set; }
    public static int Enemy { get; private set; }
    public static int Bullet { get; private set; }
    public static int MagnetField { get; private set; }
    public static int EntityBody { get; private set; }
    public static int AttackHitbox { get; private set; }
    public static int WeaponBody { get; private set; }

    // --- 用途別マスク（Initialize()で計算） ---

    /// <summary>接地判定用。Default + Ground + Wall。</summary>
    public static int MaskGroundCheck { get; private set; }

    /// <summary>EntityController衝突用。MagnetField + Bullet + IgnoreRaycast除外。</summary>
    public static int MaskEntityCollision { get; private set; }

    /// <summary>射撃レイキャスト用。Player除外。</summary>
    public static int MaskShootingRaycast { get; private set; }

    /// <summary>-1（未定義レイヤー）を安全にマスク化する。</summary>
    private static int SafeMask(int layer) => layer >= 0 ? (1 << layer) : 0;

    // --- Layer Collision Matrix 設計意図 ---
    // MagnetField × Ground/Wall = OFF（磁力場トリガーが地面や壁に反応する必要なし）
    // MagnetField × Bullet = OFF（弾道曲げはGetStrengthAtで直接計算）
    // MagnetField × MagnetField = OFF（磁力場同士のトリガー不要）
    // MagnetField × Magnetized = OFF（磁化オブジェクトとの直接トリガー不要）
    // MagnetField × Player/Enemy = ON（OnTriggerStayでEntity検知に必要）
    // Player × Bullet = ON（SelfFire弾がプレイヤーに当たる必要あり）
    // Bullet × Bullet = ON（弾同士のOnTriggerEnterでダメージ蓄積処理）
    // Bullet × EntityBody = OFF（弾はBulletReceiverのtriggerで検出）
    // Bullet × AttackHitbox = OFF（弾が攻撃ヒットボックス経由で磁化するのを防止）
    // MagnetField × EntityBody/AttackHitbox = OFF（磁力場は物理コライダーとの衝突不要）
    // Player × EntityBody = OFF（不要なtriggerイベント防止。衝突検出はLayerMask経由）
    // Enemy × EntityBody = OFF（同上）
    // Player × AttackHitbox = ON（攻撃がプレイヤーに当たる必要あり）
    // Enemy × AttackHitbox = OFF（敵同士のフレンドリーファイア防止）
    // EntityBody × EntityBody = ON（Entity同士の衝突。HandlePenetration/MoveAndSlideで処理）
    // EntityBody × AttackHitbox = OFF（物理衝突と攻撃判定の混在防止）
    // AttackHitbox × AttackHitbox = OFF（攻撃同士の衝突不要）
    // WeaponBody × Bullet/MagnetField/Player/Enemy/EntityBody/AttackHitbox/WeaponBody = OFF（武器物理は地面/壁のみ）

    /// <summary>
    /// 全Awakeより前にメインスレッドで実行される。
    /// NameToLayerはUnity APIなのでstaticフィールド初期化子からは呼べない。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        Ground = LayerMask.NameToLayer("Ground");
        Wall = LayerMask.NameToLayer("Wall");
        Player = LayerMask.NameToLayer("Player");
        Enemy = LayerMask.NameToLayer("Enemy");
        Bullet = LayerMask.NameToLayer("Bullet");
        MagnetField = LayerMask.NameToLayer("MagnetField");
        EntityBody = LayerMask.NameToLayer("EntityBody");
        AttackHitbox = LayerMask.NameToLayer("AttackHitbox");
        WeaponBody = LayerMask.NameToLayer("WeaponBody");

        // Default(0) + Ground + Wall
        MaskGroundCheck = (1 << 0) | SafeMask(Ground) | SafeMask(Wall);
        // MagnetField + Bullet + AttackHitbox + IgnoreRaycast(2) を除外
        MaskEntityCollision = ~(SafeMask(MagnetField) | SafeMask(Bullet) | SafeMask(AttackHitbox) | SafeMask(WeaponBody) | (1 << 2));
        // Player を除外
        MaskShootingRaycast = ~SafeMask(Player);

        ValidateLayers();
    }

    static void ValidateLayers()
    {
        if (Ground == -1) Debug.LogError("[PhysicsLayers] Layer 'Ground' がProjectSettingsに未定義");
        if (Wall == -1) Debug.LogError("[PhysicsLayers] Layer 'Wall' がProjectSettingsに未定義");
        if (Player == -1) Debug.LogError("[PhysicsLayers] Layer 'Player' がProjectSettingsに未定義");
        if (Enemy == -1) Debug.LogError("[PhysicsLayers] Layer 'Enemy' がProjectSettingsに未定義");
        if (Bullet == -1) Debug.LogError("[PhysicsLayers] Layer 'Bullet' がProjectSettingsに未定義");
        if (MagnetField == -1) Debug.LogError("[PhysicsLayers] Layer 'MagnetField' がProjectSettingsに未定義");
        if (EntityBody == -1) Debug.LogError("[PhysicsLayers] Layer 'EntityBody' がProjectSettingsに未定義");
        if (AttackHitbox == -1) Debug.LogError("[PhysicsLayers] Layer 'AttackHitbox' がProjectSettingsに未定義");
        if (WeaponBody == -1) Debug.LogError("[PhysicsLayers] Layer 'WeaponBody' がProjectSettingsに未定義");
    }
}
