using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// SoundCueClip がクリップ範囲に入った瞬間に SE を鳴らし、範囲を出たら停止する。
/// ランタイムは Sound.PlayTimelineCue、Edit Mode の Timeline プレビュー再生中は EditorPreviewPlay フック経由で鳴らす。
/// </summary>
public class SoundCueMixerBehaviour : PlayableBehaviour
{
    /// <summary>
    /// Edit Mode の Timeline プレビュー再生で SE を鳴らすためのフック。(キューシート名, キュー名, 開始位置秒) → 再生ハンドル。
    /// SoundCueTimelinePreview([InitializeOnLoad]) が登録する。ビルド・ランタイムでは未使用（null のまま）。
    /// </summary>
    public static System.Func<string, string, double, float, Sound.TimelineCuePlayback> EditorPreviewPlay;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            var input = (ScriptPlayable<SoundCueBehaviour>)playable.GetInput(i);
            var behaviour = input.GetBehaviour();
            if (behaviour == null) continue;

            bool active = playable.GetInputWeight(i) > 0f;
            if (active && !behaviour.fired)
            {
                behaviour.playbackStartTime = input.GetTime();
                float initialSpeed = EvaluatePlaybackSpeed(behaviour, 0f);
                behaviour.playback = Play(GetCueSheet(behaviour), GetCueName(behaviour), behaviour.playbackStartTime, initialSpeed);
                behaviour.fired = behaviour.playback != null;
            }

            if (active && behaviour.fired)
                ApplyCurves(behaviour, input.GetTime(), input.GetDuration(), playable.GetInputWeight(i));
            if (!active && behaviour.fired)
            {
                Stop(behaviour);
            }
        }
    }

    public override void OnGraphStop(Playable playable)
    {
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            var input = (ScriptPlayable<SoundCueBehaviour>)playable.GetInput(i);
            Stop(input.GetBehaviour());
        }
    }

    private static Sound.TimelineCuePlayback Play(string cueSheet, string cueName, double startTimeSeconds, float initialPlaybackSpeed)
    {
        if (Application.isPlaying)
            return Sound.PlayTimelineCue(cueSheet, cueName, startTimeSeconds, initialPlaybackSpeed);

        // Edit Mode の Timeline プレビュー再生時のみ。フック未登録のビルドでは null で何もしない。
        return EditorPreviewPlay?.Invoke(cueSheet, cueName, startTimeSeconds, initialPlaybackSpeed);
    }

    private static void Stop(SoundCueBehaviour behaviour)
    {
        if (behaviour == null) return;

        behaviour.playback?.Stop();
        behaviour.playback = null;
        behaviour.fired = false;
    }

    private static void ApplyCurves(SoundCueBehaviour behaviour, double currentTime, double duration, float inputWeight)
    {
        if (behaviour == null) return;

        float elapsed = Mathf.Max(0f, (float)(currentTime - behaviour.playbackStartTime));
        float durationSeconds = 0f;
        if (duration > 0.0 && !double.IsInfinity(duration))
            durationSeconds = (float)duration;

        float normalizedTime = 0f;
        if (durationSeconds > 0f)
            normalizedTime = Mathf.Clamp01(elapsed / durationSeconds);

        AnimationCurve volumeCurve = behaviour.clip != null ? behaviour.clip.volumeCurve : behaviour.volumeCurve;
        AnimationCurve pitchCurve = behaviour.clip != null ? behaviour.clip.pitchCurve : behaviour.pitchCurve;
        float fadeInDuration = behaviour.clip != null ? behaviour.clip.fadeInDuration : behaviour.fadeInDuration;
        float fadeOutDuration = behaviour.clip != null ? behaviour.clip.fadeOutDuration : behaviour.fadeOutDuration;

        float volume = volumeCurve != null ? volumeCurve.Evaluate(normalizedTime) : 1f;
        volume *= Mathf.Clamp01(inputWeight);
        volume *= EvaluateEdgeFade(elapsed, durationSeconds, fadeInDuration, fadeOutDuration);

        float pitch = pitchCurve != null ? pitchCurve.Evaluate(normalizedTime) : 0f;
        float playbackSpeed = EvaluatePlaybackSpeed(behaviour, normalizedTime);

        behaviour.playback?.SetParameters(volume, pitch, playbackSpeed);
    }

    private static string GetCueSheet(SoundCueBehaviour behaviour)
        => behaviour.clip != null ? behaviour.clip.cueSheet : behaviour.cueSheet;

    private static string GetCueName(SoundCueBehaviour behaviour)
        => behaviour.clip != null ? behaviour.clip.cueName : behaviour.cueName;

    private static float EvaluatePlaybackSpeed(SoundCueBehaviour behaviour, float normalizedTime)
    {
        float baseSpeed = behaviour.clip != null ? behaviour.clip.playbackSpeed : behaviour.playbackSpeed;
        AnimationCurve speedCurve = behaviour.clip != null ? behaviour.clip.speedCurve : behaviour.speedCurve;
        float speedCurveValue = speedCurve != null ? speedCurve.Evaluate(normalizedTime) : 1f;
        return Mathf.Max(0.01f, baseSpeed * speedCurveValue);
    }

    private static float EvaluateEdgeFade(float elapsed, float duration, float fadeIn, float fadeOut)
    {
        if (duration <= 0f) return 1f;

        float maxFade = duration * 0.5f;
        fadeIn = Mathf.Min(Mathf.Max(0f, fadeIn), maxFade);
        fadeOut = Mathf.Min(Mathf.Max(0f, fadeOut), maxFade);

        float fade = 1f;
        if (fadeIn > 0f)
            fade *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeIn));
        if (fadeOut > 0f)
            fade *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((duration - elapsed) / fadeOut));
        return fade;
    }
}
