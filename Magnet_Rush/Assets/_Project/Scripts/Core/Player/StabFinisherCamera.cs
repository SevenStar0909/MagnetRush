using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// ボススタブ・フィニッシャー専用カメラ。Cinemachine を介さず専用の実 Camera を、
/// 「プレイヤーに追従する rig ＋ Timeline クリップのローカル軌跡」で駆動する。
/// rig を毎フレームのプレイヤー位置に置き（向きは開始時に固定したプレイヤー向き＝オービット基準）、
/// その rig ローカル空間で Timeline クリップ（球面オフセットの軌跡：真後ろ・ローアングル→右へ回り込み・俯瞰→着弾で引き）を
/// SampleAnimation で直接サンプルする。これでカメラは常にプレイヤーを画面に捉えつつ、軌跡は Timeline で編集できる。
/// 突きの振り向きでオービット基準が回らないよう、rig の向きは開始時に一度だけ固定する。
/// 軌跡は走り込み〜着弾までを 0→1 で流し、着弾でカメラを止めて（仕様の「ピタッと止まる」）最後の構図を余韻として保持する。
/// 専用 Camera は depth を Main より高くしてあるので、有効化した瞬間に上に描画＝瞬間カット。
/// 依存: Player.OnStabFinisherStart/End, Player.Current。FinisherCameraRig に付ける。
/// </summary>
[DefaultExecutionOrder(-200)]
public class StabFinisherCamera : MonoBehaviour
{
    [Header("演出カメラ参照")]
    [Tooltip("演出中だけ表示する専用 Camera（Main より depth を高くして上に描画＝瞬間カット）。idle 時は GameObject 非アクティブ")]
    [SerializeField] private Camera m_finisherCamera;

    [Tooltip("カメラ軌跡を持つ既定の Timeline（ボスが専用 cameraTimeline を持たない時のフォールバック元）")]
    [SerializeField] private PlayableDirector m_director;

    [Tooltip("追従の中心をプレイヤー足元からどれだけ上げるか（m）。球面オフセットはこの中心まわりに回る。プレイヤーの胴中心あたりが目安")]
    [SerializeField] private float m_playerAnchorHeight = 1f;

    [Header("着弾シェイク（仕様④：縦主軸の短く強い揺れ）")]
    [Tooltip("揺れの最大振幅（m）。着弾の瞬間が最大で、短時間で減衰する")]
    [SerializeField] private float m_shakeAmplitude = 0.35f;

    [Tooltip("揺れの長さ（秒）。短く（0.3前後）が打撃感の目安")]
    [SerializeField] private float m_shakeDuration = 0.3f;

    [Tooltip("揺れの速さ（Hz）。大きいほど細かく振動する")]
    [SerializeField] private float m_shakeFrequency = 22f;

    private PlayableAsset m_defaultTimeline; // boss が cameraTimeline を持たない時のフォールバック（初期アサイン）
    private AnimationClip m_clip;            // 現在サンプリング中のカメラ用クリップ（球面軌跡・プレイヤーローカル）
    private float m_clipLength;
    private Transform m_player;              // 追従対象（プレイヤー本体）
    private Quaternion m_orbitFrame;         // 開始時に固定したプレイヤーの水平向き＝オービットの基準（rig の回転）
    private float m_timer;
    private float m_pathDuration;            // 走り込み〜着弾の尺。ここで軌跡を 0→1 流し切る。
    private bool m_active;
    private bool m_frozen;                   // 着弾後 true。カメラを止めて最後の構図を余韻として保持する。
    private float m_shakeTimer;              // 着弾シェイクの経過時間
    private Vector3 m_frozenCamPos;          // 着弾で固定したカメラのワールド位置（シェイクはこの周りで揺れる）
    private Quaternion m_frozenCamRot;       // 着弾で固定したカメラのワールド回転

    void OnEnable()
    {
        if (m_director != null) m_defaultTimeline = m_director.playableAsset;
        Player.OnStabFinisherStart += OnStabFinisherStart;
        Player.OnStabFinisherEnd += OnStabFinisherEnd;
    }

    void OnDisable()
    {
        Player.OnStabFinisherStart -= OnStabFinisherStart;
        Player.OnStabFinisherEnd -= OnStabFinisherEnd;
    }

