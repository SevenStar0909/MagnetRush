using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// LT入力でエイムモード（スロー＋カメラ寄り＋FOV変更）を制御する。
/// </summary>
public class AimController : MonoBehaviour
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private PlayerSettings m_settings;
    [FormerlySerializedAs("cameraSettings")]
    [SerializeField] private CameraSettingsApplier m_cameraSettings;

    private PlayerInputHandler m_input;
    private PlayerStateManager m_states;
    /// <summary>
    /// エイム中かどうかを返す。
    /// </summary>
    public bool IsAiming { get; private set; }
    private float m_aimReleaseGrace; // LT離しのジッター防止用タイマー

    void Awake()
    {
        m_input = GetComponent<PlayerInputHandler>();
        m_states = GetComponent<PlayerStateManager>();
    }

    void Update()
    {
        if (m_input.AimHeld)
        {
            m_aimReleaseGrace = m_settings.aimReleaseGraceTime;
            if (!IsAiming) StartAim();
        }
        else
        {
            // LTが離されても猶予時間内は解除しない（RT押下時のジッター防止）
            if (IsAiming)
            {
                m_aimReleaseGrace -= Time.unscaledDeltaTime;
                if (m_aimReleaseGrace <= 0f) StopAim();
            }
        }
    }

    /// <summary>
    /// エイムモードを開始する。スロー＋カメラ変更を適用する。
    /// </summary>
    public void StartAim()
    {
        IsAiming = true;
        Time.timeScale = m_settings.aimTimeScale;

        if (m_cameraSettings != null)
            m_cameraSettings.SetAimMode(true);

        if (m_states != null)
            m_states.Change<AimPlayerState>();
    }

    /// <summary>
    /// エイムモードを終了する。タイムスケールとカメラを元に戻す。
    /// </summary>
    public void StopAim()
    {
        IsAiming = false;
        Time.timeScale = 1f;

        if (m_cameraSettings != null)
            m_cameraSettings.SetAimMode(false);

        // 入力があればMove、なければIdleに戻る
        if (m_states != null)
        {
            if (m_input != null && m_input.MoveInput.sqrMagnitude > 0.01f)
                m_states.Change<MovePlayerState>();
            else
                m_states.Change<IdlePlayerState>();
        }
    }

    void OnDisable()
    {
        // シーン遷移・オブジェクト破棄時にスロー状態を強制解除
        Time.timeScale = 1f;
    }
}
