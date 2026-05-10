using UnityEngine;

[CreateAssetMenu(fileName = "EnemyBossSettings", menuName = "MagnetRush/EnemyBossSettings")]
public class EnemyBossSettings : ScriptableObject
{
    [Header("[ステータス]")]
    [Label("最大HP")]
    [Tooltip("最大HP")]
    // TODO: Health.cs の m_maxHealth を廃止し、本フィールドを参照するよう配線
    public int maxHp = 10;

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
    [Label("攻撃可能距離（m）")]
    [Tooltip("近接攻撃可能距離（m）")]
    public float attackRange = 20.0f;

    [Label("rush攻撃可能距離（m）")]
    [Tooltip(" プレイヤーがこの距離（m）以外にいる場合、rush攻撃を行う")]
    public float rushAttackRange = 20.0f;

    [Label("missile攻撃可能距離（m）")]
    [Tooltip(" プレイヤーがこの距離（m）以外にいる場合、missile攻撃を行う")]
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

    [Header("スタッガー")]
    [Tooltip("スタンorAttack終了後の隙時間（秒）")]
    public float staggerDuration = 1.5f;
    [Tooltip("スタッガー中の移動速度倍率")]
    [Range(0f, 1f)]
    public float staggerMoveMultiplier = 0.5f;
}
