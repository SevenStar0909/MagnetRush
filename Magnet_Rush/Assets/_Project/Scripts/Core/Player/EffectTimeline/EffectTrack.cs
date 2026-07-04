using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// どのタイムラインにも置ける汎用エフェクトトラック。
/// クリップの中身（音／VFX／オブジェクト移動／カメラ移動）は EffectClip 側で選ぶ。
/// 1本のトラックに種類の違うエフェクトを混ぜて並べられる。
/// </summary>
[TrackColor(0.35f, 0.75f, 0.95f)]
[TrackClipType(typeof(EffectClip))]
public class EffectTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<EffectMixerBehaviour>.Create(graph, inputCount);
    }
}
