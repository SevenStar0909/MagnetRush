using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// ゲームパッドの振動を Timeline で駆動するトラック。バインド対象は無し（接続中のパッド Gamepad.current を直接鳴らす）。
/// CameraShake / TimeScale トラックと同じく、スタブ演出の Timeline に並べてプランナーが配置・調整する。
/// </summary>
[TrackColor(0.85f, 0.2f, 0.55f)]
[TrackClipType(typeof(GamepadRumbleClip))]
public class GamepadRumbleTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, UnityEngine.GameObject go, int inputCount)
    {
        return ScriptPlayable<GamepadRumbleMixerBehaviour>.Create(graph, inputCount);
    }
}
