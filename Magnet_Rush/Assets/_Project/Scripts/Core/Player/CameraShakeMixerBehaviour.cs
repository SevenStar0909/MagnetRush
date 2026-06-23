using UnityEngine;
using UnityEngine.Playables;

/// <summary>AnimationTrack 評価後のカメラ rig へ決定的なオフセットを加え、Sceneプレビューでも同じ揺れを再現する。</summary>
public class CameraShakeMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var animator = playerData as Animator;
        if (animator == null) return;

        var inspectorSettings = animator.GetComponent<StabFinisherCamera>();
        var runtimeSettings = animator.GetComponent<CameraShakeRuntimeSettings>();

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
            float duration = runtimeSettings != null ? runtimeSettings.duration
                : inspectorSettings != null ? inspectorSettings.ShakeDuration : behaviour.shakeDuration;
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

        if (localOffset.sqrMagnitude <= 0f) return;

        Transform cameraTransform = animator.transform.Find("FinisherCamera");
        Quaternion cameraRotation = cameraTransform != null ? cameraTransform.rotation : animator.transform.rotation;
        animator.transform.position += cameraRotation * localOffset;
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
