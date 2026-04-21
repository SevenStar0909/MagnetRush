using System;
using UnityEngine;

/// <summary>
/// エイム制御コンポーネント。LT 入力でエイムモードに入りスロー + カメラ固定ストレイフに遷移する。
/// 依存: PlayerInputHandler, PlayerStateManager, Player（PlayerSettings 参照用、同 GameObject）
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerStateManager))]
[RequireComponent(typeof(Player))]
public class AimController : MonoBehaviour
{
    /// <summary>エイム中かどうか。</summary>
    public bool IsAiming { get; private set; }

    /// <summary>エイム状態変化時に発火。CameraSettingsApplier 等が購読。静的なのは Player.Current 未生成時点で購読可能にするため。</summary>
    public static event Action<bool> OnAimChanged;

    private PlayerInputHandler m_input;
    private PlayerStateManager m_states;
    private Player m_player;
    private float m_aimReleaseGrace;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnAimChanged = null;
    }

    void Awake()
    {
        m_input = GetComponent<PlayerInputHandler>();
        m_states = GetComponent<PlayerStateManager>();
        m_player = GetComponent<Player>();
    }

    /// <summary>LT 入力に応じてエイムモードを開始/維持する。毎フレーム呼ぶ。</summary>
    public void HandleAimInput()
    {
        if (m_input.AimHeld)
        {
            m_aimReleaseGrace = m_player.Settings.aimReleaseGraceTime;
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
        Time.timeScale = m_player.Settings.aimTimeScale;
        OnAimChanged?.Invoke(true);
        m_states.Change<AimPlayerState>();
    }

    /// <summary>エイムモード終了。入力があれば Move、なければ Idle に戻る。</summary>
    public void StopAim()
    {
        IsAiming = false;
        Time.timeScale = 1f;
        OnAimChanged?.Invoke(false);

        if (m_input.MoveInput.sqrMagnitude > 0.01f)
            m_states.Change<MovePlayerState>();
        else
            m_states.Change<IdlePlayerState>();
    }
}
