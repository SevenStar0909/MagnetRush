using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// スタブ演出の「着弾スロー」を Timeline のクリップとして編集できるようにするクリップ資産。
/// クリップの位置・長さ・両端のイーズ（ハンドル）でスローのタイミングと立ち上がり/戻りを作り、
/// slowTimeScale でスローの強さを決める。さらにクリップ先頭で freezeDuration 秒だけ
/// freezeTimeScale（既定 0＝完全停止）に固定してから slowTimeScale のスローへ移れるので、
/// クリップ1個で「着弾でガチ止め → スロー → 戻り」のヒットストップを作れる。
/// 実際の Time.timeScale 適用は TimeScaleMixerBehaviour が行う。
/// 依存: TimeScaleTrack（このクリップを載せるトラック）。
/// </summary>
[Serializable]
public class TimeScaleClip : PlayableAsset, ITimelineClipAsset
{
    [LabelRange("スローの強さ（0=完全停止 / 1=等速）", 0f, 1f)]
    [Tooltip("このクリップが完全に効いている時の時間の速さ。0=完全停止、1=等速。小さいほど強いスロー")]
    public float slowTimeScale = 0.15f;

    [Label("ガチ止めの長さ（秒・0なら止めずにスロー）")]
    [Tooltip("クリップ先頭で時間を止めておく長さ（秒）。0なら止めずに最初からスロー。ここでヒットストップの『ガチ止め』時間を作る")]
    public float freezeDuration = 0f;

    [LabelRange("ガチ止め中の強さ（0=完全停止）", 0f, 1f)]
    [Tooltip("止めている間の時間の速さ。0=ピタッと完全停止。slowTimeScale より強い（小さい）値にするのが普通")]
    public float freezeTimeScale = 0f;

    /// <summary>両端のイーズ／ブレンドを使う（スローの立ち上がり・戻りをハンドルで作る）。</summary>
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<TimeScaleBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.slowTimeScale = slowTimeScale;
        behaviour.freezeDuration = Mathf.Max(0f, freezeDuration);   // 負の値は「止めない」扱いに丸める
        behaviour.freezeTimeScale = freezeTimeScale;
        return playable;
    }
}

/// <summary>TimeScaleClip 1個分の実体。ミキサーが重み付けで参照するスロー強さと、先頭の完全停止設定を持つ。</summary>
[Serializable]
public class TimeScaleBehaviour : PlayableBehaviour
{
    public float slowTimeScale = 0.15f;
    public float freezeDuration = 0f;
    public float freezeTimeScale = 0f;

    /// <summary>クリップ内の経過時間に応じた実効スロー強さ。先頭 freezeDuration 秒は freezeTimeScale、その後は slowTimeScale。</summary>
    public float GetTimeScaleAt(double clipTime) =>
        clipTime < freezeDuration ? freezeTimeScale : slowTimeScale;
}
