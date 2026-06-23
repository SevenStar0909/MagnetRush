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
    private Vector3 m_anchorA0;              // 録画時アンカー位置（cutsceneAuthoringBossPosition）
    private Vector3 m_anchorA1;              // 実行時アンカー位置（実ボスの StabAnchor）
    private Quaternion m_anchorYawDelta;     // 録画→実行のヨー差分（水平回転のみ）
    private Quaternion m_playerFinisherRot;  // 演出中のプレイヤールート向き（ボスを向く・一定）
    private bool m_playerRotInitialized;
    private double m_duration;
    private bool m_hitDone;
    private StabFinisherCamera m_cutsceneCamera;
    private TransformInterpolator[] m_transformInterpolators;
    private Animator m_bodyAnim;
    private RuntimeAnimatorController m_savedBodyController;
    private GameObject m_impactEffect;

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
        player.stab.SetProgramHitVfxSuppressed(false);
        player.lateralVelocity = Vector3.zero;
        player.externalVelocity = Vector3.zero;

        // AimAbility.Update と TimeScaleTrack が同じフレームに時間倍率を書き合うと、
        // 実行順により Timeline の進行量が揺れる。演出中は Timeline を唯一の所有者にする。
        player.aim.SetTimeScaleControlSuspended(true);

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

        // 録画時アンカー(StabAnchor)→実ボスの StabAnchor へ、位置＋ヨーで演出全体を剛体変換で再アンカーする。
        // StabAnchor 未設定ならボス原点へフォールバック（現行と同一）。
        Transform bossT = ((MonoBehaviour)m_receiver).transform;
        Transform anchorT = m_receiver.StabAnchor != null ? m_receiver.StabAnchor : bossT;
        m_anchorA0 = m_settings.cutsceneAuthoringBossPosition;
        m_anchorA1 = anchorT.position;
        m_anchorYawDelta = Quaternion.Euler(0f, anchorT.eulerAngles.y - m_settings.cutsceneAuthoringBossYaw, 0f);
        // 演出中のプレイヤー向き＝録画時のプレイヤー向きをヨー差分だけ回した一定値。
        // 体アニメはこの向き基準で前進するので、向きを録画と一致させないと着地点が斜めにズレる。
        m_playerFinisherRot = m_anchorYawDelta * Quaternion.Euler(0f, m_settings.cutsceneAuthoringPlayerYaw, 0f);
        m_playerRotInitialized = true;

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
        var cameraShakeTracks = new List<CameraShakeTrack>();
        foreach (var track in cutscene.GetOutputTracks())
        {
            if (track.name == "Player") m_director.SetGenericBinding(track, bodyAnim);
            else if (track.name == "PlayerRoot") m_director.SetGenericBinding(track, rootAnim);
            else if (track is ActivationTrack activationTrack) cameraActivationTracks.Add(activationTrack);
            else if (track is AnimationTrack animationTrack) cameraAnimationTracks.Add(animationTrack);
            else if (track is CameraShakeTrack shakeTrack) cameraShakeTracks.Add(shakeTrack);
        }

        // 体 Animator は Timeline の Player トラック（B_Player_Pile）だけで駆動する。Mecanim コントローラを残すと
        // Update の director.Evaluate の後（アニメフェーズ）でコントローラが上書きし、Timeline が勝てず走りのまま固まる。
        // root/カメラ用 Animator はコントローラ無しなので元から Timeline で動く。退出時に元のコントローラへ戻す。
        m_bodyAnim = bodyAnim;
        if (m_bodyAnim != null)
        {
            m_savedBodyController = m_bodyAnim.runtimeAnimatorController;
            m_bodyAnim.runtimeAnimatorController = null;
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

            foreach (var shakeTrack in cameraShakeTracks)
            {
                int cameraIndex = Mathf.Clamp(shakeTrack.runtimeCameraIndex, 0, cameraCount - 1);
                var runtimeAnimator = m_cutsceneCamera.GetCutsceneCameraAnimator(cameraIndex);
                m_director.SetGenericBinding(shakeTrack, runtimeAnimator);

                // Scene上のFinisherCameraRig (4)で調整した値を、実行時に生成した同番号のrigへ引き継ぐ。
                var authoringSettings = FindAuthoringShakeSettings(cutscene, shakeTrack);
                if (runtimeAnimator != null && authoringSettings != null)
                {
                    var runtimeSettings = runtimeAnimator.GetComponent<CameraShakeRuntimeSettings>();
                    if (runtimeSettings == null)
                        runtimeSettings = runtimeAnimator.gameObject.AddComponent<CameraShakeRuntimeSettings>();
                    runtimeSettings.CopyFrom(authoringSettings);
                }
            }
        }
        else
        {
            // Cameraトラックが無い／専用Cameraが見つからない場合は既存のカメラTimelineを使う。
            m_cutsceneCamera = null;
        }

        // Timeline 上で調整した着弾エフェクトを複製して Control トラックへバインドする。
        // exposedName は Timeline 編集時に Unity が再生成するため、固定文字列ではなく実際の Control クリップから取得する。
        // 作者用オブジェクトの位置オフセット・回転・スケールも録画時アンカー基準で実ボスへ写し、Timeline と同じ見た目にする。
        if (m_settings.finisherImpactEffect != null && TryGetImpactEffectExposedName(cutscene, out var impactEffectExposedName))
        {
            GameObject authoringEffect = FindAuthoringImpactEffect(cutscene, m_director, impactEffectExposedName);
            GameObject effectTemplate = authoringEffect != null ? authoringEffect : m_settings.finisherImpactEffect;
            m_impactEffect = Object.Instantiate(effectTemplate);

            if (authoringEffect != null)
            {
                Transform authoringRoot = authoringEffect.transform.root;
                Vector3 authoringPosition = authoringRoot.InverseTransformPoint(authoringEffect.transform.position);
                Quaternion authoringRotation = Quaternion.Inverse(authoringRoot.rotation) * authoringEffect.transform.rotation;
                m_impactEffect.transform.SetPositionAndRotation(
                    m_anchorA1 + m_anchorYawDelta * (authoringPosition - m_anchorA0),
                    m_anchorYawDelta * authoringRotation);
                m_impactEffect.transform.localScale = authoringEffect.transform.localScale;
            }
            else
            {
                Vector3 impactPos = m_receiver.StabAnchor != null ? m_receiver.StabAnchor.position : bossT.position;
                m_impactEffect.transform.SetPositionAndRotation(impactPos, Quaternion.identity);
            }

            m_impactEffect.SetActive(false);
            m_director.SetReferenceValue(impactEffectExposedName, m_impactEffect);
            player.stab.SetProgramHitVfxSuppressed(true);
        }

        m_duration = cutscene.duration;
        EvaluateAt(player, 0.0);

        m_bossAi?.BeginStabFinisher();
        player.FireStabFinisherStart(m_receiver.StabAnchor, m_receiver.StabChoreographyIndex);
    }

    private static StabFinisherCamera FindAuthoringShakeSettings(TimelineAsset cutscene, CameraShakeTrack shakeTrack)
    {
        var directors = Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var director in directors)
        {
            if (director.playableAsset != cutscene) continue;
            if (director.GetGenericBinding(shakeTrack) is not Animator animator) continue;

            var settings = animator.GetComponent<StabFinisherCamera>();
            if (settings != null) return settings;
        }
        return null;
    }

    private static GameObject FindAuthoringImpactEffect(
        TimelineAsset cutscene,
        PlayableDirector runtimeDirector,
        PropertyName exposedName)
    {
        var directors = Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var director in directors)
        {
            if (director == runtimeDirector || director.playableAsset != cutscene) continue;

            var effect = director.GetReferenceValue(exposedName, out bool isValid) as GameObject;
            if (isValid && effect != null) return effect;

            // 旧PrefabはUnityによるキー再生成前の名前で保持しているため、実ゲームSceneではこちらも確認する。
            effect = director.GetReferenceValue(new PropertyName("StabExplosionSource"), out isValid) as GameObject;
            if (isValid && effect != null) return effect;

            foreach (var child in director.transform.root.GetComponentsInChildren<Transform>(true))
                if (child.name == "StabExplosionPreview") return child.gameObject;
        }
        return null;
    }

    private static bool TryGetImpactEffectExposedName(TimelineAsset cutscene, out PropertyName exposedName)
    {
        foreach (var track in cutscene.GetOutputTracks())
        {
            if (track is not ControlTrack || track.name != "Stab Explosion") continue;

            foreach (var clip in track.GetClips())
            {
                if (clip.asset is not ControlPlayableAsset controlAsset) continue;
                exposedName = controlAsset.sourceGameObject.exposedName;
                if (!exposedName.Equals(default(PropertyName))) return true;
            }
        }

        exposedName = default;
        return false;
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

    // Timeline を指定時間で評価し、録画 world 座標を「録画時アンカー→実ボスの StabAnchor」へ
    // 位置＋ヨーの剛体変換で写す（相対関係は保たれるので演出はそのまま、ボスの向きにも追従する）。
    // 位置は毎フレーム Evaluate が録画値へ戻すのでピボット式を都度適用（累積しない）。
    // プレイヤールートは回転カーブを持たず Evaluate で戻らないため、向きは開始時に確定した一定値を適用する。
    private void EvaluateAt(Player player, double time)
    {
        m_director.time = time;
        m_director.Evaluate();
        player.transform.position = m_anchorA1 + m_anchorYawDelta * (player.transform.position - m_anchorA0);
        if (m_playerRotInitialized) player.transform.rotation = m_playerFinisherRot;
        m_cutsceneCamera?.ApplyCutsceneCameraAnchor(m_anchorA0, m_anchorA1, m_anchorYawDelta);
    }

    protected override void OnExit(Player player)
    {
        if (m_director != null)
        {
            m_director.Stop();
            Object.Destroy(m_director.gameObject);
            m_director = null;
        }
        // 演出で外したコントローラを戻す。直後の ReturnToNormal が State を再設定するので通常アニメへ復帰する。
        if (m_bodyAnim != null)
        {
            m_bodyAnim.runtimeAnimatorController = m_savedBodyController;
            m_bodyAnim = null;
            m_savedBodyController = null;
        }
        if (m_impactEffect != null)
        {
            Object.Destroy(m_impactEffect);
            m_impactEffect = null;
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
        player.stab.SetProgramHitVfxSuppressed(false);
        player.aim.SetTimeScaleControlSuspended(false);
    }

    // 既存のダメージ＋receiver.OnStabHit を再利用する。Timeline が爆発を駆動できない場合だけ汎用 VFX へフォールバックする。
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
