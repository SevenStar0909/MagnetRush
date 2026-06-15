using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// TPS用カメラ制御。右スティック/マウスでカメラ回転し、エイム時にFOV・距離を切り替える。
/// ThirdPersonFollow用の回転ピボットを自動生成してFollow対象にセットする。
/// </summary>
[DefaultExecutionOrder(-200)]
public class CameraSettingsApplier : MonoBehaviour
{
    [SerializeField] private CinemachineCamera m_cinemachineCamera;

    [Header("死亡演出")]
    [Tooltip("死亡時にこのカメラ距離まで寄せてプレイヤーを大きく写す。0以下で寄り無効")]
    [SerializeField] private float m_deathZoomDistance = 2f;
    [Tooltip("死亡時の寄り・中央寄せにかける時間(秒・実時間)")]
    [SerializeField] private float m_deathZoomDuration = 1.2f;
    [Tooltip("死亡時に見る高さ(プレイヤー基準のローカルY)。倒れた体を画面中央に収めるため通常(1.2)より低くする")]
    [SerializeField] private float m_deathCenterHeight = 0.5f;

    [Header("カメラ演出設定")]
    [Tooltip("反発ジャンプ演出などカメラ演出のパラメータ（SO）")]
    [SerializeField] private PlayerCameraSettings m_cameraSettings;

    [Header("エイム演出")]
    [Tooltip("エイム時のFOV・距離を補間する速さ（大きいほど早い）")]
    [SerializeField] private float m_aimBlendSpeed = 15f;

    [Header("チュートリアル強制注目")]
    [Tooltip("ターゲットへ向き直る速さ（度/秒）")]
    [SerializeField] private float m_focusTurnSpeed = 200f;
    [Tooltip("注目FOVへ寄せる速さ（度/秒）")]
    [SerializeField] private float m_focusFovSpeed = 60f;

    private PlayerSettings m_settings;
    private Health m_health;

    private CinemachineThirdPersonFollow m_thirdPersonFollow;
    private CinemachineCameraOffset m_damageShakeOffset;
    private float m_defaultFOV;
    private float m_defaultCameraDistance;
    private Transform m_cameraPivot;
    private float m_yaw;
    private float m_pitch;
    private bool m_initialized;
    private bool m_isFrozen;

    private bool m_isDead;
    private float m_deathBlend;
    private float m_deathStartPivotY;
    private float m_deathStartShoulderX;
    private float m_deathStartDistance;

    private Player m_player;
    private Magnetizable m_playerMagnetizable;
    private bool m_isAiming;
    private float m_repulsePullCurrent;
    private float m_repulseActiveTimer;

    private float m_currentFOV;
    private float m_currentCameraDistance;

    private Transform m_focusTarget;
    private float m_focusFov;
    private bool m_isFocusing;

    private float m_damageShakeAmplitude;
    private float m_damageShakeDuration;
    private float m_damageShakeTimer;
    private float m_damageShakeFrequency;
    private Vector3 m_damageShakeSeed;

    void OnEnable()
    {
        AimAbility.OnAimChanged += SetAimMode;
        Player.OnPlayerReady += InitializeWithPlayer;
        Player.OnFallRespawnStart += OnFallRespawnStart;
        Player.OnFallRespawnEnd += OnFallRespawnEnd;
        MagneticContactDamage.OnEnemyDamageCameraShake += HandleEnemyDamageCameraShake;
        if (Player.Current != null) InitializeWithPlayer(Player.Current);
    }

    void OnDisable()
    {
        AimAbility.OnAimChanged -= SetAimMode;
        Player.OnPlayerReady -= InitializeWithPlayer;
        Player.OnFallRespawnStart -= OnFallRespawnStart;
        Player.OnFallRespawnEnd -= OnFallRespawnEnd;
        MagneticContactDamage.OnEnemyDamageCameraShake -= HandleEnemyDamageCameraShake;
        if (m_health != null) m_health.OnDie -= HandlePlayerDeath;
        if (m_playerMagnetizable != null) m_playerMagnetizable.OnRepulsionForce -= HandleRepulsionForce;
        ResetDamageShake();
    }

