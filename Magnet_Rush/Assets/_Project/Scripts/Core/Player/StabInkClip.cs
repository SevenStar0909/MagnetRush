using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 「効果だけ黒・他は白」インク演出を Timeline のクリップとして編集できるようにする資産。
/// クリップの位置・長さ・両端のイーズで白黒化のタイミングと立ち上がり/戻りを作り、strength で最大の強さを決める。
/// 実際の StabInkEffect.Strength 適用は StabInkMixerBehaviour が行う。依存: StabInkTrack。
/// </summary>
[Serializable]
public class StabInkClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("このクリップが完全に効いている時の強さ。1=完全に効果黒・他白、0=通常。両端イーズでフェードできる")]
    [Range(0f, 1f)] public float strength = 1f;

    /// <summary>両端のイーズ／ブレンドを使う（白黒化の立ち上がり・戻りをハンドルで作る）。</summary>
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<StabInkBehaviour>.Create(graph);
        playable.GetBehaviour().strength = strength;
        return playable;
    }
}

/// <summary>StabInkClip 1個分の実体。ミキサーが重み付けで参照する強さだけ持つ。</summary>
[Serializable]
public class StabInkBehaviour : PlayableBehaviour
{
    public float strength = 1f;
}
