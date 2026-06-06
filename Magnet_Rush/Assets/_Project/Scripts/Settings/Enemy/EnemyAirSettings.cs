using UnityEngine;

[CreateAssetMenu(fileName = "EnemyAirSettings", menuName = "MagnetRush/EnemyAirSettings")]
[ClassLabelSO("空中敵設定")]
public class EnemyAirSettings : ScriptableObject
{
    [Header("[ステータス]")]
    [Label("最大HP")]
    [Tooltip("最大HP")]
    public int maxHp = 10;

    [Header("[移動]")]
    [Label("移動速度（m/s）")]
    [Tooltip("空中敵の移動速度（m/s）")]
    public float moveSpeed = 6f;

    [Label("加速度")]
    [Tooltip("移動方向への加速度")]
    public float acceleration = 12f;

    [Label("追跡範囲（m）")]
    [Tooltip("プレイヤーを追跡する範囲（m）")]
    public float chaseRange = 25f;

    [Label("停止距離（m）")]
    [Tooltip("プレイヤーに近づきすぎない距離")]
    public float stopDistance = 1.5f;

    [Header("[Entity移動]")]
    [Label("方向転換減衰")]
    [Tooltip("方向転換時の減衰（大きいほど素早く曲がる）")]
    public float turningDrag = 18f;

    [Label("減速度")]
    [Tooltip("追跡をやめた時の減速速度")]
    public float deceleration = 20f;

    [Label("回転速度")]
    [Tooltip("移動方向へ向く回転速度")]
    public float rotationSpeed = 12f;

    [Header("[攻撃]")]
    [Label("接触ダメージ")]
    [Tooltip("プレイヤーへ体当たりした時のダメージ")]
    public int impactDamage = 1;

    [Header("[物理]")]
    [Label("外部力減衰率")]
    [Tooltip("外部力（磁力等）の指数減衰率。大きいほど早く減速する")]
    public float externalDrag = 3f;
}
