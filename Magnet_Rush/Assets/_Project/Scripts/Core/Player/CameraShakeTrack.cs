using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Animator をルートに持つカメラ rig を揺らす Timeline トラック。
/// Scene 上ではトラックへ対象 Animator をバインドし、実行時は cameraIndex のrigへ自動バインドする。
/// </summary>
[TrackColor(1f, 0.55f, 0.15f)]
[TrackClipType(typeof(CameraShakeClip))]
[TrackBindingType(typeof(Animator))]
public class CameraShakeTrack : TrackAsset
{
    [Tooltip("実行時に揺らすカメラ番号（0始まり）。StabFinisherCutscene の5台目は4")]
    [LabelMin("揺らすカメラ番号（0始まり）", 0)] public int runtimeCameraIndex = 4;

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CameraShakeMixerBehaviour>.Create(graph, inputCount);
    }

    // GatherProperties は実装しない。揺れの加算先 FinisherCamera の位置は、同じカメラを駆動する
    // AnimationTrack（焼き込み済みの子オフセット）がプレビュー終了時に元へ戻す。ここで重ねて登録すると
    // 同一 EditorCurveBinding の二重登録になり Timeline プレビューが ArgumentException で壊れる。
}
