using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

/// <summary>
/// ボススタブ・フィニッシャー専用カメラの駆動。演出開始で rig をボスへ配置し、
/// finisher vcam を有効化（Brain がブレンドイン）して Timeline を再生する。
/// 軌跡・カメラの向き・尺はすべて Timeline(StabFinisherTimeline) の Animation Track で編集する。
/// 依存: Player.OnStabFinisherStart/End, CinemachineCamera, PlayableDirector。
/// FinisherCameraRig（rig ルート）に付ける。
/// </summary>
[DefaultExecutionOrder(-200)]
public class StabFinisherCamera : MonoBehaviour
{
    [Header("演出カメラ参照")]
    [Tooltip("演出中だけ有効化する finisher vcam")]
    [SerializeField] private CinemachineCamera m_finisherVcam;

    [Tooltip("軌跡・向き・尺を持つ Timeline を再生する PlayableDirector")]
    [SerializeField] private PlayableDirector m_director;

    void OnEnable()
    {
        Player.OnStabFinisherStart += OnStabFinisherStart;
        Player.OnStabFinisherEnd += OnStabFinisherEnd;
    }

    void OnDisable()
    {
        Player.OnStabFinisherStart -= OnStabFinisherStart;
        Player.OnStabFinisherEnd -= OnStabFinisherEnd;
    }

    // 演出開始: rig をボスへ寄せ、finisher vcam を有効化（Brain がブレンドイン）して Timeline を頭から再生する。
    // variant は将来の崩れポーズ別 Timeline 切替用に残す（現状は単一 Timeline）。
    private void OnStabFinisherStart(Transform target, int variant)
    {
        if (m_finisherVcam == null || m_director == null)
        { ChannelLogger.LogGuardReturn("Stab", "finisherカメラ参照が未設定 — 演出カメラなし"); return; }

        PlaceRigAtBoss();

        m_finisherVcam.gameObject.SetActive(true);
        m_director.time = 0d;
        m_director.Play();
    }

    // 演出終了: Timeline を止め、finisher vcam を無効化して Brain を通常カメラへ戻す。
    private void OnStabFinisherEnd()
    {
        if (m_director != null) m_director.Stop();
        if (m_finisherVcam != null) m_finisherVcam.gameObject.SetActive(false);
    }

    // ボスをタグで解決して rig をその位置・正面に合わせる。未登録/不在なら現状維持（authoring 位置のまま）。
    private void PlaceRigAtBoss()
    {
        GameObject boss = null;
        try { boss = GameObject.FindWithTag("Boss"); }
        catch (UnityException) { ChannelLogger.LogGuardReturn("Stab", "Bossタグ未登録 — rig位置は現状維持"); return; }
        if (boss == null) { ChannelLogger.LogGuardReturn("Stab", "Boss不在 — rig位置は現状維持"); return; }

        Vector3 f = boss.transform.forward;
        f.y = 0f;
        Vector3 fwd = f.sqrMagnitude < 0.0001f ? Vector3.forward : f.normalized;
        transform.SetPositionAndRotation(boss.transform.position, Quaternion.LookRotation(fwd, Vector3.up));
    }
}
