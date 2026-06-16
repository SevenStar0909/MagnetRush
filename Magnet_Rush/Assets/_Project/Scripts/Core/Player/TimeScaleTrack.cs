using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 時間倍率（Time.timeScale）を Timeline 上のクリップで制御するトラック。
/// スタブ演出の着弾スローを「タイムライン上のバー」として編集できるようにするためのもの。
/// クリップが効いていない区間は等速（1）に戻す。実適用は TimeScaleMixerBehaviour。
/// </summary>
[TrackColor(0.4f, 0.6f, 1f)]
[TrackClipType(typeof(TimeScaleClip))]
public class TimeScaleTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimeScaleMixerBehaviour>.Create(graph, inputCount);
    }
}
