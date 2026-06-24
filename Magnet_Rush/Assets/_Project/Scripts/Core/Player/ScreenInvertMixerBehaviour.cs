using UnityEngine;
using UnityEngine.Playables;

public class ScreenInvertMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        float value = 0f;
        ScreenInvertBehaviour strongest = null;
        double strongestTime = 0d;
        float strongestWeight = 0f;
        for (int i = 0; i < playable.GetInputCount(); i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;
            var input = (ScriptPlayable<ScreenInvertBehaviour>)playable.GetInput(i);
            var behaviour = input.GetBehaviour();
            value = Mathf.Max(value, weight * behaviour.strength);
            if (weight <= strongestWeight) continue;
            strongestWeight = weight;
            strongest = behaviour;
            strongestTime = input.GetTime();
        }

        ScreenInvertEffect.Strength = Mathf.Clamp01(value);
        if (strongest == null) return;

        ScreenInvertEffect.Contrast = strongest.contrast;
        ScreenInvertEffect.Threshold = strongest.threshold;
        int phase = Mathf.FloorToInt((float)strongestTime * strongest.flickerRate);
        ScreenInvertEffect.InvertPolarity = (phase & 1) == 0 ? 0f : 1f;
    }

    public override void OnGraphStop(Playable playable) => Restore();
    public override void OnPlayableDestroy(Playable playable) => Restore();

    private static void Restore()
    {
        ScreenInvertEffect.Strength = 0f;
        ScreenInvertEffect.InvertPolarity = 0f;
    }
}
