using UnityEngine;

[CreateAssetMenu(fileName = "EnemyBossSettings", menuName = "MagnetRush/EnemyBossSettings")]
[ClassLabelSO("ボス敵設定")]
public class EnemyBossSettings : ScriptableObject
{
    [Header("[ステータス]")]
    [Label("最大HP")]
    [Tooltip("最大HP")]
    public int maxHp = 10;

    [Label("HPバー本数")]
    [Tooltip("HP表示のバー分割数。スタブ攻撃は1バー分のHPを一気に削る")]
    [Min(1)]
    public int healthBarSegments = 3;

    [Label("最大体幹ゲージ")]
    [Tooltip("最大体幹ゲージ")]
    public int maxStamina = 10; 
    [Label("体幹ゲージ回復（/s）")]
    [Tooltip("体幹ゲージ回復量")]
    public int staminaRecovery = 5;
    [Label("体幹ゲージ回復クールダウン（秒）")]
    [Tooltip("攻撃を受けた後の体幹ゲージ回復クールダウン（秒）")]
    public int staminaRecoveryCooldown = 5;
    [Label("体幹崩れ(スタン)時間（秒）")]
    [Tooltip("体幹ゲージが0になりる行動不能時間")]
    public float staminaBreakDuration = 3.0f;
    [Label("右手スタンStamina消費量")]
    [Tooltip("AttackStance中に右手×PhysicsObjectが当たった時のStamina消費量。0でブレイク→スタン")]
    public int armStunStaminaDamage = 5;

    [Header("[移動]")]
    [Label("移動速度（m/s）")]
    [Tooltip("NavMeshAgentの移動速度（m/s）")]
    public float moveSpeed = 3.5f;
    [Label("加速度")]
    [Tooltip("NavMeshAgentの加速度")]
    public float acceleration = 8.0f;
    [Label("追跡範囲（m）")]
    [Tooltip("プレイヤーを追跡する範囲（m）")]
    public float chaseRange = 20f;
    [Label("停止距離（m）")]
    [Tooltip("プレイヤーとの停止距離（m）")]
    public float stopDistance = 1.5f;

    [Header("[Entity移動]")]
    [Label("方向転換減衰")]
    [Tooltip("方向転換時の減衰（大きいほど素早く曲がる）")]
    public float turningDrag = 15f;
    [Label("減速度")]
    [Tooltip("減速度（入力なし時の停止速度）")]
    public float deceleration = 20f;

    [Header("[物理]")]
    [Label("重力加速度（負値）")]
    [Tooltip("重力加速度（負値）")]
    public float gravity = -20f;
    [Label("接地スナップ力")]
    [Tooltip("接地時のスナップ力")]
    public float snapForce = 2f;
    [Label("接地判定追加距離")]
    [Tooltip("接地判定の追加距離")]
    public float groundCheckDistance = 0.3f;
    [Label("接地判定レイヤー")]
    [Tooltip("接地判定レイヤー（0=PhysicsLayers.MaskGroundCheck）")]
    public LayerMask groundLayer;
    [Label("外部力減衰率")]
    [Tooltip("外部力（磁力等）の指数減衰率。大きいほど早く減速する")]
    public float externalDrag = 3f;

    [Header("[攻撃]")]
    [Label("攻撃ダメージ")]
    [Tooltip("1回の攻撃ダメージ")]
    public int attackDamage = 1;
    [Label("近接攻撃の射程（m）")]
    [Tooltip("プレイヤーがこの距離より近くにいるとき、近接攻撃を行う")]
    public float attackRange = 20.0f;

    [Label("タックル攻撃の発動距離（m）")]
    [Tooltip("プレイヤーがこの距離より遠くにいるとき、タックル攻撃を行う（近接攻撃の射程より外で発動）")]
    public float rushAttackRange = 20.0f;

    [Label("タックル時の速度倍率")]
    [Tooltip("タックル中、通常移動速度の何倍の速さで突進するか。1.0で通常と同じ、2.0で2倍の速さ")]
    [Range(0.5f, 5.0f)]
    public float rushSpeedMultiplier = 2.0f;

    [Label("ミサイル攻撃の発動距離（m）")]
    [Tooltip("プレイヤーがこの距離より遠くにいるとき、ミサイル攻撃を行う（タックルと交互に発動）")]
    public float missileAttackRange = 50.0f;

    [Label("攻撃間隔（秒）")]
    [Tooltip("攻撃間隔（秒）")]
    public float attackInterval = 1.2f;
    [Label("プレイヤーへの回転速度")]
    [Tooltip("プレイヤーへの回転速度")]
    public float rotationSpeed = 10f;
    [Label("ヒットボックス持続時間（秒）")]
    [Tooltip("攻撃ヒットボックスの持続時間（秒）")]
    public float attackHitboxDuration = 0.2f;

    [Header("[向き調整]")]
    [Label("通常時の向きデッドゾーン（度）")]
    [Tooltip("プレイヤーとの角度がこの値以下なら向き直さない（追尾ねじれ防止）。Idle/Stance/Missile/Stagger等で使用")]
    [Range(0f, 90f)]
    public float faceDeadZoneDeg = 3f;
    [Label("AttackMotion中の向きデッドゾーン（度）")]
    [Tooltip("AttackMotion中はこの角度未満では向き直さない。180で実質ロック（攻撃発生中の追尾を止める）")]
    [Range(0f, 180f)]
    public float attackMotionFaceDeadZoneDeg = 180f;

    [Header("スタッガー")]
    [Tooltip("スタンorAttack終了後の隙時間（秒）")]
    public float staggerDuration = 1.5f;
    [Tooltip("スタッガー中の移動速度倍率")]
    [Range(0f, 1f)]
    public float staggerMoveMultiplier = 0.5f;

    [Header("[手の磁力範囲キャスト]")]
    [Label("手の磁力範囲半径（m）")]
    [Tooltip("AttackStance/AttackMotion 突入時、右手を中心にこの半径内の PhysicsObject と右手自身に同一の N/S をランダム付与する")]
    [Min(0f)]
    public float magnetCastRadius = 5.0f;
}
