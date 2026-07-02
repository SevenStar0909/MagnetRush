using System;
using UnityEngine;

/// <summary>
/// タイムライン上で SE / BGM を1回鳴らすエフェクト。クリップの頭で Sound.Play する。
/// キューシート名・キュー名は SoundData の値（例 "SE" / "PlayerShot"）と一致させる。
/// </summary>
[Serializable]
public class SoundEffect : TimelineEffect
{
    [Tooltip("鳴らすキューシート名。SE か BGM。SoundData.CueSheet と一致させる")]
    public string cueSheet = "SE";

    [Tooltip("鳴らすキュー名。SoundData.SE の値と一致させる（例 PlayerShot）")]
    public string cue = "";

    // 音はスクラブのたびに鳴るとうるさいので Edit Mode プレビューでは鳴らさない。
    public override bool PreviewInEditMode => false;

    public override void OnEnter(in EffectContext ctx)
    {
        if (string.IsNullOrEmpty(cue)) return;
        Sound.Play(cueSheet, cue);
    }
}
