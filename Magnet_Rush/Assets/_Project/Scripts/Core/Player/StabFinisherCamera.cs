using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// ボススタブ・フィニッシャー専用カメラ。Cinemachine を介さず専用の実 Camera を、
/// 「プレイヤーに追従する rig ＋ Timeline（PlayableDirector）」で駆動する。
/// rig を毎フレームのプレイヤー位置に置き（向きは開始時に固定したプレイヤー向き＝オービット基準）、
/// その rig ローカル空間で Timeline を director で評価する。カメラのポーズ（位置・向き・FOV）は
/// 子 FinisherCamera へのローカル Animation トラックにキーフレームで持つ（プランナーが Timeline で編集可能）。
/// director は Manual モードで、走り込み〜着弾の進行に合わせて time を進めて Evaluate する。
/// この Timeline はカメラ専用。演出全体の任意時刻に置く Stab Slow は、トップレベルの
/// StabFinisherCutscene 側へ配置する（Camera Rig 内の子 Director は親 Director から再生されないため）。
/// 突きの振り向きでオービット基準が回らないよう、rig の向きは開始時に一度だけ固定する。
/// 着弾でカメラを止めて（仕様の「ピタッと止まる」）最後の構図を余韻として保持する。
/// 専用 Camera は depth を Main より高くしてあるので、有効化した瞬間に上に描画＝瞬間カット。
/// 依存: Player.OnStabFinisherStart/End, Player.Current。FinisherCameraRig（Animator＋PlayableDirector を持つ）に付ける。
/// </summary>
[DefaultExecutionOrder(-200)]
public class StabFinisherCamera : MonoBehaviour
{
    public static StabFinisherCamera Current { get; private set; }

    [Header("演出カメラ参照")]
    [Tooltip("演出中だけ表示する専用 Camera（Main より depth を高くして上に描画＝瞬間カット）。idle 時は GameObject 非アクティブ")]
    [SerializeField] private Camera m_finisherCamera;

    [Tooltip("カメラ軌跡などを鳴らす PlayableDirector（既定の Timeline をバインドしておく）")]
    [SerializeField] private PlayableDirector m_director;

    [Tooltip("追従の中心をプレイヤー足元からどれだけ上げるか（m）。球面オフセットはこの中心まわりに回る。プレイヤーの胴中心あたりが目安")]
    [SerializeField] private float m_playerAnchorHeight = 1f;

    [Tooltip("カメラTimeline上で「突き着弾」に当たる時刻（秒）。ここまでを走り込み〜着弾へ割り当て、それ以降を着弾後の引きに使う。Stab Slowの時刻はトップレベルのfinisherCutscene側で指定する")]
    [SerializeField] private float m_impactTime = 1f;

    [Header("着弾シェイク（縦主軸・重い一撃／引きに加算）")]
    [Tooltip("揺れの最大振幅（m）。「ドスン」を出すなら大きめ（0.6〜0.9）。着弾直後が最大で急減衰する")]
    [SerializeField] private float m_shakeAmplitude = 0.7f;

    [Tooltip("揺れの長さ（秒）。短く（0.25〜0.35）。最初に最大→急速にゼロへ")]
    [SerializeField] private float m_shakeDuration = 0.3f;

    [Tooltip("揺れの速さ（Hz）。低いほど重いストローク（8〜12が目安）。高いと細かいプルプルになる")]
    [SerializeField] private float m_shakeFrequency = 10f;

