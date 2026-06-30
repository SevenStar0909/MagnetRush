using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 効果音(SE)を Timeline で鳴らすトラック。バインド対象なし（Sound ファサード経由で再生）。
/// CameraShake / GamepadRumble トラックと同じく、スタブ演出の Timeline に並べてプランナーが配置・調整する。
/// </summary>
[TrackColor(0.95f, 0.75f, 0.15f)]
[TrackClipType(typeof(SoundCueClip))]
public class SoundCueTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, UnityEngine.GameObject go, int inputCount)
    {
        return ScriptPlayable<SoundCueMixerBehaviour>.Create(graph, inputCount);
    }
}
