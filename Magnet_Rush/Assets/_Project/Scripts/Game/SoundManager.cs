using UnityEngine;
using System;
using System.Collections.Generic;
using CriWare;

/// <summary>
/// <para>サウンド管理クラス</para>
/// <para>再生の要求を受けて、CriAtomExPlayer を用いてサウンドを再生する。</para>
/// </summary>
public class SoundManager : Singleton<SoundManager>
{
    [Tooltip("音量調整データ（マスター＋キュー別倍率）。「Tools/サウンド調整シート」で編集する")]
    [SerializeField] private SoundVolumeSettings m_volumeSettings;

    private CriAtomExPlayer m_player;
    private CriAtomExPlayer m_bgmPlayer;
    private CriAtomExPlayer m_player3d;
    private CriAtomEx3dListener m_listener;
    private Camera m_listenerCamera;
    private string m_currentBgm;
    private readonly Dictionary<string, CriAtomExAcb> m_acbCache = new();
    private readonly List<ManagedLoop> m_managedLoops = new();
    // PlayAt のワンショット3D再生が使い終わったソースを回収するための一覧。
    // ソースを再生ごとに専有させないと、後発の PlayAt が共有ソースを動かして
    // 再生中の全3D SEの位置・減衰距離が飛ぶ（音がブツ切れに聞こえる主因の一つ）
    private readonly List<(CriAtomExPlayback playback, CriAtomEx3dSource source)> m_oneShot3dPlaybacks = new();

    /// <summary>
    /// 距離カリング対象のループ再生の内部状態。
    /// リスナーが最大減衰距離の外にいる間はボイスを解放し、可聴距離に戻ったら同じキューを再始動する。
    /// 聞こえないループにボイスを占有させないための仕組み（発音数はプール上限で有限）。
    /// </summary>
    private sealed class ManagedLoop
    {
        public string cueSheetName;
        public string cueName;
        public Vector3 position;
        public float minDistance;
        public float maxDistance;
        public bool stopRequested;
        public bool voiceAlive;
        public Playback3d playback;
    }

    /// <summary>
    /// <para>CriAtomExPlayerをラップした構造体</para>
    /// <para>個別操作するときに使う</para>
    /// </summary>
    public struct Playback
    {
        CriAtomExPlayer player;
        CriAtomExPlayback playback;
        // 調整シート（SoundVolumeSettings）由来の音量倍率。呼び出し側のローカル音量に掛け合わせる
        float sheetScale;

        internal Playback(CriAtomExPlayer player, CriAtomExPlayback pb, float sheetScale = 1f)
        {
            this.player = player;
            this.playback = pb;
            this.sheetScale = sheetScale;
        }

        public void SetVolumeAndPitch(float vol, float pitch)
            => SetVolumePitchAndSpeed(vol, pitch, 1f);

        public void SetVolumePitchAndSpeed(float vol, float pitch, float playbackSpeed)
        {
            // acbロード失敗時は default ハンドル（player=null）が返るため、触らず戻る
            if (this.player == null) return;
            this.player.SetVolume(vol * this.sheetScale);
            this.player.SetPitch(pitch);
            this.player.SetPlaybackRatio(Mathf.Max(0.01f, playbackSpeed));
            this.player.Update(playback);
        }

        public void Stop()
        {
            if (this.player == null) return;
            this.playback.Stop();
        }

        public bool IsPlaying()
        {
            if (this.player == null) return false;
            return this.playback.GetStatus() == CriAtomExPlayback.Status.Playing;
        }
    }

    /// <summary>
    /// <para>3D再生（距離減衰あり）のハンドル。専用の CriAtomEx3dSource を持つ。</para>
    /// <para>移動する音源を SetPosition で追従させ、Stop で再生停止と同時に専用ソースを破棄する。</para>
    /// </summary>
    public struct Playback3d
    {
        CriAtomExPlayer player;
        CriAtomExPlayback playback;
        CriAtomEx3dSource source;
        // 調整シート（SoundVolumeSettings）由来の音量倍率。呼び出し側のローカル音量に掛け合わせる
        float sheetScale;