    private Animator m_animator;             // rig の Animator（Timeline トラックのバインド先）
    private PlayableAsset m_defaultTimeline; // boss が cameraTimeline を持たない時のフォールバック（初期アサイン）
    private double m_timelineDuration = 1.0; // カメラ Timeline 全体の尺（秒）。カメラ区間＋着弾後テール（引き）を含む。
    private float m_frozenRealTimer;         // 着弾後の経過（実時間）。テールの引きを進めるのに使う。
    private Transform m_player;              // 追従対象（プレイヤー本体）
    private Quaternion m_orbitFrame;         // 開始時に固定したプレイヤーの水平向き＝オービットの基準（rig の回転）
    private float m_timer;
    private float m_pathDuration;            // 走り込み〜着弾の尺。ここで Timeline の [0..m_impactTime] を流し切る。
    private bool m_active;
    private bool m_frozen;                   // 着弾後 true。rig を着弾点で固定し、カメラはテールの軌跡（引き）で動かす。
    private float m_shakeTimer;              // 着弾シェイクの経過時間
    private Vector3 m_frozenRigPos;          // 着弾で固定した rig のワールド位置（カメラはここを基準に引いていく）
    private Quaternion m_frozenRigRot;       // 着弾で固定した rig のワールド回転
    private readonly List<GameObject> m_timelineCameraRigs = new();
    private readonly List<Animator> m_timelineCameraAnimators = new();
    private bool m_usesCutsceneCameraTracks;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;

    void OnEnable()
    {
        // _Camera.prefab に旧構成の FinisherCameraRig が重複していても、イベント購読と描画を二重化しない。
        if (Current != null && Current != this)
        {
            enabled = false;
            return;
        }
        Current = this;

        m_animator = GetComponent<Animator>();
        if (m_director != null)
        {
            m_defaultTimeline = m_director.playableAsset;
            m_director.timeUpdateMode = DirectorUpdateMode.Manual; // 進行はこちらで time を進めて Evaluate する。
        }
        Player.OnStabFinisherStart += OnStabFinisherStart;
        Player.OnStabFinisherEnd += OnStabFinisherEnd;
        Player.OnStabFinisherImpact += OnStabFinisherImpact;
    }

    void OnDisable()
    {
        if (Current != this) return;

        Player.OnStabFinisherStart -= OnStabFinisherStart;
        Player.OnStabFinisherEnd -= OnStabFinisherEnd;
        Player.OnStabFinisherImpact -= OnStabFinisherImpact;
        EndCutsceneCameraTracks();
        Current = null;
    }

    // 演出開始: 追従対象とオービット基準を固定し、Timeline を割り当てて専用 Camera を表示、先頭で初期ポーズを確定する。
    private void OnStabFinisherStart(Transform target, int variant)
    {
        // StabFinisherCutscene に Camera/Activation トラックがある場合は、そちらを正とする。
        if (m_usesCutsceneCameraTracks) return;

        if (m_finisherCamera == null)
        { ChannelLogger.LogGuardReturn("Stab", "finisher Camera が未設定 — 演出カメラなし"); return; }
        if (m_director == null)
        { ChannelLogger.LogGuardReturn("Stab", "PlayableDirector が未設定 — 演出カメラなし"); return; }

        // 刺したボスの設定を優先（無ければプレイヤー共通にフォールバック）。
        var receiver = target != null ? target.GetComponentInParent<IStabReceiver>() : null;
        var bossSettings = receiver != null ? receiver.StabFinisherSettings : null;
        var settings = bossSettings != null ? bossSettings
            : (Player.Current != null && Player.Current.Settings != null ? Player.Current.Settings.stabFinisherSettings : null);
        if (settings == null)
        { ChannelLogger.LogGuardReturn("Stab", "StabFinisherSettings未取得 — 演出カメラなし"); return; }

        m_player = Player.Current != null ? Player.Current.transform : null;
        if (m_player == null)
        { ChannelLogger.LogGuardReturn("Stab", "Player.Current未取得 — 演出カメラなし"); return; }

        // このボス専用のカメラ Timeline（無ければ共通デフォルト）を director に割り当てる。
        var timeline = settings.cameraTimeline != null ? settings.cameraTimeline : m_defaultTimeline;
        if (timeline == null)
        { ChannelLogger.LogGuardReturn("Stab", "カメラ Timeline 未設定 — 演出カメラなし"); return; }
        SetTimeline(timeline);

        // 軌跡は「走り込み＋跳び乗り＋突き＝着弾」までを 0→1 で流す。着弾で止めて余韻にする。
        var p = settings.GetProfile(variant);
        m_pathDuration = Mathf.Max(0.01f, p.approachDuration + p.leapDuration + p.plungeDuration);

        // オービット基準＝開始時のプレイヤーの水平向き。突きの振り向きで基準が回らないよう、ここで一度だけ固定する。
        Vector3 fwd = m_player.forward; fwd.y = 0f;
        m_orbitFrame = fwd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;

        m_timer = 0f;
        m_active = true;
        m_frozen = false;

        // 専用 Camera を表示し、Timeline 先頭フレームで初期ポーズを確定する。
        m_finisherCamera.gameObject.SetActive(true);
        ApplyPose(0f);
    }

