using UnityEngine;

[CreateAssetMenu(fileName = "EnemyTurretSettings", menuName = "MagnetRush/EnemyTurretSettings")]
public class EnemyTurretSettings : ScriptableObject
{
    [Header("[ステータス]")]
    [Label("最大HP")]
    [Tooltip("最大HP")]
    public int maxHp = 10;

    [Header("[索敵・照準]")]
    [Label("磁性オブジェクト探索半径（m）")]
    [Tooltip("磁性オブジェクト探索半径（m）。0以下ならMagnetSettings.magnetRangeを使用。")]
    public float detectionMagRange = 0f;
    [Label("プレイヤー追従距離（m・0で無制限）")]
    [Tooltip("未磁化時にプレイヤーを追従する最大距離（m）。0以下なら無制限。")]
    public float aimToPlayerRange = 20f;
    [Label("ターゲット探索更新間隔（秒）")]
    [Tooltip("ターゲット探索更新間隔（秒）")]
    public float targetRefreshInterval = 0.1f;
    [Label("回転速度（度/秒）")]
    [Tooltip("回転速度（度/秒）")]
    public float rotationSpeed = 120f;

    [Header("[射撃]")]
    [Label("発射間隔（秒）")]
    [Tooltip("発射間隔（秒）")]
    public float shootInterval = 1.0f;
    [LabelRange("照準一致しきい値", -1f, 1f)]
    [Tooltip("砲口前方向と照準方向の一致閾値（-1〜1）")]
    public float shootMinDot = 0.95f;
    [Label("発射弾数")]
    [Tooltip("1回の発射で撃つ弾数")]
    public int burstCount = 1;
    [Label("バースト弾間隔（秒）")]
    [Tooltip("バースト内の弾間隔（秒）")]
    public float burstInterval = 0.15f;
    [Label("磁化中のみ発射")]
    [Tooltip("ONなら磁化中のみ発射")]
    public bool shootOnlyWhenMagnetized = false;
}
