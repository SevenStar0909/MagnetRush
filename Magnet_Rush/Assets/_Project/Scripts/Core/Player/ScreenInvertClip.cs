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

    [Tooltip("白黒の境界の硬さ。大きいほど中間の灰色が減り、バチッとした白黒になる")]
    [Range(1f, 50f)] public float contrast = 12f;

    [Tooltip("白になる明るさの境界。下げると白が増え、上げると黒が増える")]
    [Range(0f, 1f)] public float threshold = 0.5f;

    [Tooltip("1秒あたりの白黒反転回数。60なら60fpsで毎フレーム反転する")]
    [Min(0f)] public float flickerRate = 60f;

    public override double duration => 2d / 60d;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ScreenInvertBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.strength = strength;
        behaviour.contrast = contrast;
        behaviour.threshold = threshold;
        behaviour.flickerRate = flickerRate;
        return playable;
    }
}

[Serializable]
public class ScreenInvertBehaviour : PlayableBehaviour
{
    public float strength = 1f;
    public float contrast = 12f;
    public float threshold = 0.5f;
    public float flickerRate = 60f;
}