        internal Playback3d(CriAtomExPlayer player, CriAtomEx3dSource source, CriAtomExPlayback pb, float sheetScale = 1f)
        {
            this.player = player;
            this.source = source;
            this.playback = pb;
            this.sheetScale = sheetScale;
        }

        /// <summary>発音位置を更新する。移動する音源を毎フレーム追従させる用途。</summary>
        public void SetPosition(Vector3 position)
        {
            if (source == null) return;
            source.SetPosition(position.x, position.y, position.z);
            source.Update();
        }

        public void SetVolumeAndPitch(float vol, float pitch)
        {
            if (player == null || source == null) return;

            // m_player3d は全 3D 再生で共有なので、Update 前に自分のソースへ再バインドして取り違えを防ぐ
            player.Set3dSource(source);
            player.SetVolume(vol * sheetScale);
            player.SetPitch(pitch);
            player.Update(playback);
        }

        public bool IsPlaying()
        {
            return source != null && playback.GetStatus() == CriAtomExPlayback.Status.Playing;
        }

        /// <summary>再生を停止し、専用の3Dソースを破棄する。</summary>
        public void Stop()
        {
            if (source == null) return;
            playback.Stop();
            source.Dispose();
            source = null;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        // 重複インスタンス（シーンリロードで生成される）は破棄されるので、フック登録もリソース生成もしない
        if (Instance != this) return;

        m_player = new CriAtomExPlayer();
        m_bgmPlayer = new CriAtomExPlayer();
        // SE多発で発音数上限に達すると後着優先でBGMのボイスが奪われ、BGMが途中で止まる（実測でSE15連発×3回で死亡）。
        // BGMを最優先にしてSEに奪わせない
        m_bgmPlayer.SetVoicePriority(255);

        // 3D再生用。リスナー(カメラ)を player に紐付けて距離減衰させる。
        // ソース(発音位置)は再生ごとに専有インスタンスを作る（共有すると後発の再生が
        // 再生中の全3D SEの位置を動かしてしまう）
        m_player3d = new CriAtomExPlayer();
        m_listener = new CriAtomEx3dListener();
        m_player3d.Set3dListener(m_listener);
        // acb のキューは全て Pan3d（2Dパン）でオーサリングされているため、3D経路のプレーヤ側で
        // 3Dポジショニングを強制する。これが無いと SetMinMaxDistance が無視され距離減衰が一切効かない
        m_player3d.SetPanType(CriAtomEx.PanType.Pos3d);

        // 疎結合ファサードに自分の再生処理を登録（Game を参照しない層からも Sound.Play で鳴らせる）
        Sound.OnPlay = Play;
        Sound.OnPlayBgm = PlayBgm;
        Sound.OnStopBgm = StopBgm;
        Sound.OnPlayAt = PlayAt;
        Sound.OnPlayLoop = PlayLoop;
        Sound.OnPlayLoopAt = PlayLoopAt;
        Sound.OnPlayTimelineCue = PlayTimelineCue;

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (m_volumeSettings == null)
            Debug.LogWarning("[SoundManager] SoundVolumeSettings 未アサイン。全音量が等倍で再生されます。");

        // ACF（同時発音数上限・AISAC等のグローバル設定）を最初に登録する。未登録だと発音制御が一切効かない
        RegisterAcf();

        // 初回再生時の警告とロード待ちを避けるため、使うキューシートを先読みしておく
        LoadCueSheet(SoundData.CueSheet.SE);
        LoadCueSheet(SoundData.CueSheet.BGM);
    }

