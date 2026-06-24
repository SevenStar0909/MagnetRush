using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.25f, 0.65f, 1f)]
[TrackClipType(typeof(ScreenInvertClip))]
public class ScreenInvertTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<ScreenInvertMixerBehaviour>.Create(graph, inputCount);
    }
}
