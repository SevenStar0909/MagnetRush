using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public class BossRoomTimelineController : MonoBehaviour
{
    private const string kLogPrefix = "[boss gate]";

    [SerializeField] private EnemyBossArenaGate m_gate;
    [SerializeField] private PlayableDirector m_director;
    [SerializeField] private EnemyBossAI m_bossAI;

    private bool m_started;

    private void Awake()
    {
        if (m_gate == null)
            m_gate = GetComponent<EnemyBossArenaGate>();
        if (m_director == null)
            m_director = GetComponent<PlayableDirector>();
        if (m_bossAI == null)
            m_bossAI = FindFirstObjectByType<EnemyBossAI>(FindObjectsInactive.Include);

        if (m_bossAI != null)
        {
            m_bossAI.setisBattleing(false);
        }

        Debug.Log($"{kLogPrefix} timeline controller awake gate={(m_gate != null)} director={(m_director != null)} playable={(m_director != null && m_director.playableAsset != null)} bossAI={(m_bossAI != null)}", this);
    }

    private void OnEnable()
    {
        if (m_gate != null)
            m_gate.Sealed += HandleSealed;
        if (m_director != null)
            m_director.stopped += HandleDirectorStopped;

        Debug.Log($"{kLogPrefix} timeline controller enabled", this);
    }

    private void OnDisable()
    {
        if (m_gate != null)
            m_gate.Sealed -= HandleSealed;
        if (m_director != null)
            m_director.stopped -= HandleDirectorStopped;

        Debug.Log($"{kLogPrefix} timeline controller disabled", this);
    }

    private void HandleSealed()
    {
        if (m_started)
        {
            Debug.Log($"{kLogPrefix} sealed received again, ignored", this);
            return;
        }

        m_started = true;
        Debug.Log($"{kLogPrefix} sealed received, play boss bgm", this);
        Sound.PlayBgm(SoundData.BGM.BossBattle);

        if (m_bossAI != null)
        {
            m_bossAI.setisBattleing(false);
            Debug.Log($"{kLogPrefix} boss battleing set false before timeline", this);
        }
        else
        {
            Debug.LogWarning($"{kLogPrefix} bossAI missing before timeline", this);
        }

        if (m_director == null || m_director.playableAsset == null)
        {
            Debug.LogWarning($"{kLogPrefix} director or playable missing, start battle immediately", this);
            StartBossBattle();
            return;
        }

        m_director.time = 0.0;
        m_director.Evaluate();
        m_director.Play();
        Debug.Log($"{kLogPrefix} timeline play name={m_director.playableAsset.name} duration={m_director.duration:0.###}", this);
    }

    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (!m_started || director != m_director)
            return;

        Debug.Log($"{kLogPrefix} timeline stopped", this);
        StartBossBattle();
    }

    private void StartBossBattle()
    {
        if (m_bossAI == null)
        {
            Debug.LogWarning($"{kLogPrefix} cannot start battle, bossAI missing", this);
            return;
        }

        m_bossAI.SetBattlingOn();
        Debug.Log($"{kLogPrefix} boss battleing set true", this);
    }
}