    /// <summary>
    /// ACF を CRI に登録する。acb と同じく、エディタでは Audio フォルダのフルパス、
    /// ビルドでは StreamingAssets 直下から読む。ファイルが無ければ警告して続行する（音は鳴るが発音制御なし）。
    /// </summary>
    private static void RegisterAcf()
    {
#if UNITY_EDITOR
        string path = System.IO.Path.GetFullPath("Assets/_Project/Asset/Audio/MagnetRush.acf");
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"[SoundManager] ACF が見つかりません: {path}");
            return;
        }
#else
        // 生の RegisterAcf は相対パスを StreamingAssets 基準で解決しないため、明示的に前置する
        string path = System.IO.Path.Combine(CriWare.Common.streamingAssetsPath, "MagnetRush.acf");
#endif
        CriAtomEx.RegisterAcf(null, path);
    }

    private void LateUpdate()
    {
        // 再生が終わったワンショット3D再生の専有ソースを回収する
        for (int i = m_oneShot3dPlaybacks.Count - 1; i >= 0; i--)
        {
            if (m_oneShot3dPlaybacks[i].playback.GetStatus() != CriAtomExPlayback.Status.Removed) continue;
            m_oneShot3dPlaybacks[i].source.Dispose();
            m_oneShot3dPlaybacks.RemoveAt(i);
        }

        // 3D音の聴取点をメインカメラに追従させる
        if (m_listener == null) return;
        if (m_listenerCamera == null) m_listenerCamera = Camera.main;
        if (m_listenerCamera == null) return;

        var t = m_listenerCamera.transform;
        Vector3 p = t.position;
        Vector3 f = t.forward;
        Vector3 u = t.up;
        m_listener.SetPosition(p.x, p.y, p.z);
        m_listener.SetOrientation(f.x, f.y, f.z, u.x, u.y, u.z);
        m_listener.Update();

        // 聞こえない距離のループにボイスを占有させない（プール上限の奪い合いで他の音が途切れる対策）
        for (int i = 0; i < m_managedLoops.Count; i++)
            UpdateLoopVoiceByDistance(m_managedLoops[i], p);
    }

    /// <summary>
    /// ループのボイスを距離で始動/停止する。停止側の閾値を広げたヒステリシスで境界のバタつきを防ぐ。
    /// </summary>
    private void UpdateLoopVoiceByDistance(ManagedLoop loop, Vector3 listenerPosition)
    {
        const float stopMargin = 1.15f;
        if (loop.stopRequested) return;

        float dist = Vector3.Distance(listenerPosition, loop.position);
        if (!loop.voiceAlive && dist <= loop.maxDistance)
        {
            loop.playback = PlayAtWithHandle(loop.cueSheetName, loop.cueName, loop.position, loop.minDistance, loop.maxDistance);
            loop.voiceAlive = true;
        }
        else if (loop.voiceAlive && dist > loop.maxDistance * stopMargin)
        {
            loop.playback.Stop();
            loop.voiceAlive = false;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // 自分が登録したフックだけを解除（重複インスタンス破棄で本物のフックを消さないため）
        if (Sound.OnPlay == Play) Sound.OnPlay = null;
        if (Sound.OnPlayBgm == PlayBgm) Sound.OnPlayBgm = null;
        if (Sound.OnStopBgm == StopBgm) Sound.OnStopBgm = null;
        if (Sound.OnPlayAt == PlayAt) Sound.OnPlayAt = null;
        if (Sound.OnPlayLoop == PlayLoop) Sound.OnPlayLoop = null;
        if (Sound.OnPlayLoopAt == PlayLoopAt) Sound.OnPlayLoopAt = null;
        if (Sound.OnPlayTimelineCue == PlayTimelineCue) Sound.OnPlayTimelineCue = null;

        m_managedLoops.Clear();
        foreach (var (_, source) in m_oneShot3dPlaybacks) source?.Dispose();
        m_oneShot3dPlaybacks.Clear();
        m_player?.Dispose();
        m_bgmPlayer?.Dispose();
        m_player3d?.Dispose();
        m_listener?.Dispose();
        foreach (var acb in m_acbCache.Values) acb?.Dispose();
        m_acbCache.Clear();
    }

    public void LoadCueSheet(string name)
    {
        if (m_acbCache.ContainsKey(name)) return;
        m_acbCache[name] = CriAtomExAcb.LoadAcbFile(null, ResolveAcbPath(name), "");
        if (m_acbCache[name] == null)
            Debug.LogWarning($"[SoundManager] '{name}' の acb を読み込めませんでした: {ResolveAcbPath(name)}");
    }

    /// <summary>
    /// acb ファイルの読み込みパスを返す。
    /// 本プロジェクトの acb は Assets/_Project/Asset/Audio/&lt;キューシート名&gt;/ にある（サウンド担当の出力先）ため、
    /// エディタではフルパスへ変換して読む。ビルドでは AudioBuildPreprocessor が StreamingAssets 直下へコピーしたものを読む。
    /// 生の LoadAcbFile は相対パスを StreamingAssets 基準で解決しないため、明示的に前置する必要がある。
    /// </summary>
    private static string ResolveAcbPath(string name)
    {
#if UNITY_EDITOR
        return System.IO.Path.GetFullPath($"Assets/_Project/Asset/Audio/{name}/{name}.acb");
#else
        return System.IO.Path.Combine(CriWare.Common.streamingAssetsPath, name + ".acb");
#endif
    }

    public void UnloadCueSheet(string name)
    {
        if (!m_acbCache.TryGetValue(name, out var acb)) return;
        // ロード失敗時は null がキャッシュされているため null 条件付きで解放する
        acb?.Dispose();
        m_acbCache.Remove(name);
    }

    /// <summary>
    /// ハンドルを受け取らない再生
    /// </summary>
    public void Play(string cueSheetName, string cueName)
    {
        var acb = GetAcb(cueSheetName);
        if (acb == null) { ChannelLogger.LogGuardReturn("Sound", $"acb未ロードのため {cueName} を再生できない"); return; }
        // Playback.SetVolumePitchAndSpeed が共有プレーヤにピッチを残すため、単発再生の前に基準へ戻す
        m_player.SetPitch(0f);
        m_player.SetPlaybackRatio(1f);
        m_player.SetVolume(GetSeVolume(cueName));
        m_player.SetCue(acb, cueName);
        m_player.Start();
    }

    /// <summary>
    /// <para>ハンドルを受け取る再生</para>
    /// <para>個別操作する場合はこちらを使う</para>
    /// </summary>
    public Playback PlayWithHandle(string cueSheetName, string cueName)
    {
        var acb = GetAcb(cueSheetName);
        if (acb == null) { ChannelLogger.LogGuardReturn("Sound", $"acb未ロードのため {cueName} を再生できない"); return default; }
        m_player.SetPitch(0f);
        m_player.SetPlaybackRatio(1f);
        m_player.SetVolume(GetSeVolume(cueName));
        m_player.SetCue(acb, cueName);
        Playback pb = new(m_player, m_player.Start(), GetSeVolume(cueName));
        return pb;
    }

    /// <summary>
    /// ループSEを再生し、停止用デリゲートを返す。呼び出し側はこの Action を保持し、止めたいタイミングで呼ぶ。
    /// Game を参照しない層に Playback 型を晒さず「止められる音」を渡すための窓口（Sound.PlayLoop 経由）。
    /// </summary>
    public Action PlayLoop(string cueSheetName, string cueName)
    {
        Playback pb = PlayWithHandle(cueSheetName, cueName);
        bool stopped = false;
        return () =>
        {
            if (stopped) return;
            stopped = true;
            pb.Stop();
        };
    }

    /// <summary>
    /// Timeline の SoundCueClip 用。Clip In を CRI の開始位置に反映し、クリップ終了時に停止・カーブ更新できるハンドルを返す。
    /// </summary>
    public Sound.TimelineCuePlayback PlayTimelineCue(string cueSheetName, string cueName, double startTimeSeconds, float initialPlaybackSpeed)
    {
        var acb = GetAcb(cueSheetName);
        if (acb == null)
        {
            ChannelLogger.LogGuardReturn("Sound", $"acb未ロードのため Timeline キュー {cueName} を再生できない");
            return new Sound.TimelineCuePlayback(() => { }, (v, p, s) => { });
        }

        long startTimeMs = Math.Max(0L, (long)Math.Round(startTimeSeconds * 1000.0));

        m_player.SetPitch(0f);
        m_player.SetStartTime(startTimeMs);
        m_player.SetPlaybackRatio(Mathf.Max(0.01f, initialPlaybackSpeed));
        // Timeline のカーブが毎フレーム絶対値で音量を上書きするため、シート倍率はハンドル側に焼き込んで掛け算にする
        m_player.SetVolume(GetSeVolume(cueName));
        m_player.SetCue(acb, cueName);
        Playback pb = new(m_player, m_player.Start(), GetSeVolume(cueName));
        m_player.SetStartTime(0);

        bool stopped = false;
        void Stop()
        {
            if (stopped) return;
            stopped = true;
            pb.Stop();
        }

        void SetParameters(float volume, float pitch, float playbackSpeed)
        {
            if (stopped) return;
            pb.SetVolumePitchAndSpeed(volume, pitch, playbackSpeed);
        }

        return new Sound.TimelineCuePlayback(Stop, SetParameters);
    }

    /// <summary>
    /// BGM を再生する。すでに同じ BGM が鳴っていれば何もしない（シーン再入での鳴り直し防止）。
    /// </summary>
    public void PlayBgm(string cueName)
    {
        if (m_bgmPlayer == null) return;
        if (m_currentBgm == cueName && m_bgmPlayer.GetStatus() == CriAtomExPlayer.Status.Playing) return;

        var acb = GetAcb(SoundData.CueSheet.BGM);
        if (acb == null) { Debug.LogWarning("[SoundManager] BGM の acb を取得できません。"); return; }

        m_bgmPlayer.Stop();
        m_bgmPlayer.SetVolume(GetBgmVolume(cueName));
        m_bgmPlayer.SetCue(acb, cueName);
        m_bgmPlayer.Start();
        m_currentBgm = cueName;
    }

    /// <summary>BGM を停止する。</summary>
    public void StopBgm()
    {
        m_bgmPlayer?.Stop();
        m_currentBgm = null;
    }

    /// <summary>
    /// 3D位置で SE を再生する（距離減衰あり）。リスナー(カメラ)から遠いほど小さく鳴る。
    /// 距離が負値なら調整シート（SoundVolumeSettings）のキュー別減衰距離を使う。
    /// </summary>
    public void PlayAt(string cueSheetName, string cueName, Vector3 position, float minDistance, float maxDistance)
    {
        if (m_player3d == null) return;

        var acb = GetAcb(cueSheetName);
        if (acb == null) return;

        if (minDistance < 0f || maxDistance < 0f)
        {
            Vector2 d = GetCueDistance(cueName);
            minDistance = d.x;
            maxDistance = d.y;
        }

        // 再生ごとに専有ソースを作り、再生終了後に LateUpdate で回収する
        var source = new CriAtomEx3dSource();
        source.SetMinMaxDistance(minDistance, maxDistance);
        source.SetPosition(position.x, position.y, position.z);
        source.Update();

        m_player3d.Set3dSource(source);
        m_player3d.SetVolume(GetSeVolume(cueName));
        m_player3d.SetCue(acb, cueName);
        m_oneShot3dPlaybacks.Add((m_player3d.Start(), source));
    }

    /// <summary>
    /// 3D位置で SE をハンドル付きで再生する（距離減衰あり）。専用ソースを持つので移動・停止できる。
    /// ループするキューを移動する敵に追従させる等に使う。Playback3d.SetPosition で位置更新、Stop で停止＋破棄。
    /// </summary>
    public Playback3d PlayAtWithHandle(string cueSheetName, string cueName, Vector3 position, float minDistance, float maxDistance)
    {
        if (m_player3d == null) return default;

        var acb = GetAcb(cueSheetName);
        if (acb == null) return default;

        var source = new CriAtomEx3dSource();
        source.SetMinMaxDistance(minDistance, maxDistance);
        source.SetPosition(position.x, position.y, position.z);
        source.Update();

        m_player3d.Set3dSource(source);
        m_player3d.SetVolume(GetSeVolume(cueName));
        m_player3d.SetCue(acb, cueName);
        CriAtomExPlayback pb = m_player3d.Start();
        return new Playback3d(m_player3d, source, pb, GetSeVolume(cueName));
    }

    // ── カテゴリ操作 ──────────────────────────────────────────────

    public void SetCategoryVolume(string categoryName, float vol)
        => CriAtomExCategory.SetVolume(categoryName, vol);

    public void PauseCategory(string categoryName)
        => CriAtomExCategory.Pause(categoryName, true);

    public void ResumeCategory(string categoryName)
        => CriAtomExCategory.Pause(categoryName, false);

    public void StopCategory(string categoryName)
        => CriAtomExCategory.Stop(categoryName);

    /// <summary>
    /// 3D位置でループSEを再生し、位置更新・停止用ハンドルを返す（Sound.PlayLoopAt 経由）。
    /// 減衰距離は調整シートのキュー別設定を使う。
    /// </summary>
    public Sound.PositionalLoopPlayback PlayLoopAt(string cueSheetName, string cueName, Vector3 position)
    {
        Vector2 d = GetCueDistance(cueName);
        return PlayLoopAt(cueSheetName, cueName, position, d.x, d.y);
    }

    /// <summary>
    /// 3D位置でループSEを距離カリング付きで再生する。リスナーが最大減衰距離の外にいる間は
    /// ボイスを解放し、可聴距離に戻ったら再始動する（判定は LateUpdate）。
    /// </summary>
    public Sound.PositionalLoopPlayback PlayLoopAt(string cueSheetName, string cueName, Vector3 position, float minDistance, float maxDistance)
    {
        var loop = new ManagedLoop
        {
            cueSheetName = cueSheetName,
            cueName = cueName,
            position = position,
            minDistance = minDistance,
            maxDistance = maxDistance,
        };
        // リスナー位置が取れる前に鳴らし始めるケースは従来挙動（即時再生）に合わせる
        if (m_listenerCamera != null)
            UpdateLoopVoiceByDistance(loop, m_listenerCamera.transform.position);
        else
        {
            loop.playback = PlayAtWithHandle(cueSheetName, cueName, position, minDistance, maxDistance);
            loop.voiceAlive = true;
        }
        m_managedLoops.Add(loop);

        return new Sound.PositionalLoopPlayback(
            () =>
            {
                if (loop.stopRequested) return;
                loop.stopRequested = true;
                if (loop.voiceAlive)
                {
                    loop.playback.Stop();
                    loop.voiceAlive = false;
                }
                m_managedLoops.Remove(loop);
            },
            p =>
            {
                if (loop.stopRequested) return;
                loop.position = p;
                if (loop.voiceAlive) loop.playback.SetPosition(p);
            });
    }

    // 音量調整データから最終音量（マスター × キュー別倍率）を引く。未アサイン時は等倍（Start で警告済み）
    private float GetSeVolume(string cueName) => m_volumeSettings != null ? m_volumeSettings.GetSeVolume(cueName) : 1f;
    private float GetBgmVolume(string cueName) => m_volumeSettings != null ? m_volumeSettings.GetBgmVolume(cueName) : 1f;

    // 3D減衰距離を調整シートから引く。未アサイン時は既定の (4, 35)
    private Vector2 GetCueDistance(string cueName) => m_volumeSettings != null ? m_volumeSettings.GetCueDistance(cueName) : new Vector2(4f, 35f);

    private CriAtomExAcb GetAcb(string name)
    {
        if (!m_acbCache.TryGetValue(name, out var acb))
        {
            Debug.LogWarning($"[SoundManager] '{name}' が事前ロードされていません。");
            LoadCueSheet(name);
            acb = m_acbCache[name];
        }
        return acb;
    }
}
