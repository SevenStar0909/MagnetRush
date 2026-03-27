using UnityEngine;

/// <summary>
/// LT入力でエイムモード（スロー＋カメラ寄り＋FOV変更）を制御する。
/// </summary>
public class AimController : MonoBehaviour
{
    [SerializeField] private PlayerSettings settings;
    [SerializeField] private CameraSettingsApplier cameraSettings;

    private PlayerInputHandler input;
    private PlayerStateManager states;
    /// <summary>
    /// エイム中かどうかを返す。
    /// </summary>
    public bool IsAiming { get; private set; }
    private float aimReleaseGrace; // LT離しのジッター防止用タイマー

    void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
        states = GetComponent<PlayerStateManager>();
    }

    void Update()
    {
        if (input.AimHeld)
        {
            aimReleaseGrace = settings.aimReleaseGraceTime;
            if (!IsAiming) StartAim();
        }
        else
        {
            // LTが離されても猶予時間内は解除しない（RT押下時のジッター防止）
            if (IsAiming)
            {
                aimReleaseGrace -= Time.unscaledDeltaTime;
                if (aimReleaseGrace <= 0f) StopAim();
            }
        }
    }

    /// <summary>
    /// エイムモードを開始する。スロー＋カメラ変更を適用する。
    /// </summary>
    public void StartAim()
    {
        IsAiming = true;
        Time.timeScale = settings.aimTimeScale;

        if (cameraSettings != null)
            cameraSettings.SetAimMode(true);

        if (states != null)
            states.Change<AimPlayerState>();
    }

    /// <summary>
    /// エイムモードを終了する。タイムスケールとカメラを元に戻す。
    /// </summary>
    public void StopAim()
    {
        IsAiming = false;
        Time.timeScale = 1f;

        if (cameraSettings != null)
            cameraSettings.SetAimMode(false);

        // 入力があればMove、なければIdleに戻る
        if (states != null)
        {
            if (input != null && input.MoveInput.sqrMagnitude > 0.01f)
                states.Change<MovePlayerState>();
            else
                states.Change<IdlePlayerState>();
        }
    }

    void OnDisable()
    {
        // シーン遷移・オブジェクト破棄時にスロー状態を強制解除
        Time.timeScale = 1f;
    }
}
