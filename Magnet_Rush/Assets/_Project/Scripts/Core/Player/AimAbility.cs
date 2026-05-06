using System;
using UnityEngine;

/// <summary>
/// エイム能力。LT 入力でエイムモードに入りスロー + カメラ固定ストレイフに遷移する。
/// 基底: Ability（共通の依存 m_input / m_player / m_events / m_states は基底で取得済み）
/// </summary>
public class AimAbility : Ability
{
    /// <summary>エイム中かどうか。</summary>
    public bool IsAiming { get; private set; }

    /// <summary>エイム状態変化時に発火。CameraSettingsApplier 等が購読。静的なのは Player.Current 未生成時点で購読可能にするため。</summary>
    public static event Action<bool> OnAimChanged;

    private float m_aimReleaseGrace;

    private float m_baselineFixedDeltaTime;
    private float m_targetTimeScale = 1f;

    // エイム継続時間の上限管理
    private float m_aimElapsed;
    // 上限到達後、LTを離すまで再エイムをロックするフラグ。連打による即座再エイムを防止
    private bool m_aimLockoutWhileHeld;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnAimChanged = null;
    }

    protected override void Awake()
    {
        base.Awake();
        // 元の物理タイムステップを退避。Time.timeScale 変動に追従させて実時間で物理を一定にする
        m_baselineFixedDeltaTime = Time.fixedDeltaTime;
    }

    void OnDisable()
    {
        // 無効化・シーン破棄時に確実に元へ戻す（スロー残留事故の保険）
        // OnDestroy より広く拾える（コンポーネント単独 disable でも動く）
        Time.timeScale = 1f;
        Time.fixedDeltaTime = m_baselineFixedDeltaTime;
        m_targetTimeScale = 1f;
        m_aimElapsed = 0f;
        m_aimLockoutWhileHeld = false;
    }

    void Update()
    {
        // 目標値へ実時間でなめらかに追従。スナップ切替えのカクつきを除去
        float duration = (m_targetTimeScale < Time.timeScale)
            ? m_player.Settings.aimEnterDuration
            : m_player.Settings.aimExitDuration;
        float maxDelta = (duration > 0f) ? (Time.unscaledDeltaTime / duration) : 1f;
        Time.timeScale = Mathf.MoveTowards(Time.timeScale, m_targetTimeScale, maxDelta);
        Time.fixedDeltaTime = m_baselineFixedDeltaTime * Time.timeScale;
    }

    /// <summary>LT 入力に応じてエイムモードを開始/維持する。Player.Update から毎フレーム呼ぶ。</summary>
    public void UpdateInput()
    {
        if (m_input.AimHeld)
        {
            // 上限到達でロック中は LT を離すまで再エイムさせない
            if (m_aimLockoutWhileHeld) return;

            m_aimReleaseGrace = m_player.Settings.aimReleaseGraceTime;
            if (!IsAiming) StartAim();

            // 実時間で経過を加算。Time.timeScale の影響を受けないため期待通りに上限が機能する
            m_aimElapsed += Time.unscaledDeltaTime;
            float max = m_player.Settings.aimMaxDuration;
            if (max > 0f && m_aimElapsed >= max)
            {
                m_aimLockoutWhileHeld = true;
                StopAim();
            }
        }
        else
        {
            // LT を離した瞬間にロック解除（次回押下で再エイム可能になる）
            m_aimLockoutWhileHeld = false;
            if (IsAiming)
            {
                m_aimReleaseGrace -= Time.unscaledDeltaTime;
                if (m_aimReleaseGrace <= 0f) StopAim();
            }
        }
    }

    /// <summary>エイムモード開始。スロー目標値セット + ステート遷移。実際の timeScale 補間は Update で進む。</summary>
    public void StartAim()
    {
        IsAiming = true;
        m_aimElapsed = 0f;
        m_targetTimeScale = m_player.Settings.aimTimeScale;
        OnAimChanged?.Invoke(true);
        m_states.Change<AimPlayerState>();
    }

    /// <summary>エイムモード終了。入力があれば Move、なければ Idle に戻る。timeScale は Update で 1.0 に戻る。</summary>
    public void StopAim()
    {
        IsAiming = false;
        m_targetTimeScale = 1f;
        OnAimChanged?.Invoke(false);

        if (m_input.MoveInput.sqrMagnitude > 0.01f)
            m_states.Change<MovePlayerState>();
        else
            m_states.Change<IdlePlayerState>();
    }
}
