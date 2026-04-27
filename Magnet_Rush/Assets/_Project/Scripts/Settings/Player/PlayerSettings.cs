using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerSettings", menuName = "MagnetRush/PlayerSettings")]
public class PlayerSettings : ScriptableObject
{
    [Header("移動")]
    [FormerlySerializedAs("moveSpeed")]
    [Tooltip("最高移動速度（m/s）")]
    public float topSpeed = 6f;
    [Tooltip("加速度。高いほど素早く最高速度に達する")]
    public float acceleration = 30f;
    [Tooltip("減速度。高いほど素早く停止する")]
    public float deceleration = 25f;
    [Tooltip("方向転換時の横方向減衰。高いほどキビキビ曲がる")]
    public float turningDrag = 20f;
    [Tooltip("キャラクターの回転速度")]
    public float rotationSpeed = 15f;

    [Header("重力")]
    [Tooltip("重力加速度（負の値）")]
    public float gravity = -20f;
    [Tooltip("接地時の地面スナップ力。地面に吸い付く強さ")]
    public float snapForce = 2f;
    [Tooltip("接地判定のレイキャスト追加距離")]
    public float groundCheckDistance = 0.3f;
    [Tooltip("接地判定の対象レイヤー。未設定(0)の場合はDefault+Ground+Wallにフォールバック")]
    public LayerMask groundLayer;

    [Header("カメラ")]
    [Tooltip("カメラの水平感度")]
    public float cameraSensitivityX = 200f;
    [Tooltip("カメラの垂直感度")]
    public float cameraSensitivityY = 200f;
    [Tooltip("カメラとプレイヤーの距離")]
    public float cameraDistance = 5f;
    [Tooltip("肩越しカメラのオフセット (X=右, Y=上, Z=前)")]
    public Vector3 shoulderOffset;
    [Tooltip("カメラのピッチ下限（度）。負ほど下を向ける。-10 で地面貫通防止")]
    public float cameraPitchMin = -10f;
    [Tooltip("カメラのピッチ上限（度）。正ほど上を向ける")]
    public float cameraPitchMax = 60f;

    [Header("射撃")]
    [Tooltip("弾の発射位置の高さ（プレイヤー足元からの距離）")]
    public float firePointHeight = 1.2f;

    [Header("エイム")]
    [Tooltip("エイム解除後のスロー猶予時間（秒）")]
    public float aimReleaseGraceTime = 0.15f;
    [Tooltip("エイム中のゲーム内時間倍率（0.3=70%スロー）")]
    public float aimTimeScale = 0.3f;
    [Tooltip("エイム開始時に aimTimeScale へ到達するまでの秒数（実時間）")]
    public float aimEnterDuration = 0.12f;
    [Tooltip("エイム解除時に 1.0 へ復帰するまでの秒数（実時間）")]
    public float aimExitDuration = 0.18f;
    [Tooltip("エイム中の視野角（度）。小さいほどズーム")]
    public float aimFOV = 40f;
    [Tooltip("エイム中のカメラ距離")]
    public float aimCameraDistance = 3f;
    [Tooltip("エイム中の移動速度倍率")]
    public float aimMoveSpeedMultiplier = 0.5f;

    [Header("死亡・リスポーン")]
    [Tooltip("死亡からリスポーンまでの待機時間（秒）")]
    public float respawnDelay = 3f;

    [Header("斜面")]
    [Tooltip("上り坂での減速力")]
    public float slopeUpwardForce = 15f;
    [Tooltip("下り坂での加速力")]
    public float slopeDownwardForce = 25f;

    [Header("磁力")]
    [Tooltip("磁力への抵抗度（0=影響なし, 1=完全抵抗）")]
    public float magnetResistance = 0.5f;
    [Tooltip("外部力（磁力等）の指数減衰率。大きいほど早く減速する")]
    public float externalDrag = 3f;

    [Header("磁力回転")]
    [Tooltip("この外部力（magnitude）以上で空中回転開始")]
    public float pullOrientationThreshold = 5f;
    [Tooltip("磁力方向への回転速度")]
    public float pullOrientationSpeed = 8f;
}
