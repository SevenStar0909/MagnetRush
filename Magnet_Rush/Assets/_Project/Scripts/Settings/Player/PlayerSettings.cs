using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerSettings", menuName = "MagnetRush/PlayerSettings")]
public class PlayerSettings : ScriptableObject
{
    [Header("[移動]")]
    [FormerlySerializedAs("moveSpeed")]
    [Label("最高移動速度（m/s）")]
    [Tooltip("最高移動速度（m/s）")]
    public float topSpeed = 6f;
    [Label("加速度")]
    [Tooltip("加速度。高いほど素早く最高速度に達する")]
    public float acceleration = 30f;
    [Label("減速度")]
    [Tooltip("減速度。高いほど素早く停止する")]
    public float deceleration = 25f;
    [Label("方向転換減衰")]
    [Tooltip("方向転換時の横方向減衰。高いほどキビキビ曲がる")]
    public float turningDrag = 20f;
    [Label("回転速度")]
    [Tooltip("キャラクターの回転速度")]
    public float rotationSpeed = 15f;

    [Header("[重力]")]
    [Label("重力加速度（負値）")]
    [Tooltip("重力加速度（負の値）")]
    public float gravity = -20f;
    [Label("接地スナップ力")]
    [Tooltip("接地時の地面スナップ力。地面に吸い付く強さ")]
    public float snapForce = 2f;
    [Label("接地判定追加距離")]
    [Tooltip("接地判定のレイキャスト追加距離")]
    public float groundCheckDistance = 0.3f;
    [Label("接地判定レイヤー")]
    [Tooltip("接地判定の対象レイヤー。未設定(0)の場合はDefault+Ground+Wallにフォールバック")]
    public LayerMask groundLayer;

    [Header("[カメラ]")]
    [Label("水平感度")]
    [Tooltip("カメラの水平感度")]
    public float cameraSensitivityX = 200f;
    [Label("垂直感度")]
    [Tooltip("カメラの垂直感度")]
    public float cameraSensitivityY = 200f;
    [Label("カメラ距離")]
    [Tooltip("カメラとプレイヤーの距離")]
    public float cameraDistance = 5f;
    [Label("肩越しオフセット (X=右, Y=上, Z=前)")]
    [Tooltip("肩越しカメラのオフセット (X=右, Y=上, Z=前)")]
    public Vector3 shoulderOffset;
    [Label("ピッチ下限（度）")]
    [Tooltip("カメラのピッチ下限（度）。負ほど下を向ける。-10 で地面貫通防止")]
    public float cameraPitchMin = -10f;
    [Label("ピッチ上限（度）")]
    [Tooltip("カメラのピッチ上限（度）。正ほど上を向ける")]
    public float cameraPitchMax = 60f;

    [Header("[射撃]")]
    [Label("発射位置の高さ（m）")]
    [Tooltip("弾の発射位置の高さ（プレイヤー足元からの距離）")]
    public float firePointHeight = 1.2f;

    [Header("[エイム]")]
    [Label("解除猶予時間（秒）")]
    [Tooltip("エイム解除後のスロー猶予時間（秒）")]
    public float aimReleaseGraceTime = 0.15f;
    [Label("スロー時間倍率")]
    [Tooltip("エイム中のゲーム内時間倍率（0.3=70%スロー）")]
    public float aimTimeScale = 0.3f;
    [Label("最大エイム時間（秒・0で無制限）")]
    [Tooltip("エイム継続時間の上限（実時間・秒）。0以下で無制限。上限到達後はLTを離すまで再エイム不可")]
    public float aimMaxDuration = 3f;
    [Label("スロー突入補間時間（秒）")]
    [Tooltip("エイム開始時に aimTimeScale へ到達するまでの秒数（実時間）")]
    public float aimEnterDuration = 0.12f;
    [Label("スロー解除補間時間（秒）")]
    [Tooltip("エイム解除時に 1.0 へ復帰するまでの秒数（実時間）")]
    public float aimExitDuration = 0.18f;
    [Label("視野角（度）")]
    [Tooltip("エイム中の視野角（度）。小さいほどズーム")]
    public float aimFOV = 40f;
    [Label("カメラ距離")]
    [Tooltip("エイム中のカメラ距離")]
    public float aimCameraDistance = 3f;
    [Label("移動速度倍率")]
    [Tooltip("エイム中の移動速度倍率")]
    public float aimMoveSpeedMultiplier = 0.5f;

    [Header("[死亡・リスポーン]")]
    [Label("リスポーン待機時間（秒）")]
    [Tooltip("死亡からリスポーンまでの待機時間（秒）")]
    public float respawnDelay = 3f;

    [Header("[斜面]")]
    [Label("上り坂減速力")]
    [Tooltip("上り坂での減速力")]
    public float slopeUpwardForce = 15f;
    [Label("下り坂加速力")]
    [Tooltip("下り坂での加速力")]
    public float slopeDownwardForce = 25f;

    [Header("[磁力]")]
    [Label("磁力への抵抗度（0=影響なし, 1=完全抵抗）")]
    [Tooltip("磁力への抵抗度（0=影響なし, 1=完全抵抗）")]
    public float magnetResistance = 0.5f;
    [Label("外部力減衰率")]
    [Tooltip("外部力（磁力等）の指数減衰率。大きいほど早く減速する")]
    public float externalDrag = 3f;

    [Header("[磁力回転]")]
    [Label("空中回転開始しきい値")]
    [Tooltip("この外部力（magnitude）以上で空中回転開始")]
    public float pullOrientationThreshold = 5f;
    [Label("磁力方向への回転速度")]
    [Tooltip("磁力方向への回転速度")]
    public float pullOrientationSpeed = 8f;
}
