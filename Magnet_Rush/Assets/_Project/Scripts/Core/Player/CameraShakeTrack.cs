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
    [Tooltip("実行時に揺らすカメラ番号（0始まり）。StabFinisherCutscene の5台目は4")]
    [Min(0)] public int runtimeCameraIndex = 4;

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CameraShakeMixerBehaviour>.Create(graph, inputCount);
    }

    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        base.GatherProperties(director, driver);
        var animator = director.GetGenericBinding(this) as Animator;
        if (animator == null) return;

        driver.AddFromName<Transform>(animator.gameObject, "m_LocalPosition.x");
        driver.AddFromName<Transform>(animator.gameObject, "m_LocalPosition.y");
        driver.AddFromName<Transform>(animator.gameObject, "m_LocalPosition.z");
    }
}
