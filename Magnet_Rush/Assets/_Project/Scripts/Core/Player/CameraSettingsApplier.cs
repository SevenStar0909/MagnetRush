using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.Cinemachine;

/// <summary>
/// TPS用カメラ制御。右スティック/マウスでカメラ回転し、エイム時にFOV・距離を切り替える。
/// ThirdPersonFollow用の回転ピボットを自動生成してFollow対象にセットする。
/// </summary>
[DefaultExecutionOrder(-200)]
public class CameraSettingsApplier : MonoBehaviour
{
    [FormerlySerializedAs("cinemachineCamera")]
    [SerializeField] private CinemachineCamera m_cinemachineCamera;

    private PlayerSettings m_settings;

    private CinemachineThirdPersonFollow m_thirdPersonFollow;
    private float m_defaultFOV;
    private float m_defaultCameraDistance;
    private Transform m_cameraPivot;
    private float m_yaw;
    private float m_pitch;
    private bool m_initialized;

    void OnEnable()
    {
        AimController.OnAimChanged += SetAimMode;
        Player.OnPlayerReady += InitializeWithPlayer;
        if (Player.Current != null) InitializeWithPlayer(Player.Current);
    }

    void OnDisable()
    {
        AimController.OnAimChanged -= SetAimMode;
        Player.OnPlayerReady -= InitializeWithPlayer;
    }

    private void InitializeWithPlayer(Player playerComponent)
    {
        if (m_initialized) { ChannelLogger.LogGuardReturn("Player", "既に初期化済み"); return; }
        if (m_cinemachineCamera == null) { ChannelLogger.LogGuardReturn("Player", "CinemachineCamera未設定"); return; }
        if (playerComponent == null) { ChannelLogger.LogGuardReturn("Player", "Playerコンポーネントなし"); return; }

        m_settings = playerComponent.Settings;
        var player = playerComponent.gameObject;

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

    void LateUpdate()
    {
        if (m_cameraPivot == null || m_settings == null) { ChannelLogger.LogGuardReturn("Player", "カメラピボットまたは設定なし"); return; }

        // 右スティック / マウスでカメラ回転
        Vector2 look = Vector2.zero;
        if (Mouse.current != null)
            look = Mouse.current.delta.ReadValue();
        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.rightStick.ReadValue();
            // スティック入力はフレーム非依存で大きめに
            look += stick * 5f;
        }

        m_yaw += look.x * m_settings.cameraSensitivityX * Time.unscaledDeltaTime;
        m_pitch -= look.y * m_settings.cameraSensitivityY * Time.unscaledDeltaTime;
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
}
