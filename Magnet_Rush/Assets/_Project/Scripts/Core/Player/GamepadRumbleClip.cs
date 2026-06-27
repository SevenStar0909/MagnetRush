using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Timeline 上でゲームパッドの振動（強さ・減衰）を編集するクリップ。クリップの長さ＝振動の長さ。</summary>
[Serializable]
public class GamepadRumbleClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("重いゴロゴロした振動（低周波モーター）の強さ。0で無効、1で最大")]
    [LabelMin("重い振動の強さ（0〜1）", 0f)] public float lowFrequency = 0.8f;

    [Tooltip("軽いブルブルした振動（高周波モーター）の強さ。0で無効、1で最大")]
    [LabelMin("軽い振動の強さ（0〜1）", 0f)] public float highFrequency = 0.6f;

    [Tooltip("減衰の強さ。1=一定の強さで鳴ってクリップ終わりにゼロ、2=最初に強くすぐ弱まる（突き刺さりのガツンに向く）")]
    [LabelMin("減衰の強さ", 0.01f)] public float decayPower = 2f;

    public override double duration => 0.2d;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<GamepadRumbleBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.lowFrequency = lowFrequency;
        behaviour.highFrequency = highFrequency;
        behaviour.decayPower = decayPower;
        return playable;
    }
}

[Serializable]
public class GamepadRumbleBehaviour : PlayableBehaviour
{
    public float lowFrequency;
    public float highFrequency;
    public float decayPower;
}
