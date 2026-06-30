using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Timeline 上で効果音(SE)を1回鳴らすクリップ。クリップの開始位置＝鳴り始めるタイミング。
/// プランナーがスタブ演出の Timeline に並べて、SE のタイミングを調整できるようにする。
/// </summary>
[Serializable]
public class SoundCueClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("鳴らす効果音のキュー名。SoundData.SE の値（例: Stab）")]
    public string cueName = SoundData.SE.Stab;

    [Tooltip("キューシート名。効果音は基本 SE のまま")]
    public string cueSheet = SoundData.CueSheet.SE;

    public override double duration => 0.2d;
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SoundCueBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.cueSheet = cueSheet;
        behaviour.cueName = cueName;
        return playable;
    }
}

[Serializable]
public class SoundCueBehaviour : PlayableBehaviour
{
    public string cueSheet;
    public string cueName;

    // クリップ範囲に入っている間 true。範囲を出たら Mixer がリセットし、再入で鳴らし直せる。
    [NonSerialized] public bool fired;
}
