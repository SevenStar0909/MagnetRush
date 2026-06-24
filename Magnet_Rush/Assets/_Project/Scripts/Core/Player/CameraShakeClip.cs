using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Timeline 上でカメラシェイクの強さ・速さ・方向・減衰を編集するクリップ。</summary>
[Serializable]
public class CameraShakeClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("揺れの最大振幅（m）")]
    [LabelMin("揺れの大きさ（m）", 0f)] public float amplitude = 0.35f;

    [Tooltip("1秒あたりの揺れの回数。低いほど重く、高いほど細かい揺れになる")]
    [LabelMin("揺れの速さ（回/秒）", 0f)] public float frequency = 22f;

    [Tooltip("バインド先に StabFinisherCamera が無い場合に使う揺れの長さ（秒）。ボス演出ではクリップの長さが優先される")]
    [LabelMin("揺れの長さ（秒・予備）", 0f)] public float shakeDuration = 0.3f;

    [Tooltip("カメラローカル方向ごとの揺れ比率。X=横、Y=縦、Z=前後")]
    [Label("揺れの方向（X横/Y縦/Z前後）")] public Vector3 axis = new(0.15f, 1f, 0f);

    [Tooltip("減衰の強さ。1=直線、2=前半で素早く減衰")]
    [LabelMin("減衰の強さ", 0.01f)] public float decayPower = 2f;

    public override double duration => 1d;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<CameraShakeBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.amplitude = amplitude;
        behaviour.frequency = frequency;
        behaviour.shakeDuration = shakeDuration;
        behaviour.axis = axis;
        behaviour.decayPower = decayPower;
        return playable;
    }
}

[Serializable]
public class CameraShakeBehaviour : PlayableBehaviour
{
    public float amplitude;
    public float frequency;
    public float shakeDuration;
    public Vector3 axis;
    public float decayPower;
}
