using UnityEngine;
using UnityEngine.Playables;

public class ScreenInvertMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        float value = 0f;
        for (int i = 0; i < playable.GetInputCount(); i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;
            var input = (ScriptPlayable<ScreenInvertBehaviour>)playable.GetInput(i);
            value = Mathf.Max(value, weight * input.GetBehaviour().strength);
        }

        ScreenInvertEffect.Strength = Mathf.Clamp01(value);
    }

    public override void OnGraphStop(Playable playable) => Restore();
    public override void OnPlayableDestroy(Playable playable) => Restore();

    private static void Restore()
    {
        ScreenInvertEffect.Strength = 0f;
    }
}
