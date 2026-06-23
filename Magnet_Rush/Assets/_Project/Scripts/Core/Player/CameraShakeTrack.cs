using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Animator をルートに持つカメラ rig を揺らす Timeline トラック。
/// Scene 上ではトラックへ対象 Animator をバインドし、実行時は cameraIndex のrigへ自動バインドする。
/// </summary>
[TrackColor(1f, 0.55f, 0.15f)]
[TrackClipType(typeof(CameraShakeClip))]
[TrackBindingType(typeof(Animator))]
public class CameraShakeTrack : TrackAsset
{
    public enum SettingsSource
    {
        TimelineClip,
        BoundCameraInspector
    }

    [Tooltip("実行時に揺らすカメラ番号（0始まり）。StabFinisherCutscene の5台目は4")]
    [Min(0)] public int runtimeCameraIndex = 4;

    [Tooltip("Timeline Clip: 各シェイククリップのInspector値を使用。Bound Camera Inspector: バインド先のStabFinisherCamera値を使用")]
    public SettingsSource settingsSource = SettingsSource.TimelineClip;

    public bool UsesBoundCameraInspector => settingsSource == SettingsSource.BoundCameraInspector;

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var playable = ScriptPlayable<CameraShakeMixerBehaviour>.Create(graph, inputCount);
        playable.GetBehaviour().useBoundCameraInspector = UsesBoundCameraInspector;
        return playable;
    }

    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        base.GatherProperties(director, driver);
        var animator = director.GetGenericBinding(this) as Animator;
        if (animator == null) return;

        Transform cameraTransform = CameraShakeMixerBehaviour.FindCameraTransform(animator);
        if (cameraTransform == null) return;

        // Edit ModeのTimelineプレビュー終了時に、シェイクを加算した実カメラの位置を確実に戻す。
        driver.AddFromName<Transform>(cameraTransform.gameObject, "m_LocalPosition.x");
        driver.AddFromName<Transform>(cameraTransform.gameObject, "m_LocalPosition.y");
        driver.AddFromName<Transform>(cameraTransform.gameObject, "m_LocalPosition.z");
    }
}