    // 演出開始: 追従対象とオービット基準を固定し、専用 Camera を表示してクリップのサンプリングを開始する。
    private void OnStabFinisherStart(Transform target, int variant)
    {
        if (m_finisherCamera == null)
        { ChannelLogger.LogGuardReturn("Stab", "finisher Camera が未設定 — 演出カメラなし"); return; }

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

        // このボス専用のカメラ Timeline（無ければ共通デフォルト）から AnimationClip を取り出す。
        var timeline = settings.cameraTimeline != null ? settings.cameraTimeline : m_defaultTimeline;
        m_clip = ExtractClip(timeline);
        if (m_clip == null)
        { ChannelLogger.LogGuardReturn("Stab", "Timeline から AnimationClip を取得できない — 演出カメラなし"); return; }
        m_clipLength = m_clip.length;

        // 軌跡は「走り込み＋跳び乗り＋突き＝着弾」までを 0→1 で流す。着弾で止めて余韻にする。
        var p = settings.GetProfile(variant);
        m_pathDuration = Mathf.Max(0.01f, p.approachDuration + p.leapDuration + p.plungeDuration);

        // オービット基準＝開始時のプレイヤーの水平向き。突きの振り向きで基準が回らないよう、ここで一度だけ固定する。
        Vector3 fwd = m_player.forward; fwd.y = 0f;
        m_orbitFrame = fwd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;

        m_timer = 0f;
        m_active = true;
        m_frozen = false;

        // 専用 Camera を表示。先に有効化して Animator を起こし、クリップ先頭フレームで初期ポーズを確定する。
        m_finisherCamera.gameObject.SetActive(true);
        ApplyPose(0f);
    }

    // 演出終了: 専用 Camera を非表示にして Main Camera だけに戻す。
    private void OnStabFinisherEnd()
    {
        m_active = false;
        m_clip = null;
        m_player = null;
        if (m_finisherCamera != null) m_finisherCamera.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!m_active || m_clip == null) return;

        if (!m_frozen)
        {
            m_timer += Time.deltaTime;
            ApplyPose(Mathf.Clamp01(m_timer / m_pathDuration));
            // 着弾に到達したらカメラを止める。最後の構図（引き）を余韻として保持し、プレイヤーの離脱は追わない。
            if (m_timer >= m_pathDuration)
            {
                m_frozen = true;
                m_shakeTimer = 0f;
                m_frozenCamPos = m_finisherCamera.transform.position;
                m_frozenCamRot = m_finisherCamera.transform.rotation;
            }
            return;
        }

        // 着弾後: 構図を止めたまま、縦主軸の短く強いシェイクを上乗せして減衰させる（仕様④）。
        ApplyShake();
    }

    // 固定した着弾構図の周りを、カメラの縦方向（画面の上下）に減衰サイン波で揺らす。
    private void ApplyShake()
    {
        Vector3 pos = m_frozenCamPos;
        if (m_shakeTimer < m_shakeDuration)
        {
            m_shakeTimer += Time.deltaTime;
            float decay = 1f - Mathf.Clamp01(m_shakeTimer / m_shakeDuration);
            float osc = Mathf.Sin(m_shakeTimer * m_shakeFrequency * 2f * Mathf.PI);
            pos += (m_frozenCamRot * Vector3.up) * (m_shakeAmplitude * decay * osc);
        }
        m_finisherCamera.transform.SetPositionAndRotation(pos, m_frozenCamRot);
    }

    // rig をプレイヤー位置（中心は足元から少し上・向きは開始時固定）に置き、その rig ローカルでクリップをサンプルする。
    // クリップは「プレイヤー中心まわりの球面オフセット＋プレイヤーを見る回転」を持つので、カメラは常にプレイヤーを捉える。
    private void ApplyPose(float t)
    {
        if (m_player == null) return;
        Vector3 anchor = m_player.position + Vector3.up * m_playerAnchorHeight;
        transform.SetPositionAndRotation(anchor, m_orbitFrame);
        m_clip.SampleAnimation(m_finisherCamera.gameObject, t * m_clipLength);
    }

    // Timeline アセットの AnimationTrack から最初の AnimationClip を取り出す。
    // director 経由（Animator パイプライン）では path="" のルート transform カーブがルートモーション扱いで
    // 破棄されるため、ここで取り出して SampleAnimation で直接適用する。
    private static AnimationClip ExtractClip(PlayableAsset timeline)
    {
        var ta = timeline as TimelineAsset;
        if (ta == null) return null;
        foreach (var track in ta.GetOutputTracks())
        {
            var at = track as AnimationTrack;
            if (at == null) continue;
            if (at.infiniteClip != null) return at.infiniteClip;
            foreach (var c in at.GetClips())
            {
                var apa = c.asset as AnimationPlayableAsset;
                if (apa != null && apa.clip != null) return apa.clip;
            }
        }
        return null;
    }
}
