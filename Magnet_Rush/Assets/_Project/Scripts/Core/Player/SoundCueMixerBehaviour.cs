using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// SoundCueClip がクリップ範囲に入った瞬間に SE を1回だけ鳴らす。
/// ランタイムは Sound.Play、Edit Mode の Timeline プレビュー再生中は EditorPreviewPlay フック経由で鳴らす。
/// クリップ長は「鳴らすタイミングの窓」であって、SE 自体の長さはキュー側で決まる。
/// </summary>
public class SoundCueMixerBehaviour : PlayableBehaviour
{
    /// <summary>
    /// Edit Mode の Timeline プレビュー再生で SE を鳴らすためのフック。(キューシート名, キュー名)。
    /// SoundCueTimelinePreview([InitializeOnLoad]) が登録する。ビルド・ランタイムでは未使用（null のまま）。
    /// </summary>
    public static System.Action<string, string> EditorPreviewPlay;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            var input = (ScriptPlayable<SoundCueBehaviour>)playable.GetInput(i);
            var behaviour = input.GetBehaviour();

            bool active = playable.GetInputWeight(i) > 0f;
            if (active && !behaviour.fired)
            {
                Fire(behaviour.cueSheet, behaviour.cueName);
                behaviour.fired = true;
            }
            else if (!active && behaviour.fired)
            {
                // クリップ範囲を出たらリセット。リプレイ・巻き戻しで再度鳴らせる。
                behaviour.fired = false;
            }
        }
    }

    private static void Fire(string cueSheet, string cueName)
    {
        if (Application.isPlaying)
            Sound.Play(cueSheet, cueName);
        else
            // Edit Mode の Timeline プレビュー再生時のみ。フック未登録のビルドでは null で何もしない。
            EditorPreviewPlay?.Invoke(cueSheet, cueName);
    }
}
