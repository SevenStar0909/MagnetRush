using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// スタブ着弾の「効果だけ黒・他は白」インク演出を Timeline のクリップで制御するトラック。
/// クリップが効いている区間だけ StabInkEffect.Strength を上げる。実適用は StabInkMixerBehaviour。
/// 黒のシルエットは実際のスタブVFX（StabInkScreen が付く）が描画されている範囲に出るので、
/// クリップは VFX が出る時刻（着弾＝cutsceneHitTime 付近）に合わせて置くこと。
/// </summary>
[TrackColor(0.1f, 0.1f, 0.1f)]
[TrackClipType(typeof(StabInkClip))]
public class StabInkTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<StabInkMixerBehaviour>.Create(graph, inputCount);
    }
}
