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

    [Label("最大スタンゲージ")]
    [Tooltip("満タンになるとよろけ（Stagger）が発生する。物理オブジェクトをボス本体に当てると溜まる")]
    public int maxStamina = 10;
    [Label("スタンゲージ減衰（/s）")]
    [Tooltip("当てるのをやめると、1秒あたりこの量だけスタンゲージが戻る")]
    public int staminaRecovery = 5;
    [Label("スタンゲージ減衰の待ち時間（秒）")]
    [Tooltip("最後に当ててからこの秒数たつと、スタンゲージが戻り始める")]
    public int staminaRecoveryCooldown = 5;
    [Label("スタン状態の継続時間（秒）")]
    [Tooltip("振り上げカウンターでスタンしたとき動けない時間。仕様＝5秒")]
    public float staminaBreakDuration = 5.0f;
    [Label("本体に1発当てたときのスタンゲージ蓄積率（％）")]
    [Tooltip("磁力で飛ばした物理オブジェクトがボス本体に当たるたび、スタンゲージが何％溜まるか。10なら10発で満タン→よろけ")]
    [Range(1, 100)]
    public int stunGaugePercentPerBodyHit = 10;

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
    [Tooltip("接地判定レイヤー（0=環境(Default/Ground/Wall)の既定マスク）")]
    public LayerMask groundLayer;
    [Label("外部力減衰率")]
    [Tooltip("外部力（磁力等）の指数減衰率。大きいほど早く減速する")]
    public float externalDrag = 3f;

    [Header("[攻撃]")]
    [Label("攻撃ダメージ")]
    [Tooltip("1回の攻撃ダメージ")]
    public int attackDamage = 1;

    [Label("起動範囲（m）")]
    [Tooltip("プレイヤーがこの距離より遠いと、攻撃も向き直りもせず Idle のまま待機する（戦闘を始める半径）")]
    public float activationRange = 30.0f;

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
    [Label("よろけ状態の継続時間（秒）")]
    [Tooltip("スタンゲージ満タンでよろけたとき動けない時間。仕様＝10秒")]
    public float staggerDuration = 10f;
    [Tooltip("スタッガー中の移動速度倍率（未使用）")]
    [Range(0f, 1f)]
    public float staggerMoveMultiplier = 0.5f;

    [Header("[手の磁力範囲キャスト]")]
    [Label("手の磁力範囲半径（m）")]
    [Tooltip("AttackStance/AttackMotion 突入時、右手を中心にこの半径内の PhysicsObject と右手自身に同一の N/S をランダム付与する")]
    [Min(0f)]
    public float magnetCastRadius = 5.0f;
}
