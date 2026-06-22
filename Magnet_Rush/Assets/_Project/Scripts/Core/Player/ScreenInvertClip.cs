using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 全画面の色反転を Timeline 上で配置するクリップ。
/// 新規作成時は 60fps の2フレーム尺。クリップの移動・リサイズと strength で調整できる。
/// </summary>
[Serializable]
public class ScreenInvertClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("反転の強さ。1=完全反転、0=通常。クリップのブレンドハンドルでも補間可能")]
    [Range(0f, 1f)] public float strength = 1f;

    public override double duration => 2d / 60d;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ScreenInvertBehaviour>.Create(graph);
        playable.GetBehaviour().strength = strength;
        return playable;
    }
}

[Serializable]
public class ScreenInvertBehaviour : PlayableBehaviour
{
    public float strength = 1f;
}
