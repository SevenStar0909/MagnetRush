using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// ボススタブ・フィニッシャー専用カメラ。Cinemachine を介さず専用の実 Camera を、
/// 「プレイヤーに追従する rig ＋ Timeline（PlayableDirector）」で駆動する。
/// rig を毎フレームのプレイヤー位置に置き（向きは開始時に固定したプレイヤー向き＝オービット基準）、
/// その rig ローカル空間で Timeline を director で評価する。カメラのポーズ（位置・向き・FOV）は
/// 子 FinisherCamera へのローカル Animation トラックにキーフレームで持つ（プランナーが Timeline で編集可能）。
/// director は Manual モードで、走り込み〜着弾の進行に合わせて time を進めて Evaluate する
/// （＝コード駆動の尺は保ちつつ、カメラ・スロー・Signal など全トラックを1つの Timeline で鳴らせる）。
/// 突きの振り向きでオービット基準が回らないよう、rig の向きは開始時に一度だけ固定する。
/// 着弾でカメラを止めて（仕様の「ピタッと止まる」）最後の構図を余韻として保持する。
/// 専用 Camera は depth を Main より高くしてあるので、有効化した瞬間に上に描画＝瞬間カット。
/// 依存: Player.OnStabFinisherStart/End, Player.Current。FinisherCameraRig（Animator＋PlayableDirector を持つ）に付ける。
/// </summary>
[DefaultExecutionOrder(-200)]
public class StabFinisherCamera : MonoBehaviour
{
    [Header("演出カメラ参照")]
    [Tooltip("演出中だけ表示する専用 Camera（Main より depth を高くして上に描画＝瞬間カット）。idle 時は GameObject 非アクティブ")]
    [SerializeField] private Camera m_finisherCamera;

    [Tooltip("カメラ軌跡などを鳴らす PlayableDirector（既定の Timeline をバインドしておく）")]
    [SerializeField] private PlayableDirector m_director;

    [Tooltip("追従の中心をプレイヤー足元からどれだけ上げるか（m）。球面オフセットはこの中心まわりに回る。プレイヤーの胴中心あたりが目安")]
    [SerializeField] private float m_playerAnchorHeight = 1f;

    [Tooltip("Timeline 上で「突き着弾」に当たる時刻（秒）。ここまで(0〜)を走り込み〜着弾の尺に割り当て、それ以降(〜尺末)は実時間テール＝着弾後の引き／スローに使う。着弾スロークリップの開始と揃える")]
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
    private double m_timelineDuration = 1.0; // Timeline 全体の尺（秒）。カメラ区間＋着弾後テール（引き／スロー）を含む。
    private float m_frozenRealTimer;         // 着弾後の経過（実時間）。テール（引き・スローの戻りなど）を進めるのに使う。
    private Transform m_player;              // 追従対象（プレイヤー本体）
    private Quaternion m_orbitFrame;         // 開始時に固定したプレイヤーの水平向き＝オービットの基準（rig の回転）
    private float m_timer;
    private float m_pathDuration;            // 走り込み〜着弾の尺。ここで Timeline の [0..m_impactTime] を流し切る。
    private bool m_active;
    private bool m_frozen;                   // 着弾後 true。rig を着弾点で固定し、カメラはテールの軌跡（引き）で動かす。
    private float m_shakeTimer;              // 着弾シェイクの経過時間
    private Vector3 m_frozenRigPos;          // 着弾で固定した rig のワールド位置（カメラはここを基準に引いていく）
    private Quaternion m_frozenRigRot;       // 着弾で固定した rig のワールド回転

    void OnEnable()
    {
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
        Player.OnStabFinisherStart -= OnStabFinisherStart;
        Player.OnStabFinisherEnd -= OnStabFinisherEnd;
        Player.OnStabFinisherImpact -= OnStabFinisherImpact;
    }

    // 演出開始: 追従対象とオービット基準を固定し、Timeline を割り当てて専用 Camera を表示、先頭で初期ポーズを確定する。
    private void OnStabFinisherStart(Transform target, int variant)
    {
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
        m_active = false;
        m_player = null;
        if (m_director != null) m_director.Stop();
        if (m_finisherCamera != null) m_finisherCamera.gameObject.SetActive(false);
    }

    // 突きがボスに刺さった瞬間（ヒットVFXと同フレーム）に呼ばれる。ここで着弾アップ＋スローを即開始する。
    // 走り込み〜着弾の尺（m_pathDuration）に依存せず、実際のヒットにフレーム同期させるための入口。
    private void OnStabFinisherImpact()
    {
        if (!m_active)
        { ChannelLogger.LogGuardReturn("Stab", "演出中でないスタブ着弾通知 — 演出カメラは無視"); return; }
        if (m_frozen) return; // 既にフリーズ済み（保険が先に走った等）。多重は無視。
        FreezeAtImpact();
    }

    // 着弾でフリーズ＝rig を着弾点で固定し、以降カメラはテールの軌跡（引き）＋スローで動かす。
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
        // テールのカメラ軌跡・向き（Timeline のキー）とスローを鳴らし、最後に縦シェイクを引きの上へ加算する。
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
}
