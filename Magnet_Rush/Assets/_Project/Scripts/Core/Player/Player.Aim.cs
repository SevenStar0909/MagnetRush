using System;
using UnityEngine;

/// <summary>
/// Player のエイム制御部分（partial）。
/// LT 入力でエイムモードに入りスロー + カメラ固定ストレイフに遷移する。
/// </summary>
public partial class Player
{
    // --- エイム制御 ---

    /// <summary>エイム中かどうか。</summary>
    public bool IsAiming { get; private set; }

    /// <summary>エイム状態変化時に発火。CameraSettingsApplier 等が購読。</summary>
    public static event Action<bool> OnAimChanged;

    private float m_aimReleaseGrace;

    /// <summary>LT 入力に応じてエイムモードを開始/維持する。毎フレーム呼ぶ。</summary>
    public void HandleAimInput()
    {
        if (input.AimHeld)
        {
            m_aimReleaseGrace = m_settings.aimReleaseGraceTime;
            if (!IsAiming) StartAim();
        }
        else if (IsAiming)
        {
            m_aimReleaseGrace -= Time.unscaledDeltaTime;
            if (m_aimReleaseGrace <= 0f) StopAim();
        }
    }

    /// <summary>エイムモード開始。スロー + ステート遷移。</summary>
    public void StartAim()
    {
        IsAiming = true;
        Time.timeScale = m_settings.aimTimeScale;
        OnAimChanged?.Invoke(true);
        states.Change<AimPlayerState>();
    }

    /// <summary>エイムモード終了。入力があれば Move、なければ Idle に戻る。</summary>
    public void StopAim()
    {
        IsAiming = false;
        Time.timeScale = 1f;
        OnAimChanged?.Invoke(false);

        if (input != null && input.MoveInput.sqrMagnitude > 0.01f)
            states.Change<MovePlayerState>();
        else
            states.Change<IdlePlayerState>();
    }
}
