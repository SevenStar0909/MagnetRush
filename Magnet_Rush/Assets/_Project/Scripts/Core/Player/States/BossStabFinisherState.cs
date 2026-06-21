using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// ボススタブ・フィニッシャー演出ステート（Timeline 駆動）。
/// プランナーが録画した演出 Timeline（StabFinisherSettings.finisherCutscene）を実行時に再生し、
/// プレイヤーの移動軌跡（PlayerRoot トラック）と体アニメ（Player トラック）をそのまま流す。
/// 録画は world 座標なので、録画時のボス位置との差分だけ演出全体をずらして実ボスへ合わせる。
/// Camera/Activation トラックも同じ Timeline から実行時カメラへバインドし、Sceneプレビューと同じ構図を再生する。
/// Camera トラックが無い場合だけ StabFinisherCamera の別 Timeline へフォールバックする。
/// ヒットは AnimationTrack 経由だとクリップの AnimEvent が発火しないため、時間指定で発火する。
/// 演出中は Player.Update が UpdateEntity をスキップするため、本ステート（director）が transform を駆動する。
/// 基底: EntityState&lt;Player&gt;
/// </summary>
public class BossStabFinisherState : EntityState<Player>
{
    private StabFinisherSettings m_settings;
    private IStabReceiver m_receiver;
    private EnemyBossAI m_bossAi;

    private PlayableDirector m_director;
    private Vector3 m_offset;
    private double m_duration;
    private bool m_hitDone;
    private StabFinisherCamera m_cutsceneCamera;
    private TransformInterpolator[] m_transformInterpolators;

    /// <summary>PlayerAnimator 互換用。Timeline が体を直接駆動するので固定値でよい（突き扱い・接地扱い）。</summary>
    public int AnimatorPhaseIndex => (int)PlayerStateIndex.StabAttack;

    /// <summary>PlayerAnimator 互換用。Timeline 駆動中は接地扱いにする。</summary>
    public bool IsAirbornePhase => false;

    /// <summary>StabAbility から演出データを渡す。Change 前に呼ぶこと。profile は Timeline 駆動では使わない。</summary>
    public void Setup(StabFinisherSettings.Profile profile, IStabReceiver receiver)
    {
        m_receiver = receiver;
        m_settings = receiver != null ? receiver.StabFinisherSettings : null;
        m_bossAi = receiver as EnemyBossAI;
    }

    protected override void OnEnter(Player player)
    {
        m_hitDone = false;
        player.lateralVelocity = Vector3.zero;
        player.externalVelocity = Vector3.zero;

        // 通常移動用の FixedUpdate 補間が、Timeline で動かすモデルを LateUpdate で上書きしないようにする。
        m_transformInterpolators = player.GetComponentsInChildren<TransformInterpolator>(true);
        foreach (var interpolator in m_transformInterpolators)
            interpolator.SetSuspended(true);

        var cutscene = m_settings != null ? m_settings.finisherCutscene as TimelineAsset : null;
        if (cutscene == null || m_receiver == null)
        {
            ChannelLogger.LogGuardReturn("Stab", "演出 Timeline 未設定 — 即ヒットにフォールバック");
            DoHit(player);
            ReturnToNormal(player);
            return;
        }

        // 録画時ボス位置との差分だけ演出全体をずらして実ボスへ合わせる
        Transform bossT = ((MonoBehaviour)m_receiver).transform;
        m_offset = bossT.position - m_settings.cutsceneAuthoringBossPosition;

        // バインド対象の Animator: body=モデル側 / root=_Player 自身（軌跡用・無ければ追加）
        Animator bodyAnim = null;
        foreach (var a in player.GetComponentsInChildren<Animator>(true))
        {
            if (a.gameObject != player.gameObject) { bodyAnim = a; break; }
        }
        var rootAnim = player.GetComponent<Animator>();
        if (rootAnim == null) rootAnim = player.gameObject.AddComponent<Animator>();

        // 実行時 director を生成。Manual 評価で本ステートが毎フレーム時間を進める
        m_director = new GameObject("StabFinisherCutsceneDirector").AddComponent<PlayableDirector>();
        m_director.playableAsset = cutscene;
        m_director.timeUpdateMode = DirectorUpdateMode.Manual;
        m_director.extrapolationMode = DirectorWrapMode.Hold;

        var cameraActivationTracks = new List<ActivationTrack>();
        var cameraAnimationTracks = new List<AnimationTrack>();
        foreach (var track in cutscene.GetOutputTracks())
        {
            if (track.name == "Player") m_director.SetGenericBinding(track, bodyAnim);
            else if (track.name == "PlayerRoot") m_director.SetGenericBinding(track, rootAnim);
            else if (track is ActivationTrack activationTrack) cameraActivationTracks.Add(activationTrack);
            else if (track is AnimationTrack animationTrack) cameraAnimationTracks.Add(animationTrack);
        }

        // Scene上の StabFinisherDirector と同じ Camera/Activation トラックを同じ順番で実行時rigへ割り当てる。
        int cameraCount = Mathf.Min(cameraActivationTracks.Count, cameraAnimationTracks.Count);
        m_cutsceneCamera = StabFinisherCamera.Current;
        if (cameraCount > 0 && m_cutsceneCamera != null && m_cutsceneCamera.PrepareCutsceneCameraTracks(cameraCount))
        {
            for (int i = 0; i < cameraCount; i++)
            {
                m_director.SetGenericBinding(cameraActivationTracks[i], m_cutsceneCamera.GetCutsceneCameraRig(i));
                m_director.SetGenericBinding(cameraAnimationTracks[i], m_cutsceneCamera.GetCutsceneCameraAnimator(i));
            }
        }
        else
        {
            // Cameraトラックが無い／専用Cameraが見つからない場合は既存のカメラTimelineを使う。
            m_cutsceneCamera = null;
        }

        m_duration = cutscene.duration;
        EvaluateAt(player, 0.0);

        m_bossAi?.BeginStabFinisher();
        player.FireStabFinisherStart(m_receiver.StabAnchor, m_receiver.StabChoreographyIndex);
    }

