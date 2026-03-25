using UnityEngine;

[CreateAssetMenu(fileName = "EnemySettings", menuName = "MagnetRush/EnemySettings")]
public class EnemySettings : ScriptableObject
{
    [Header("Status")]
    public int maxHp = 10;
    [Header("Move")]
    public float moveSpeed = 3.5f;
    public float acceleration = 8.0f;
    public float chaseRange = 20f;
    public float stopDistance = 1.5f;
    [Header("Attack")]
    public int attackDamage = 1;
    public float attackRange = 1.5f; // 攻撃の有効範囲
    public float attackInterval = 1.2f; // 攻撃のクールダウン時間
    public float attackHitboxDuration = 0.2f;
}