    private void InitializeWithPlayer(Player playerComponent)
    {
        if (m_initialized) { ChannelLogger.LogGuardReturn("Player", "既に初期化済み"); return; }
        if (m_cinemachineCamera == null) { ChannelLogger.LogGuardReturn("Player", "CinemachineCamera未設定"); return; }
        if (playerComponent == null) { ChannelLogger.LogGuardReturn("Player", "Playerコンポーネントなし"); return; }

        m_settings = playerComponent.Settings;
        var player = playerComponent.gameObject;

        m_health = playerComponent.GetComponent<Health>();
        if (m_health != null) m_health.OnDie += HandlePlayerDeath;

        m_player = playerComponent;
        m_playerMagnetizable = playerComponent.magnetizable;
        if (m_playerMagnetizable != null) m_playerMagnetizable.OnRepulsionForce += HandleRepulsionForce;

        // カメラ回転ピボットをプレイヤーの子に生成
        var pivotGO = new GameObject("CameraPivot");
        pivotGO.transform.SetParent(player.transform, false);
        // 肩の高さ付近にピボットを配置（キャラの頭上ではなく肩越し視点になるように）
        pivotGO.transform.localPosition = Vector3.up * 1.2f;
        m_cameraPivot = pivotGO.transform;

        m_cinemachineCamera.Follow = m_cameraPivot;
        m_cinemachineCamera.LookAt = m_cameraPivot;

        m_damageShakeOffset = m_cinemachineCamera.GetComponent<CinemachineCameraOffset>();
        if (m_damageShakeOffset == null)
            m_damageShakeOffset = m_cinemachineCamera.gameObject.AddComponent<CinemachineCameraOffset>();
        m_damageShakeOffset.ApplyAfter = CinemachineCore.Stage.Aim;
        m_damageShakeOffset.PreserveComposition = false;
        m_damageShakeOffset.Offset = Vector3.zero;

        m_thirdPersonFollow = m_cinemachineCamera.GetComponent<CinemachineThirdPersonFollow>();
        m_defaultFOV = m_cinemachineCamera.Lens.FieldOfView;
        m_currentFOV = m_defaultFOV;

        if (m_thirdPersonFollow != null)
        {
            // TPS標準値を強制セット（プレハブ値よりコード値を優先）
            m_thirdPersonFollow.ShoulderOffset = new Vector3(0.5f, 0f, 0f);
            m_thirdPersonFollow.VerticalArmLength = 0f;
            m_thirdPersonFollow.CameraDistance = 3.5f;
            m_thirdPersonFollow.CameraSide = 1f;
            m_thirdPersonFollow.Damping = new Vector3(0.05f, 0.2f, 0.1f);
            m_defaultCameraDistance = m_thirdPersonFollow.CameraDistance;

            // SOの値があれば上書き
            if (m_settings != null)
            {
                if (m_settings.shoulderOffset.sqrMagnitude > 0.001f)
                    m_thirdPersonFollow.ShoulderOffset = m_settings.shoulderOffset;
                if (m_settings.cameraDistance > 0f)
                {
                    m_thirdPersonFollow.CameraDistance = m_settings.cameraDistance;
                    m_defaultCameraDistance = m_settings.cameraDistance;
                }
            }

            m_currentCameraDistance = m_defaultCameraDistance;
        }

        // 初期角度: ほぼ水平（ピッチ0°でプレイヤーの背後から水平に見る）
        m_yaw = player.transform.eulerAngles.y;
        m_pitch = 2f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        m_initialized = true;
    }

    // マウスのピクセル差分を度に換算する係数。
    // Mouse.delta は「このフレームの累積ピクセル」なので deltaTime を掛けてはいけない。
    // 掛けると FPS スパイク時に感度が一気に跳ねてカクつく原因になる。
    private const float k_MousePixelToDegree = 0.05f;

