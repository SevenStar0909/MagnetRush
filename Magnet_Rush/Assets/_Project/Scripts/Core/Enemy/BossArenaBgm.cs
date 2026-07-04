using UnityEngine;

/// <summary>
/// ボスアリーナ封鎖と連動してボス戦BGMを制御する。
/// EnemyBossArenaGate の Sealed（プレイヤー踏み込み→封鎖）でBGMを再生し、
/// Cleared（ボス撃破→壁解除）で停止する。
/// 依存: EnemyBossArenaGate, Sound
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBossArenaGate))]
public class BossArenaBgm : MonoBehaviour
{
    [Tooltip("封鎖した瞬間に流すBGMのキュー名")]
    [SerializeField] private string m_bgmCue = SoundData.BGM.BossBattle;

    [Tooltip("ONならボス撃破で壁が解除された瞬間にBGMを止める")]
    [SerializeField] private bool m_stopOnCleared = true;

    private EnemyBossArenaGate m_gate;

    void Awake()
    {
        m_gate = GetComponent<EnemyBossArenaGate>();
    }

    void OnEnable()
    {
        if (m_gate != null)
        {
            m_gate.Sealed += HandleSealed;
            m_gate.Cleared += HandleCleared;
        }
    }

    void OnDisable()
    {
        if (m_gate != null)
        {
            m_gate.Sealed -= HandleSealed;
            m_gate.Cleared -= HandleCleared;
        }
    }

    private void HandleSealed()
    {
        if (string.IsNullOrEmpty(m_bgmCue)) { ChannelLogger.LogGuardReturn("Enemy", "BossArenaBgm: BGMキュー名が空"); return; }
        Sound.PlayBgm(m_bgmCue);
    }

    private void HandleCleared()
    {
        if (!m_stopOnCleared) { ChannelLogger.LogGuardReturn("Enemy", "BossArenaBgm: 停止OFF設定のためBGM継続"); return; }
        Sound.StopBgm();
    }
}
