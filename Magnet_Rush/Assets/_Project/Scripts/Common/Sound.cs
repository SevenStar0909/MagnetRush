using System;
using UnityEngine;

/// <summary>
/// サウンド再生の窓口（疎結合用ファサード）。
/// 実体の再生処理は SoundManager(MagnetRush.Game) が起動時にフックを登録する。
/// これにより Game を参照しない層（Core / Bullet / Enemy）からも音を鳴らせる。
/// 依存: なし（System.Action のみ）
/// </summary>
public static class Sound
{
    /// <summary>SE 等を再生するフック。(キューシート名, キュー名)。SoundManager が登録する。</summary>
    public static Action<string, string> OnPlay;

    /// <summary>BGM を再生するフック。(キュー名)。キューシートは BGM 固定。</summary>
    public static Action<string> OnPlayBgm;

    /// <summary>BGM を停止するフック。</summary>
    public static Action OnStopBgm;

    /// <summary>3D位置で SE を再生するフック。(キューシート名, キュー名, ワールド位置, 最小距離, 最大距離)。</summary>
    public static Action<string, string, Vector3, float, float> OnPlayAt;

    /// <summary>ループSEを再生し、停止用デリゲートを返すフック。(キューシート名, キュー名) → 停止Action。SoundManager が登録する。</summary>
    public static Func<string, string, Action> OnPlayLoop;

    /// <summary>Timeline用の停止・音量変更可能SE再生フック。(キューシート名, キュー名, 開始位置秒, 初期速度倍率) → 再生ハンドル。SoundManager が登録する。</summary>
    public static Func<string, string, double, float, TimelineCuePlayback> OnPlayTimelineCue;

    public sealed class TimelineCuePlayback
    {
        private readonly Action m_stop;
        private readonly Action<float, float, float> m_setParameters;

        public TimelineCuePlayback(Action stop, Action<float, float, float> setParameters)
        {
            m_stop = stop;
            m_setParameters = setParameters;
        }

        public void SetVolumeAndPitch(float volume, float pitch) => SetParameters(volume, pitch, 1f);
        public void SetParameters(float volume, float pitch, float playbackSpeed) => m_setParameters?.Invoke(volume, pitch, playbackSpeed);
        public void Stop() => m_stop?.Invoke();
    }

    private static readonly Action s_noop = () => { };

    /// <summary>SE 等を再生する。SoundManager 未登録時（起動直後・テスト）は何もしない。</summary>
    public static void Play(string cueSheet, string cue) => OnPlay?.Invoke(cueSheet, cue);

    /// <summary>BGM を再生する。</summary>
    public static void PlayBgm(string cue) => OnPlayBgm?.Invoke(cue);

    /// <summary>BGM を停止する。</summary>
    public static void StopBgm() => OnStopBgm?.Invoke();

    /// <summary>3D位置で SE を再生する（距離減衰あり）。SoundManager 未登録時は何もしない。</summary>
    public static void PlayAt(string cueSheet, string cue, Vector3 position, float minDistance = 4f, float maxDistance = 35f)
        => OnPlayAt?.Invoke(cueSheet, cue, position, minDistance, maxDistance);

    /// <summary>
    /// ループSEを再生し、停止用デリゲートを返す。呼び出し側はこの Action を保持し、止めたいタイミングで呼ぶ。
    /// SoundManager 未登録時（起動直後・テスト）は何もしない no-op を返す。
    /// </summary>
    public static Action PlayLoop(string cueSheet, string cue)
        => OnPlayLoop?.Invoke(cueSheet, cue) ?? s_noop;

    /// <summary>
    /// Timeline用にSEを途中位置から再生し、停止・音量変更用ハンドルを返す。
    /// クリップの Clip In を startTimeSeconds に渡し、クリップ範囲を抜けたら返されたハンドルで停止する。
    /// </summary>
    public static TimelineCuePlayback PlayTimelineCue(string cueSheet, string cue, double startTimeSeconds, float initialPlaybackSpeed)
        => OnPlayTimelineCue?.Invoke(cueSheet, cue, startTimeSeconds, initialPlaybackSpeed);
}