    // 演出終了: Timeline を止めて専用 Camera を非表示にし、Main Camera だけに戻す。
    private void OnStabFinisherEnd()
    {
        if (m_usesCutsceneCameraTracks)
        {
            EndCutsceneCameraTracks();
            return;
        }

        m_active = false;
        m_player = null;
        if (m_director != null) m_director.Stop();
        if (m_finisherCamera != null) m_finisherCamera.gameObject.SetActive(false);
    }

    // 突きがボスに刺さった瞬間（ヒットVFXと同フレーム）に呼ばれる。ここで着弾構図へ切り替える。
    // 走り込み〜着弾の尺（m_pathDuration）に依存せず、実際のヒットにフレーム同期させるための入口。
    private void OnStabFinisherImpact()
    {
        if (m_usesCutsceneCameraTracks) return;

        if (!m_active)
        { ChannelLogger.LogGuardReturn("Stab", "演出中でないスタブ着弾通知 — 演出カメラは無視"); return; }
        if (m_frozen) return; // 既にフリーズ済み（保険が先に走った等）。多重は無視。
        FreezeAtImpact();
    }

    // 着弾でフリーズ＝rig を着弾点で固定し、以降カメラはテールの軌跡（引き）で動かす。
    // まず着弾の構図（impact キー＝t=1）へ確定してから固定する（途中で来ても突き放しの構図に到達させる）。
    private void FreezeAtImpact()
    {
        ApplyPose(1f);
        m_frozen = true;
        m_frozenRealTimer = 0f;
        m_shakeTimer = 0f;
        m_frozenRigPos = transform.position;
        m_frozenRigRot = transform.rotation;
    }

    void LateUpdate()
    {
        if (!m_active || m_director == null) return;

        if (!m_frozen)
        {
            // スロー中もプレイヘッドは実時間で進める（でないとスローが自分の進行を遅くして戻れない）。
            // 着弾前は timeScale=1 なので unscaled でも従来と同じ尺。
            m_timer += Time.unscaledDeltaTime;
            ApplyPose(Mathf.Clamp01(m_timer / m_pathDuration));
            // 通常は突き刺さりの AnimEvent（OnStabFinisherImpact）で着弾フリーズする。
            // ここは保険＝イベントが来ないまま尺を使い切った場合のみフリーズする。
            if (m_timer >= m_pathDuration) FreezeAtImpact();
            return;
        }

        // 着弾後: rig は着弾点に固定したまま（プレイヤーの離脱は追わない）、Timeline を実時間で進め続ける。
        // テールのカメラ軌跡・向き（Timeline のキー）を鳴らし、最後に縦シェイクを引きの上へ加算する。
        transform.SetPositionAndRotation(m_frozenRigPos, m_frozenRigRot);
        m_frozenRealTimer += Time.unscaledDeltaTime;
        m_director.time = System.Math.Min(m_impactTime + m_frozenRealTimer, m_timelineDuration);
        m_director.Evaluate();
        AddImpactShake();
    }

    // 着弾の瞬間だけ、カメラ（引き中）の位置へ縦方向の減衰ストロークを加算する。
    // 「最初にドンと最大→急速にゼロへ」を出すため減衰は2乗（前半で一気に落ちる）。スローでもキレるよう unscaled で計る。
    private void AddImpactShake()
    {
        if (m_shakeTimer >= m_shakeDuration) return;
        m_shakeTimer += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(m_shakeTimer / m_shakeDuration);
        float decay = (1f - k) * (1f - k);
        float osc = Mathf.Sin(m_shakeTimer * m_shakeFrequency * 2f * Mathf.PI);
        var cam = m_finisherCamera.transform;
        cam.position += (cam.rotation * Vector3.up) * (m_shakeAmplitude * decay * osc);
    }

