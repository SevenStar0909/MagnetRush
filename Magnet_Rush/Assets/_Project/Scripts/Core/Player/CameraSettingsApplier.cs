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

    private PlayerSettings m_settings;
    private Health m_health;

    private CinemachineThirdPersonFollow m_thirdPersonFollow;
    private float m_defaultFOV;
    private float m_defaultCameraDistance;
    private Transform m_cameraPivot;
    private float m_yaw;
    private float m_pitch;
    private bool m_initialized;
    private bool m_isFrozen;

    void OnEnable()
    {
        AimAbility.OnAimChanged += SetAimMode;
        Player.OnPlayerReady += InitializeWithPlayer;
        Player.OnFallRespawnStart += OnFallRespawnStart;
        Player.OnFallRespawnEnd += OnFallRespawnEnd;
        if (Player.Current != null) InitializeWithPlayer(Player.Current);
    }

    void OnDisable()
    {
        AimAbility.OnAimChanged -= SetAimMode;
        Player.OnPlayerReady -= InitializeWithPlayer;
        Player.OnFallRespawnStart -= OnFallRespawnStart;
        Player.OnFallRespawnEnd -= OnFallRespawnEnd;
        if (m_health != null) m_health.OnDie -= HandlePlayerDeath;
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

        // カメラ回転ピボットをプレイヤーの子に生成
        var pivotGO = new GameObject("CameraPivot");
        pivotGO.transform.SetParent(player.transform, false);
        // 肩の高さ付近にピボットを配置（キャラの頭上ではなく肩越し視点になるように）
        pivotGO.transform.localPosition = Vector3.up * 1.2f;
        m_cameraPivot = pivotGO.transform;

        m_cinemachineCamera.Follow = m_cameraPivot;
        m_cinemachineCamera.LookAt = m_cameraPivot;

        m_thirdPersonFollow = m_cinemachineCamera.GetComponent<CinemachineThirdPersonFollow>();
        m_defaultFOV = m_cinemachineCamera.Lens.FieldOfView;

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
        // 凍結中は入力でピボットを回さない。落下→復帰の間カメラを静止させる
        if (m_isFrozen) return;
        if (m_cameraPivot == null || m_settings == null) { ChannelLogger.LogGuardReturn("Player", "カメラピボットまたは設定なし"); return; }

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

        m_pitch = Mathf.Clamp(m_pitch, m_settings.cameraPitchMin, m_settings.cameraPitchMax);
        m_cameraPivot.rotation = Quaternion.Euler(m_pitch, m_yaw, 0f);
    }

    /// <summary>
    /// エイムモード切替。カメラ距離とFOVを変更する。
    /// </summary>
    public void SetAimMode(bool aiming)
    {
        if (m_thirdPersonFollow == null || m_settings == null) { ChannelLogger.LogGuardReturn("Player", "ThirdPersonFollowまたは設定なし"); return; }

        m_thirdPersonFollow.CameraDistance = aiming ? m_settings.aimCameraDistance : m_defaultCameraDistance;

        if (m_cinemachineCamera != null)
        {
            var lens = m_cinemachineCamera.Lens;
            lens.FieldOfView = aiming ? m_settings.aimFOV : m_defaultFOV;
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

    // 死亡時: カメラをプレイヤーへ寄せて死亡を大きく見せる。入力での回転は止める（追従は維持）。
    private void HandlePlayerDeath()
    {
        if (m_thirdPersonFollow == null || m_deathZoomDistance <= 0f) { ChannelLogger.LogGuardReturn("Player", "死亡カメラ寄り: ThirdPersonFollowなしまたは無効"); return; }
        m_isFrozen = true;
        StartCoroutine(DeathZoomRoutine());
    }

    // 死亡ビートのスロー/停止中でも進むよう実時間(unscaled)で、寄せ＋画面中央寄せを同時に行う。
    // 通常は肩越し(オフセットX)＋胸の高さ(1.2)を見るので、倒れた体が画面中央下に映る。
    // 死亡時は横=肩オフセットを0(真後ろ)、縦=見る高さを倒れた体へ下げて、プレイヤーを画面中央に収める。
    private IEnumerator DeathZoomRoutine()
    {
        float startDist = m_thirdPersonFollow.CameraDistance;
        Vector3 startShoulder = m_thirdPersonFollow.ShoulderOffset;
        float startPivotY = m_cameraPivot != null ? m_cameraPivot.localPosition.y : m_deathCenterHeight;

        float dur = Mathf.Max(0.01f, m_deathZoomDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);

            m_thirdPersonFollow.CameraDistance = Mathf.Lerp(startDist, m_deathZoomDistance, k);
            m_thirdPersonFollow.ShoulderOffset = new Vector3(Mathf.Lerp(startShoulder.x, 0f, k), startShoulder.y, startShoulder.z);

            if (m_cameraPivot != null)
            {
                Vector3 lp = m_cameraPivot.localPosition;
                lp.y = Mathf.Lerp(startPivotY, m_deathCenterHeight, k);
                m_cameraPivot.localPosition = lp;
            }
            yield return null;
        }

        m_thirdPersonFollow.CameraDistance = m_deathZoomDistance;
        m_thirdPersonFollow.ShoulderOffset = new Vector3(0f, startShoulder.y, startShoulder.z);
        if (m_cameraPivot != null)
        {
            Vector3 lp = m_cameraPivot.localPosition;
            lp.y = m_deathCenterHeight;
            m_cameraPivot.localPosition = lp;
        }
    }
}
