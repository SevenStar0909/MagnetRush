using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// SoundCueClip がクリップ範囲に入った瞬間に SE を1回だけ鳴らす。
/// 物理的な再生なので Edit Mode のスクラブでは鳴らさず（誤爆防止）、再生中のみ発火する。
/// クリップ長は「鳴らすタイミングの窓」であって、SE 自体の長さはキュー側で決まる。
/// </summary>
public class SoundCueMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // Edit Mode のプレビュー中は鳴らさない（スクラブで誤爆させない）。
        if (!Application.isPlaying) return;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            var input = (ScriptPlayable<SoundCueBehaviour>)playable.GetInput(i);
            var behaviour = input.GetBehaviour();

            bool active = playable.GetInputWeight(i) > 0f;
            if (active && !behaviour.fired)
            {
                Sound.Play(behaviour.cueSheet, behaviour.cueName);
                behaviour.fired = true;
            }
            else if (!active && behaviour.fired)
            {
                // クリップ範囲を出たらリセット。リプレイ・巻き戻しで再度鳴らせる。
                behaviour.fired = false;
            }
        }
    }
}
