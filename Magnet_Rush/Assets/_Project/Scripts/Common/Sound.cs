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
}
