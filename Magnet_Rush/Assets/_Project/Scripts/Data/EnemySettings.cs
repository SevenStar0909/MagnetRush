using UnityEngine;

[CreateAssetMenu(fileName = "EnemySettings", menuName = "MagnetRush/EnemySettings")]
public class EnemySettings : ScriptableObject
{
    [Header("ステータス")]
    [Tooltip("最大HP")]
    public int maxHp = 10;

    [Header("移動")]
    [Tooltip("NavMeshAgentの移動速度（m/s）")]
    public float moveSpeed = 3.5f;
    [Tooltip("NavMeshAgentの加速度")]
    public float acceleration = 8.0f;
    [Tooltip("プレイヤーを追跡する範囲（m）")]
    public float chaseRange = 20f;
    [Tooltip("プレイヤーとの停止距離（m）")]
    public float stopDistance = 1.5f;

    [Header("攻撃")]
    [Tooltip("1回の攻撃ダメージ")]
    public int attackDamage = 1;
    [Tooltip("攻撃可能距離（m）")]
    public float attackRange = 1.5f;
    [Tooltip("攻撃間隔（秒）")]
    public float attackInterval = 1.2f;
    [Tooltip("プレイヤーへの回転速度")]
    public float rotationSpeed = 10f;
    [Tooltip("攻撃ヒットボックスの持続時間（秒）")]
    public float attackHitboxDuration = 0.2f;
}