    protected override void OnStep(Player player, float dt)
    {
        if (m_director == null) { ReturnToNormal(player); return; }

        // EntityState は OnStep 後に経過時間を加算するため、今回の dt を先に含めて評価する。
        // 先頭フレームを二度表示せず、PlayerRoot と Camera を同じ再生位置へ進める。
        double t = System.Math.Min(timeSinceEntered + dt, m_duration);
        EvaluateAt(player, t);

        float hitTime = m_settings != null ? m_settings.cutsceneHitTime : 1f;
        if (!m_hitDone && t >= hitTime)
        {
            m_hitDone = true;
            if (m_receiver != null && m_receiver.CanReceiveStab) DoHit(player);
        }

        if (t >= m_duration) ReturnToNormal(player);
    }

    // Timeline を指定時間で評価し、録画 world 座標にボス相対オフセットを足して実ボスへ合わせる。
    // director.Evaluate は _Player を録画の絶対座標に置くため、毎フレーム評価後に差分を足す（累積はしない）。
    private void EvaluateAt(Player player, double time)
    {
        m_director.time = time;
        m_director.Evaluate();
        player.transform.position += m_offset;
        m_cutsceneCamera?.ApplyCutsceneCameraOffset(m_offset);
    }

    protected override void OnExit(Player player)
    {
        if (m_director != null)
        {
            m_director.Stop();
            Object.Destroy(m_director.gameObject);
            m_director = null;
        }
        m_cutsceneCamera?.EndCutsceneCameraTracks();
        m_cutsceneCamera = null;
        if (m_transformInterpolators != null)
        {
            foreach (var interpolator in m_transformInterpolators)
                if (interpolator != null) interpolator.SetSuspended(false);
            m_transformInterpolators = null;
        }
        player.velocity = Vector3.zero;
        player.lateralVelocity = Vector3.zero;
        player.externalVelocity = Vector3.zero;

        // 突き時の傾きを持ち越さない。水平（ヨーのみ）の起き上がった向きへ戻す。
        Vector3 flatFwd = player.transform.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude > 0.0001f)
            player.transform.rotation = Quaternion.LookRotation(flatFwd.normalized, Vector3.up);

        m_bossAi?.EndStabFinisher();
        player.FireStabFinisherEnd();
    }

    // 既存のダメージ＋VFX＋receiver.OnStabHit を再利用する（StabAbility.OnStabHitEvent が着弾通知も担う）。
    private void DoHit(Player player)
    {
        player.stab.OnStabHitEvent();
    }

    private void ReturnToNormal(Player player)
    {
        if (player.input.MoveInput.sqrMagnitude > 0.01f)
            player.states.Change<MovePlayerState>();
        else
            player.states.Change<IdlePlayerState>();
    }
}