    // rig をプレイヤー位置（中心は足元から少し上・向きは開始時固定）に置き、その rig ローカルで Timeline を評価する。
    // Timeline は球面オフセット（位置）・向き・FOV をキーで持つので、評価するだけでカメラのポーズが決まる。
    private void ApplyPose(float t)
    {
        if (m_player == null) return;
        Vector3 anchor = m_player.position + Vector3.up * m_playerAnchorHeight;
        transform.SetPositionAndRotation(anchor, m_orbitFrame);
        m_director.time = t * m_impactTime;
        m_director.Evaluate();
    }

    // director に Timeline を割り当て、AnimationTrack を rig の Animator にバインドする。
    // バインドは playableAsset 単位なので、ボス専用 Timeline へ差し替えた時もここで張り直す。
    private void SetTimeline(PlayableAsset timeline)
    {
        if (m_director.playableAsset != timeline) m_director.playableAsset = timeline;
        m_director.timeUpdateMode = DirectorUpdateMode.Manual;

        var ta = timeline as TimelineAsset;
        if (ta != null && m_animator != null)
        {
            foreach (var track in ta.GetOutputTracks())
                if (track is AnimationTrack) m_director.SetGenericBinding(track, m_animator);
        }
        m_timelineDuration = m_director.duration > 0.0 ? m_director.duration : m_impactTime;
    }

    /// <summary>
    /// トップレベルの StabFinisherCutscene に記録された複数カメラを再生するため、
    /// 専用 Camera の描画設定を複製した一時 rig を用意する。Camera の姿勢/FOV/Active は
    /// 呼び出し側が同 Timeline の AnimationTrack / ActivationTrack をバインドして駆動する。
    /// </summary>
    public bool PrepareCutsceneCameraTracks(int count)
    {
        EndCutsceneCameraTracks();
        if (count <= 0 || m_finisherCamera == null) return false;

        m_usesCutsceneCameraTracks = true;
        m_active = false;
        m_finisherCamera.gameObject.SetActive(false);

        for (int i = 0; i < count; i++)
        {
            var rig = new GameObject($"StabFinisherTimelineCamera ({i + 1})");
            var animator = rig.AddComponent<Animator>();

            // 元の専用Cameraを複製することで、URP・Depth・CullingMask等をSceneプレビューと同条件にする。
            var cameraObject = Instantiate(m_finisherCamera.gameObject, rig.transform, false);
            cameraObject.name = "FinisherCamera"; // AnimationClip の path と一致させる。
            cameraObject.SetActive(true);

            // 全rigを初期非表示にし、有効なActivationTrackだけが必要な時間帯にONへする。
            // ミュートされたActivationTrackのカメラが描画へ混ざるのを防ぐ。
            rig.SetActive(false);

            m_timelineCameraRigs.Add(rig);
            m_timelineCameraAnimators.Add(animator);
        }

        return true;
    }

    public GameObject GetCutsceneCameraRig(int index)
        => index >= 0 && index < m_timelineCameraRigs.Count ? m_timelineCameraRigs[index] : null;

    public Animator GetCutsceneCameraAnimator(int index)
        => index >= 0 && index < m_timelineCameraAnimators.Count ? m_timelineCameraAnimators[index] : null;

    /// <summary>録画時ボス位置との差分を全カメラへ加える。Timeline評価後に毎フレーム呼ぶため累積しない。</summary>
    public void ApplyCutsceneCameraOffset(Vector3 offset)
    {
        for (int i = 0; i < m_timelineCameraRigs.Count; i++)
        {
            var rig = m_timelineCameraRigs[i];
            if (rig != null) rig.transform.position += offset;
        }
    }

    public void EndCutsceneCameraTracks()
    {
        for (int i = 0; i < m_timelineCameraRigs.Count; i++)
        {
            if (m_timelineCameraRigs[i] != null) Destroy(m_timelineCameraRigs[i]);
        }
        m_timelineCameraRigs.Clear();
        m_timelineCameraAnimators.Clear();
        m_usesCutsceneCameraTracks = false;
        if (m_finisherCamera != null) m_finisherCamera.gameObject.SetActive(false);
    }
}
