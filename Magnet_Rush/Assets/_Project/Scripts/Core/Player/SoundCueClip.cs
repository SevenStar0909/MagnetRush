using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Timeline 上で効果音(SE)を鳴らすクリップ。
/// クリップの開始位置＝鳴り始めるタイミング、Clip In＝SEの途中開始位置、クリップ終端＝停止タイミング。
/// </summary>
[Serializable]
public class SoundCueClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("鳴らす効果音のキュー名。SoundData.SE の値（例: Stab）")]
    public string cueName = SoundData.SE.Stab;

    [Tooltip("キューシート名。効果音は基本 SE のまま")]
    public string cueSheet = SoundData.CueSheet.SE;

    [Tooltip("バー内の音量カーブ。横軸 0=開始 / 1=終了、縦軸 1=通常音量")]
    public AnimationCurve volumeCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Tooltip("バー内のピッチカーブ。横軸 0=開始 / 1=終了、縦軸 0=通常ピッチ")]
    public AnimationCurve pitchCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);

    [Min(0.01f), Tooltip("SEの再生速度倍率。2で倍速、0.5で半速")]
    public float playbackSpeed = 1f;

    [Tooltip("バー内の再生速度カーブ。横軸 0=開始 / 1=終了、縦軸 1=通常速度")]
    public AnimationCurve speedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Min(0f), Tooltip("バー開始時の自動フェードイン秒数。短く入れて音のつなぎ目をなじませる")]
    public float fadeInDuration = 0.015f;

    [Min(0f), Tooltip("バー終了時の自動フェードアウト秒数。短く入れて音の切れ目をなじませる")]
    public float fadeOutDuration = 0.015f;

    public override double duration => 0.2d;
    public ClipCaps clipCaps => ClipCaps.ClipIn | ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SoundCueBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.clip = this;
        behaviour.cueSheet = cueSheet;
        behaviour.cueName = cueName;
        behaviour.volumeCurve = volumeCurve;
        behaviour.pitchCurve = pitchCurve;
        behaviour.playbackSpeed = playbackSpeed;
        behaviour.speedCurve = speedCurve;
        behaviour.fadeInDuration = fadeInDuration;
        behaviour.fadeOutDuration = fadeOutDuration;
        return playable;
    }
}

[Serializable]
public class SoundCueBehaviour : PlayableBehaviour
{
    [NonSerialized] public SoundCueClip clip;

    public string cueSheet;
    public string cueName;
    public AnimationCurve volumeCurve;
    public AnimationCurve pitchCurve;
    public float playbackSpeed;
    public AnimationCurve speedCurve;
    public float fadeInDuration;
    public float fadeOutDuration;

    // クリップ範囲に入っている間 true。範囲を出たら Mixer がリセットし、再入で鳴らし直せる。
    [NonSerialized] public bool fired;
    [NonSerialized] public double playbackStartTime;
    [NonSerialized] public Sound.TimelineCuePlayback playback;
}
