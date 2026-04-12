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
    public static int PlayerBullet { get; private set; }
    public static int EnemyBullet { get; private set; }
    public static int MeleeHitbox { get; private set; }
    public static int MagnetField { get; private set; }
    public static int PhysicsObject { get; private set; }
    public static int EntityBody { get; private set; }

    // --- 用途別マスク（Initialize()で計算） ---

    /// <summary>接地判定用。Default + Ground + Wall。</summary>
    public static int MaskGroundCheck { get; private set; }

    /// <summary>EntityController衝突用。Ground + Wall + EntityBody + PhysicsObject。Trigger系除外。</summary>
    public static int MaskEntityCollision { get; private set; }

    /// <summary>射撃レイキャスト用。Trigger系とPlayer除外。</summary>
    public static int MaskShootingRaycast { get; private set; }

    /// <summary>-1（未定義レイヤー）を安全にマスク化する。</summary>
    private static int SafeMask(int layer) => layer >= 0 ? (1 << layer) : 0;

    // --- Layer Collision Matrix 設計意図 ---
    // 「何と何が当たるか」はMatrixで一元管理。コード内でtag/layer判定しない。
    //
    // Player/Enemy(Hurtbox, Collider) × PlayerBullet/EnemyBullet/MeleeHitbox(Trigger) = ON
    //   → Trigger × Collider でOnTriggerEnter発火。攻撃側がIHittable.OnHit()を呼ぶ
    // PlayerBullet × Player = OFF（自分の弾に当たらない。自己射撃は直接呼び出し）
    // EnemyBullet × Enemy = OFF（タレット自傷防止）
    // PlayerBullet × PlayerBullet = OFF（Trigger×Trigger非発火。弾同士はMagnetManagerで距離検出）
    // PlayerBullet × PhysicsObject = ON（MagnetBulletは磁化対象に着弾）
    // EnemyBullet × PhysicsObject = ON（PhysicsObjectが遮蔽物として弾を消す）
    // MeleeHitbox × Player = ON、× Enemy = OFF（Matrixで自傷防止）
    // MagnetField × Player/Enemy = ON（範囲内Entity検知）
    // MagnetField × PhysicsObject = ON（磁化可能オブジェクト検知）
    // PhysicsObject × Ground/Wall = ON（物理衝突）
    // PhysicsObject × EntityBody = ON（接触ダメージ。OnCollisionEnter発火）
    // PhysicsObject × PhysicsObject = ON（PhysicsObject同士の物理衝突）
    // EntityBody × EntityBody = ON（Entity同士の押し合い。ComputePenetrationで処理）

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        Ground = LayerMask.NameToLayer("Ground");
        Wall = LayerMask.NameToLayer("Wall");
        Player = LayerMask.NameToLayer("Player");
        Enemy = LayerMask.NameToLayer("Enemy");
        PlayerBullet = LayerMask.NameToLayer("PlayerBullet");
        EnemyBullet = LayerMask.NameToLayer("EnemyBullet");
        MeleeHitbox = LayerMask.NameToLayer("MeleeHitbox");
        MagnetField = LayerMask.NameToLayer("MagnetField");
        PhysicsObject = LayerMask.NameToLayer("PhysicsObject");
        EntityBody = LayerMask.NameToLayer("EntityBody");

        // Default(0) + Ground + Wall
        MaskGroundCheck = (1 << 0) | SafeMask(Ground) | SafeMask(Wall);

        // Ground + Wall + EntityBody + PhysicsObject + Default(0)。Trigger系(弾/MeleeHitbox/MagnetField)除外
        MaskEntityCollision = (1 << 0) | SafeMask(Ground) | SafeMask(Wall) | SafeMask(EntityBody) | SafeMask(PhysicsObject);

        // Trigger系 + Player除外
        MaskShootingRaycast = ~(SafeMask(Player) | SafeMask(PlayerBullet) | SafeMask(EnemyBullet)
            | SafeMask(MeleeHitbox) | SafeMask(MagnetField) | (1 << 2));

        ValidateLayers();
    }

    static void ValidateLayers()
    {
        if (Ground == -1) Debug.LogError("[PhysicsLayers] Layer 'Ground' がProjectSettingsに未定義");
        if (Wall == -1) Debug.LogError("[PhysicsLayers] Layer 'Wall' がProjectSettingsに未定義");
        if (Player == -1) Debug.LogError("[PhysicsLayers] Layer 'Player' がProjectSettingsに未定義");
        if (Enemy == -1) Debug.LogError("[PhysicsLayers] Layer 'Enemy' がProjectSettingsに未定義");
        if (PlayerBullet == -1) Debug.LogError("[PhysicsLayers] Layer 'PlayerBullet' がProjectSettingsに未定義");
        if (EnemyBullet == -1) Debug.LogError("[PhysicsLayers] Layer 'EnemyBullet' がProjectSettingsに未定義");
        if (MeleeHitbox == -1) Debug.LogError("[PhysicsLayers] Layer 'MeleeHitbox' がProjectSettingsに未定義");
        if (MagnetField == -1) Debug.LogError("[PhysicsLayers] Layer 'MagnetField' がProjectSettingsに未定義");
        if (PhysicsObject == -1) Debug.LogError("[PhysicsLayers] Layer 'PhysicsObject' がProjectSettingsに未定義");
        if (EntityBody == -1) Debug.LogError("[PhysicsLayers] Layer 'EntityBody' がProjectSettingsに未定義");
    }
}
