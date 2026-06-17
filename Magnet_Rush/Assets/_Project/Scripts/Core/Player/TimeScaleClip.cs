using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// スタブ演出の「着弾スロー」を Timeline のクリップとして編集できるようにするクリップ資産。
/// クリップの位置・長さ・両端のイーズ（ハンドル）でスローのタイミングと立ち上がり/戻りを作り、
/// slowTimeScale でスローの強さを決める。実際の Time.timeScale 適用は TimeScaleMixerBehaviour が行う。
/// 依存: TimeScaleTrack（このクリップを載せるトラック）。
/// </summary>
[Serializable]
public class TimeScaleClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("このクリップが完全に効いている時の時間の速さ。0=完全停止、1=等速。小さいほど強いスロー")]
    [Range(0f, 1f)] public float slowTimeScale = 0.15f;

    /// <summary>両端のイーズ／ブレンドを使う（スローの立ち上がり・戻りをハンドルで作る）。</summary>
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<TimeScaleBehaviour>.Create(graph);
        playable.GetBehaviour().slowTimeScale = slowTimeScale;
        return playable;
    }
}

/// <summary>TimeScaleClip 1個分の実体。ミキサーが重み付けで参照するスロー強さだけ持つ。</summary>
[Serializable]
public class TimeScaleBehaviour : PlayableBehaviour
{
    public float slowTimeScale = 0.15f;
}
