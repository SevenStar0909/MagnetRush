using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>決定的なシェイク値を計算し、AnimationTrack評価完了後に実カメラへ適用できるよう保持する。</summary>
public class CameraShakeMixerBehaviour : PlayableBehaviour
{
    private static readonly Dictionary<Transform, Vector3> s_pendingLocalOffsets = new();

    public bool useBoundCameraInspector;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var animator = playerData as Animator;
        if (animator == null) return;

        Transform cameraTransform = FindCameraTransform(animator);
        if (cameraTransform == null) return;

        var inspectorSettings = useBoundCameraInspector
            ? animator.GetComponent<StabFinisherCamera>()
            : null;
        var runtimeSettings = useBoundCameraInspector
            ? animator.GetComponent<CameraShakeRuntimeSettings>()
            : null;

        Vector3 localOffset = Vector3.zero;
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var input = (ScriptPlayable<CameraShakeBehaviour>)playable.GetInput(i);
            var behaviour = input.GetBehaviour();
            float amplitude = runtimeSettings != null ? runtimeSettings.amplitude
                : inspectorSettings != null ? inspectorSettings.ShakeAmplitude : behaviour.amplitude;
            float frequency = runtimeSettings != null ? runtimeSettings.frequency
                : inspectorSettings != null ? inspectorSettings.ShakeFrequency : behaviour.frequency;
            float duration = behaviour.useClipDuration
                ? Mathf.Max(0f, (float)input.GetDuration())
                : runtimeSettings != null ? runtimeSettings.duration
                : inspectorSettings != null ? inspectorSettings.ShakeDuration
                : behaviour.shakeDuration;
            Vector3 axis = runtimeSettings != null ? runtimeSettings.axis
                : inspectorSettings != null ? inspectorSettings.ShakeAxis : behaviour.axis;
            float decayPower = runtimeSettings != null ? runtimeSettings.decayPower
                : inspectorSettings != null ? inspectorSettings.ShakeDecayPower : behaviour.decayPower;
            if (duration <= 0f || amplitude <= 0f) continue;

            float elapsed = Mathf.Clamp((float)input.GetTime(), 0f, duration);
            if (elapsed >= duration) continue;
            float normalized = elapsed / duration;
            float decay = Mathf.Pow(1f - normalized, decayPower);
            float phase = elapsed * frequency * 2f * Mathf.PI;

            Vector3 wave = new(
                Mathf.Sin(phase * 1.13f),
                Mathf.Cos(phase),
                Mathf.Sin(phase * 0.83f + 1.7f));
            localOffset += Vector3.Scale(axis, wave) * (amplitude * decay * weight);
        }

        // AnimationTrackは、このMixerのProcessFrame後にもFinisherCameraを書き戻す。
        // ここでは値だけを保持し、PlayableGraph全体のEvaluate完了後に適用する。
        s_pendingLocalOffsets[cameraTransform] = localOffset;
    }

    /// <summary>
    /// PlayableDirector.Evaluate後に呼び、AnimationTrackの最終姿勢へシェイクを加算する。
    /// Edit ModeではCameraShakeTimelinePreview、実行時はBossStabFinisherStateが呼ぶ。
    /// </summary>
    public static bool ApplyPendingShakes()
    {
        bool applied = false;
        foreach (var pair in s_pendingLocalOffsets)
        {
            Transform cameraTransform = pair.Key;
            Vector3 localOffset = pair.Value;
            if (cameraTransform == null || localOffset.sqrMagnitude <= 0f) continue;

            cameraTransform.position += cameraTransform.rotation * localOffset;
            applied = true;
        }

        s_pendingLocalOffsets.Clear();
        return applied;
    }

    public static Transform FindCameraTransform(Animator animator)
    {
        if (animator == null) return null;

        Transform namedCamera = animator.transform.Find("FinisherCamera");
        if (namedCamera != null) return namedCamera;

        Camera childCamera = animator.GetComponentInChildren<Camera>(true);
        return childCamera != null ? childCamera.transform : null;
    }
}

/// <summary>Scene上のFinisherCameraRig設定を、実行時に生成される一時カメラへ渡すためのコピー。</summary>
public sealed class CameraShakeRuntimeSettings : MonoBehaviour
{
    public float amplitude;
    public float duration;
    public float frequency;
    public Vector3 axis;
    public float decayPower;

    public void CopyFrom(StabFinisherCamera source)
    {
        amplitude = source.ShakeAmplitude;
        duration = source.ShakeDuration;
        frequency = source.ShakeFrequency;
        axis = source.ShakeAxis;
        decayPower = source.ShakeDecayPower;
    }
}
