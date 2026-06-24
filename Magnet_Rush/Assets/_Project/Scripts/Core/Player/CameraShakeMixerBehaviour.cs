using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

/// <summary>
/// Timeline クリップの値から決定的なカメラ揺れを計算し、カメラが描画される直前に実カメラへ加算する。
/// ProcessFrame では値を保持するだけにし、URP の beginCameraRendering で適用→endCameraRendering で復元する。
/// これにより AnimationTrack の書き戻し（焼き込んだカメラ位置）より確実に後で乗るため、
/// 実行時・Edit Mode プレビューの両方でタイミングに依らず揺れが見える。
/// </summary>
public class CameraShakeMixerBehaviour : PlayableBehaviour
{
    private static readonly Dictionary<Transform, Vector3> s_pendingLocalOffsets = new();
    private static readonly Dictionary<Transform, Vector3> s_renderRestore = new();
    private static bool s_hooked;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var animator = playerData as Animator;
        if (animator == null) return;

        Transform cameraTransform = FindCameraTransform(animator);
        if (cameraTransform == null) return;

        Vector3 localOffset = Vector3.zero;
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var input = (ScriptPlayable<CameraShakeBehaviour>)playable.GetInput(i);
            var behaviour = input.GetBehaviour();
            if (behaviour.amplitude <= 0f) continue;

            // 揺れの尺＝クリップ長。クリップの端を動かせばシェイクの長さを調整できる。
            float duration = Mathf.Max(0.0001f, (float)input.GetDuration());
            float elapsed = Mathf.Clamp((float)input.GetTime(), 0f, duration);
            float normalized = elapsed / duration;
            float decay = Mathf.Pow(1f - normalized, behaviour.decayPower);
            float phase = elapsed * behaviour.frequency * 2f * Mathf.PI;

            Vector3 wave = new(
                Mathf.Sin(phase * 1.13f),
                Mathf.Cos(phase),
                Mathf.Sin(phase * 0.83f + 1.7f));
            localOffset += Vector3.Scale(behaviour.axis, wave) * (behaviour.amplitude * decay * weight);
        }

        // 保持だけ。実カメラへの適用は描画直前（OnBeginCameraRendering）で行う。
        s_pendingLocalOffsets[cameraTransform] = localOffset;
    }

    /// <summary>
    /// 描画直前/直後フックを一度だけ登録する。実行時は RuntimeInitializeOnLoadMethod、
    /// Edit Mode は CameraShakeTimelinePreview から呼ばれる。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void EnsureHooked()
    {
        if (s_hooked) return;
        s_hooked = true;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    /// <summary>シーン遷移・演出終了時に保持中のオフセットを捨てる（破棄済み rig の残骸を残さない）。</summary>
    public static void ClearPending()
    {
        s_pendingLocalOffsets.Clear();
        s_renderRestore.Clear();
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == null) return;
        if (!s_pendingLocalOffsets.TryGetValue(camera.transform, out Vector3 offset)) return;
        if (offset.sqrMagnitude <= 0f) return;

        var t = camera.transform;
        s_renderRestore[t] = t.position;
        t.position += t.rotation * offset;
    }

    private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == null) return;
        var t = camera.transform;
        if (!s_renderRestore.TryGetValue(t, out Vector3 original)) return;

        t.position = original;
        s_renderRestore.Remove(t);
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
