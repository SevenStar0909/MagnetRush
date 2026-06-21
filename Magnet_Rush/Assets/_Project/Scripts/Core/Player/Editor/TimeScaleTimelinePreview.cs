using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Edit ModeでTimelineを再生した時も、TimeScaleTrackのクリップ位置・Ease・slowTimeScaleを
/// Timeline自身の再生速度へ反映する。Time.timeScaleは変更しないためTimeManager.assetを汚さない。
/// </summary>
[InitializeOnLoad]
public static class TimeScaleTimelinePreview
{
    private static PlayableDirector s_appliedDirector;

    static TimeScaleTimelinePreview()
    {
        EditorApplication.update += Update;
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
        EditorApplication.quitting += Restore;
        EditorApplication.playModeStateChanged += _ => Restore();
    }

    private static void Update()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Restore();
            return;
        }

        var director = TimelineEditor.inspectedDirector;
        var timeline = TimelineEditor.inspectedAsset;
        if (director == null || timeline == null || director.state != PlayState.Playing)
        {
            Restore();
            return;
        }

        var tracks = timeline.GetOutputTracks()
            .OfType<TimeScaleTrack>()
            .Where(track => !track.muted)
            .ToArray();
        if (tracks.Length == 0)
        {
            Restore();
            return;
        }

        if (s_appliedDirector != null && s_appliedDirector != director) Restore();
        s_appliedDirector = director;
        SetGraphSpeed(director, EvaluateScale(tracks, director.time));
    }

    private static float EvaluateScale(TimeScaleTrack[] tracks, double time)
    {
        float blend = 0f;
        float weightedSlow = 0f;

        foreach (var track in tracks)
        {
            foreach (var clip in track.GetClips())
            {
                if (time < clip.start || time >= clip.end || clip.asset is not TimeScaleClip asset) continue;

                float weight = clip.EvaluateMixIn(time) * clip.EvaluateMixOut(time);
                if (weight <= 0f) continue;
                weightedSlow += weight * asset.slowTimeScale;
                blend += weight;
            }
        }

        if (blend <= 0f) return 1f;
        float slow = weightedSlow / blend;
        return Mathf.Lerp(1f, slow, Mathf.Clamp01(blend));
    }

    private static void Restore()
    {
        if (s_appliedDirector != null) SetGraphSpeed(s_appliedDirector, 1f);
        s_appliedDirector = null;
    }

    private static void SetGraphSpeed(PlayableDirector director, float speed)
    {
        if (director == null || !director.playableGraph.IsValid()) return;

        int rootCount = director.playableGraph.GetRootPlayableCount();
        for (int i = 0; i < rootCount; i++)
            director.playableGraph.GetRootPlayable(i).SetSpeed(speed);
    }
}