    void LateUpdate()
    {
        // 死亡中は寄せ＋中央寄せを毎フレーム適用（Play中にInspector値を変えると即反映＝ライブ調整可）
        if (m_isDead && m_thirdPersonFollow != null) { UpdateDeathFraming(); UpdateDamageShake(); return; }

        // 凍結中は入力でピボットを回さない。落下→復帰の間カメラを静止させる
        if (m_isFrozen) { ResetDamageShake(); return; }
        if (m_cameraPivot == null || m_settings == null) { ChannelLogger.LogGuardReturn("Player", "カメラピボットまたは設定なし"); return; }

        // チュートリアルでカメラロック中は入力で回さない（追従・エイム距離・反発演出はそのまま動かす）
        bool cameraLocked = m_player != null && m_player.IsAbilityLocked(PlayerAbilityType.Camera);
        if (!cameraLocked)
        {
            // マウス: ピクセル差分 (フレーム独立)。deltaTime を掛けない。
            if (Mouse.current != null)
            {
                Vector2 mouseLook = Mouse.current.delta.ReadValue();
                m_yaw += mouseLook.x * m_settings.cameraMouseSensitivityX * k_MousePixelToDegree;
                m_pitch -= mouseLook.y * m_settings.cameraMouseSensitivityY * k_MousePixelToDegree;
            }

            // パッド: アナログ軸 (連続値)。deltaTime を掛けて時間積分する。
            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.rightStick.ReadValue();
                m_yaw += stick.x * m_settings.cameraSensitivityX * Time.unscaledDeltaTime;
                m_pitch -= stick.y * m_settings.cameraSensitivityY * Time.unscaledDeltaTime;
            }
        }

        // チュートリアルの強制注目中はターゲット方向へヨー/ピッチを寄せる（カメラ入力ロック中に呼ばれる想定）
        if (m_isFocusing && m_focusTarget != null)
            ApplyFocus();

        m_pitch = Mathf.Clamp(m_pitch, m_settings.cameraPitchMin, m_settings.cameraPitchMax);
        m_cameraPivot.rotation = Quaternion.Euler(m_pitch, m_yaw, 0f);

        UpdateRepulsePull();


