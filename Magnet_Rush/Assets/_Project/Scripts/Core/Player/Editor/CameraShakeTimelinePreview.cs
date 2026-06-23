using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit ModeのTimeline評価後に保留中のカメラシェイクを適用する。
/// AnimationTrackの書き戻しより後に実行することで、Sceneビューでも実行時と同じ揺れを確認できる。
/// </summary>
[InitializeOnLoad]
public static class CameraShakeTimelinePreview
{
    static CameraShakeTimelinePreview()
    {
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!CameraShakeMixerBehaviour.ApplyPendingShakes()) return;

        SceneView.RepaintAll();
    }
}
