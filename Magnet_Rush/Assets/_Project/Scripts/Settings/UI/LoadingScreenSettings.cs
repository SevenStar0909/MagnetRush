using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "LoadingScreenSettings", menuName = "MagnetRush/LoadingScreenSettings")]
[ClassLabelSO("ロード画面設定")]
public class LoadingScreenSettings : ScriptableObject
{
    [Header("[背景プール]")]
    [Label("背景画像リスト")]
    [Tooltip("ロード画面の背景画像。ここに追加するだけで表示候補に入る（動画と合わせてランダムに1つ表示）")]
    public Sprite[] backgroundSprites;

    [Label("背景動画リスト")]
    [Tooltip("ロード画面の背景動画。mp4をプロジェクトに入れてここに追加するだけで表示候補に入る")]
    public VideoClip[] backgroundVideos;

    [Header("[表示時間]")]
    [Label("最低表示時間（秒）")]
    [Tooltip("ロードが一瞬で終わってもこの秒数はロード画面を見せる")]
    public float minDisplayTime = 1.5f;

    [Label("フェード時間（秒）")]
    [Tooltip("ロード画面が現れる/消えるときのフェードの秒数")]
    public float fadeDuration = 0.3f;

    [Header("[動画再生]")]
    [Label("動画を最後まで再生してから閉じる")]
    [Tooltip("オンにすると、ロードが先に終わってもロード画面の動画が最後まで再生されてからシーンに切り替わる")]
    public bool waitForVideoEnd = true;

    [Label("動画の音を鳴らす")]
    [Tooltip("オンにするとロード動画の音声が再生される")]
    public bool playVideoAudio = false;

    [Label("動画をループ再生")]
    [Tooltip("ロードが長引いたとき動画を繰り返し再生する")]
    public bool loopVideo = true;
}