        UpdateAimAndApplyCamera();

    }

    /// <summary>
    /// 反発力を受けた時のコールバック。空中でしきい値以上の反発を受けている間だけ
    /// 引き演出のタイマーを更新する（反発中は毎FixedUpdate呼ばれる）。
    /// </summary>
    private void HandleRepulsionForce(Vector3 force)
    {
        if (m_cameraSettings == null || m_cameraSettings.repulsePullDistance <= 0f) return;
        if (m_player == null || m_player.IsGrounded)
        {
            ChannelLogger.LogGuardReturn("Player", "接地中は反発カメラ演出なし");
            return;
        }
        if (force.sqrMagnitude < m_cameraSettings.repulseForceThreshold * m_cameraSettings.repulseForceThreshold)
        {
            ChannelLogger.LogGuardReturn("Player", "反発が弱いためカメラ演出なし");
            return;
        }

        // 反発が続く間は毎FixedUpdate届くので、短い猶予で更新し続ける
        m_repulseActiveTimer = 0.15f;
    }

    /// <summary>
    /// 反発ジャンプ中のカメラ引きを毎フレームブレンドする。
    /// エイム距離への加算方式なので、エイム切替・通常距離のどちらとも干渉しない。
    /// </summary>
    private void UpdateRepulsePull()
    {
        if (m_thirdPersonFollow == null || m_settings == null || m_cameraSettings == null) return;
        if (m_cameraSettings.repulsePullDistance <= 0f && m_repulsePullCurrent <= 0f) return;

        m_repulseActiveTimer -= Time.deltaTime;
        bool active = m_repulseActiveTimer > 0f;

        float target = active ? m_cameraSettings.repulsePullDistance : 0f;
        float duration = Mathf.Max(0.01f, active ? m_cameraSettings.repulsePullInDuration : m_cameraSettings.repulsePullOutDuration);
        float speed = Mathf.Max(0.01f, m_cameraSettings.repulsePullDistance) / duration;
        m_repulsePullCurrent = Mathf.MoveTowards(m_repulsePullCurrent, target, speed * Time.deltaTime);
    }

    private void HandleEnemyDamageCameraShake(float amplitude, float duration, float frequency)
    {
        if (!m_initialized || m_damageShakeOffset == null) return;

        amplitude = Mathf.Max(0f, amplitude);
        duration = Mathf.Max(0.01f, duration);
        if (amplitude <= 0f) return;

        float currentAmplitude = m_damageShakeAmplitude * GetDamageShakeEnvelope();
        m_damageShakeAmplitude = Mathf.Max(currentAmplitude, amplitude);
        m_damageShakeDuration = duration;
        m_damageShakeTimer = duration;
        m_damageShakeFrequency = Mathf.Max(0.01f, frequency);
        m_damageShakeSeed = new Vector3(
            UnityEngine.Random.value * 100f,
            UnityEngine.Random.value * 100f,
            UnityEngine.Random.value * 100f);
    }

    private void UpdateDamageShake()
    {
        if (m_damageShakeOffset == null) return;
        if (m_damageShakeTimer <= 0f)
        {
            m_damageShakeOffset.Offset = Vector3.zero;
            return;
        }

        m_damageShakeTimer = Mathf.Max(0f, m_damageShakeTimer - Time.unscaledDeltaTime);
        float envelope = GetDamageShakeEnvelope();
        float t = Time.unscaledTime * m_damageShakeFrequency;
        Vector3 noise = new Vector3(
            NoiseSigned(m_damageShakeSeed.x, t),
            NoiseSigned(m_damageShakeSeed.y, t),
            NoiseSigned(m_damageShakeSeed.z, t) * 0.35f);

        m_damageShakeOffset.Offset = noise * (m_damageShakeAmplitude * envelope);
    }

    private float GetDamageShakeEnvelope()
    {
        if (m_damageShakeDuration <= 0f || m_damageShakeTimer <= 0f) return 0f;

        float progress = 1f - Mathf.Clamp01(m_damageShakeTimer / m_damageShakeDuration);
        return 1f - Mathf.SmoothStep(0f, 1f, progress);
    }

    private static float NoiseSigned(float seed, float t)
    {
        return (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f;
    }

    private void ResetDamageShake()
    {
        m_damageShakeTimer = 0f;
        m_damageShakeAmplitude = 0f;
        if (m_damageShakeOffset != null)
            m_damageShakeOffset.Offset = Vector3.zero;
    }

    /// <summary>
    /// エイムモード切替。
    /// </summary>
    public void SetAimMode(bool aiming)
    {
        m_isAiming = aiming;
    }

    /// <summary>
    /// エイム状態に応じた目標値へぬるっと近づけ、最終的な距離・FOVをカメラへ適用する
    /// </summary>
    private void UpdateAimAndApplyCamera()
    {
        if (m_thirdPersonFollow == null || m_settings == null) return;

        // 目標値の設定
        float targetDistance = m_isAiming ? m_settings.aimCameraDistance : m_defaultCameraDistance;
        float targetFOV = m_isAiming ? m_settings.aimFOV : m_defaultFOV;

        // 毎フレーム補間 (スローモーションの影響を受けないよう unscaledDeltaTime を使用)
        m_currentCameraDistance = Mathf.Lerp(m_currentCameraDistance, targetDistance, m_aimBlendSpeed * Time.unscaledDeltaTime);
        m_currentFOV = Mathf.Lerp(m_currentFOV, targetFOV, m_aimBlendSpeed * Time.unscaledDeltaTime);

        // 距離の適用 (ベース距離 + 反発演出の距離)
        m_thirdPersonFollow.CameraDistance = m_currentCameraDistance + m_repulsePullCurrent;

        // FOVの適用 (※強制注目中は ApplyFocus 側で操作するため、競合を避ける)
        if (!m_isFocusing && m_cinemachineCamera != null)
        {
            var lens = m_cinemachineCamera.Lens;
            lens.FieldOfView = m_currentFOV;
            m_cinemachineCamera.Lens = lens;
        }
    }

    /// <summary>
    /// 指定ターゲットへカメラを向け、FOVを寄せる（チュートリアルの強制注目用）。
    /// カメラ操作ロック中に毎フレーム呼ぶ想定。target が null のときは何もしない。
    /// </summary>
    public void FocusOn(Transform target, float fov)
    {
        if (target == null) return;
        m_focusTarget = target;
        m_focusFov = fov;
        m_isFocusing = true;
    }

    /// <summary>強制注目を解除し、FOVを通常（エイム/既定）へ戻す。注目していないときは何もしない。</summary>
    public void ClearFocus()
    {
        if (!m_isFocusing) return;
        m_isFocusing = false;
        m_focusTarget = null;

        if (m_cinemachineCamera != null)
        {
            m_currentFOV = m_cinemachineCamera.Lens.FieldOfView;
        }
    }

    // ピボットのヨー/ピッチをターゲット方向へ寄せ、FOVを注目値へ寄せる。LateUpdateから呼ぶ。
    private void ApplyFocus()
    {
        Vector3 dir = m_focusTarget.position - m_cameraPivot.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 n = dir.normalized;
            float desiredYaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
            float desiredPitch = -Mathf.Asin(Mathf.Clamp(n.y, -1f, 1f)) * Mathf.Rad2Deg;
            m_yaw = Mathf.MoveTowardsAngle(m_yaw, desiredYaw, m_focusTurnSpeed * Time.unscaledDeltaTime);
            m_pitch = Mathf.MoveTowards(m_pitch, desiredPitch, m_focusTurnSpeed * Time.unscaledDeltaTime);
        }

        if (m_cinemachineCamera != null && m_focusFov > 0f)
        {
            var lens = m_cinemachineCamera.Lens;
            lens.FieldOfView = Mathf.MoveTowards(lens.FieldOfView, m_focusFov, m_focusFovSpeed * Time.unscaledDeltaTime);
            m_cinemachineCamera.Lens = lens;
        }
    }

    private void OnFallRespawnStart() => Freeze(true);
    private void OnFallRespawnEnd() => Freeze(false);

    /// <summary>
    /// カメラを止める/再開する。止める間は追従対象を外して本体をその場に固定し、
    /// 落下するプレイヤーを追わない。再開時は追従を戻し、ダンピングなしで新しい足場へカットする。
    /// </summary>
    /// <param name="value">true で凍結、false で解除</param>
    private void Freeze(bool value)
    {
        m_isFrozen = value;

        if (m_cinemachineCamera == null) { ChannelLogger.LogGuardReturn("Player", "CinemachineCamera未設定 — 凍結スキップ"); return; }

        if (value)
        {
            m_cinemachineCamera.Follow = null;
            m_cinemachineCamera.LookAt = null;
        }
        else
        {
            m_cinemachineCamera.Follow = m_cameraPivot;
            m_cinemachineCamera.LookAt = m_cameraPivot;
            // 前フレーム状態を破棄して、復帰先の足場へ補間なしで即カットさせる
            m_cinemachineCamera.PreviousStateIsValid = false;
        }
    }

    // 死亡時: 寄せ＋中央寄せの開始値を記録してフラグを立てる。実適用は LateUpdate の UpdateDeathFraming が毎フレーム行う。
    private void HandlePlayerDeath()
    {
        if (m_thirdPersonFollow == null || m_deathZoomDistance <= 0f) { ChannelLogger.LogGuardReturn("Player", "死亡カメラ寄り: ThirdPersonFollowなしまたは無効"); return; }
        if (m_isDead) { ChannelLogger.LogGuardReturn("Player", "既に死亡フレーミング中"); return; }

        m_deathStartDistance = m_thirdPersonFollow.CameraDistance;
        m_deathStartShoulderX = m_thirdPersonFollow.ShoulderOffset.x;
        m_deathStartPivotY = m_cameraPivot != null ? m_cameraPivot.localPosition.y : m_deathCenterHeight;
        m_deathBlend = 0f;
        m_isDead = true;
        m_isFrozen = true;
    }

    // 死亡中、毎フレーム「寄せ＋中央寄せ」を適用する。
    // 通常は肩越し(オフセットX)＋胸の高さ(1.2)を見るので、倒れた体が画面中央下に映る。
    // 死亡時は横=肩オフセットを0(真後ろ)、縦=見る高さ(m_deathCenterHeight)へ寄せてプレイヤーを画面中央に収める。
    // 毎フレーム m_deathCenterHeight 等を読むので、Play中に Inspector でいじると即反映される（ライブ調整可）。
    private void UpdateDeathFraming()
    {
        float dur = Mathf.Max(0.01f, m_deathZoomDuration);
        m_deathBlend = Mathf.MoveTowards(m_deathBlend, 1f, Time.unscaledDeltaTime / dur);
        float k = Mathf.SmoothStep(0f, 1f, m_deathBlend);

        m_thirdPersonFollow.CameraDistance = Mathf.Lerp(m_deathStartDistance, m_deathZoomDistance, k);

        Vector3 shoulder = m_thirdPersonFollow.ShoulderOffset;
        shoulder.x = Mathf.Lerp(m_deathStartShoulderX, 0f, k);
        m_thirdPersonFollow.ShoulderOffset = shoulder;

        if (m_cameraPivot != null)
        {
            Vector3 lp = m_cameraPivot.localPosition;
            lp.y = Mathf.Lerp(m_deathStartPivotY, m_deathCenterHeight, k);
            m_cameraPivot.localPosition = lp;
        }
    }
}
