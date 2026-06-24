using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine.Playables;

/// <summary>
/// Edit Mode で Timeline を再生した時も CameraShakeMixerBehaviour の描画フックを登録し、
/// カメラ揺れを実カメラへ乗せる。実行時は CameraShakeMixerBehaviour 側の
/// RuntimeInitializeOnLoadMethod が登録するので、ここは Edit Mode 専用の登録役。
/// 再生していない間は溜まった揺れオフセットを捨て、古い値が描画フックでカメラへ乗り続けて
/// 停止後に位置がずれて見えるのを防ぐ。
/// </summary>
[InitializeOnLoad]
public static class CameraShakeTimelinePreview
{
    static CameraShakeTimelinePreview()
    {
        CameraShakeMixerBehaviour.EnsureHooked();
        EditorApplication.update += Update;
        AssemblyReloadEvents.beforeAssemblyReload += CameraShakeMixerBehaviour.ClearPending;
        EditorApplication.playModeStateChanged += _ => CameraShakeMixerBehaviour.ClearPending();
    }

    private static void Update()
    {
        var director = TimelineEditor.inspectedDirector;
        // 再生中だけ揺れを残す。ProcessFrame が毎フレーム入れ直すので何もしない。
        // 停止・スクラブ中は捨てて、カメラを焼き込み位置へ戻す（古いオフセットのこびり付き防止）。
        if (director == null || director.state != PlayState.Playing)
            CameraShakeMixerBehaviour.ClearPending();
    }
}
