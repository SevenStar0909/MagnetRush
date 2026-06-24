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
    private CriAtomExPlayer m_player;
    private CriAtomExPlayer m_bgmPlayer;
    private CriAtomExPlayer m_player3d;
    private CriAtomEx3dListener m_listener;
    private CriAtomEx3dSource m_source3d;
    private Camera m_listenerCamera;
    private string m_currentBgm;
    private readonly Dictionary<string, CriAtomExAcb> m_acbCache = new();

    /// <summary>
    /// <para>CriAtomExPlayerをラップした構造体</para>
    /// <para>個別操作するときに使う</para>
    /// </summary>
    public struct Playback
    {
        CriAtomExPlayer player;
        CriAtomExPlayback playback;

        internal Playback(CriAtomExPlayer player, CriAtomExPlayback pb)
        {
            this.player = player;
            this.playback = pb;
        }

        public void SetVolumeAndPitch(float vol, float pitch)
        {
            this.player.SetVolume(vol);
            this.player.SetPitch(pitch);
            this.player.Update(playback);
        }

        public void Stop()
        {
            this.playback.Stop();
        }

        public bool IsPlaying()
        {
            return this.playback.GetStatus() == CriAtomExPlayback.Status.Playing;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        // 重複インスタンス（シーンリロードで生成される）は破棄されるので、フック登録もリソース生成もしない
        if (Instance != this) return;

        m_player = new CriAtomExPlayer();
        m_bgmPlayer = new CriAtomExPlayer();

        // 3D再生用。リスナー(カメラ)とソース(発音位置)を player に紐付けて距離減衰させる
        m_player3d = new CriAtomExPlayer();
        m_listener = new CriAtomEx3dListener();
        m_source3d = new CriAtomEx3dSource();
        m_player3d.Set3dListener(m_listener);
        m_player3d.Set3dSource(m_source3d);

        // 疎結合ファサードに自分の再生処理を登録（Game を参照しない層からも Sound.Play で鳴らせる）
        Sound.OnPlay = Play;
        Sound.OnPlayBgm = PlayBgm;
        Sound.OnStopBgm = StopBgm;
        Sound.OnPlayAt = PlayAt;

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 初回再生時の警告とロード待ちを避けるため、使うキューシートを先読みしておく
        LoadCueSheet(SoundData.CueSheet.SE);
        LoadCueSheet(SoundData.CueSheet.BGM);
    }

    private void LateUpdate()
    {
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
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // 自分が登録したフックだけを解除（重複インスタンス破棄で本物のフックを消さないため）
        if (Sound.OnPlay == Play) Sound.OnPlay = null;
        if (Sound.OnPlayBgm == PlayBgm) Sound.OnPlayBgm = null;
        if (Sound.OnStopBgm == StopBgm) Sound.OnStopBgm = null;
        if (Sound.OnPlayAt == PlayAt) Sound.OnPlayAt = null;

        m_player?.Dispose();
        m_bgmPlayer?.Dispose();
        m_player3d?.Dispose();
        m_source3d?.Dispose();
        m_listener?.Dispose();
        foreach (var acb in m_acbCache.Values) acb?.Dispose();
        m_acbCache.Clear();
    }

    public void LoadCueSheet(string name)
    {
        if (m_acbCache.ContainsKey(name)) return;
        m_acbCache[name] = CriAtomExAcb.LoadAcbFile(null, name + ".acb", "");
    }

    public void UnloadCueSheet(string name)
    {
        if (!m_acbCache.TryGetValue(name, out var acb)) return;
        acb.Dispose();
        m_acbCache.Remove(name);
    }

    /// <summary>
    /// ハンドルを受け取らない再生
    /// </summary>
    public void Play(string cueSheetName, string cueName)
    {
        m_player.SetCue(GetAcb(cueSheetName), cueName);
        m_player.Start();
    }

    /// <summary>
    /// <para>ハンドルを受け取る再生</para>
    /// <para>個別操作する場合はこちらを使う</para>
    /// </summary>
    public Playback PlayWithHandle(string cueSheetName, string cueName)
    {
        m_player.SetCue(GetAcb(cueSheetName), cueName);
        Playback pb = new(m_player, m_player.Start());
        return pb;
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
    /// </summary>
    public void PlayAt(string cueSheetName, string cueName, Vector3 position, float minDistance, float maxDistance)
    {
        if (m_player3d == null) return;

        var acb = GetAcb(cueSheetName);
        if (acb == null) return;

        m_source3d.SetMinMaxDistance(minDistance, maxDistance);
        m_source3d.SetPosition(position.x, position.y, position.z);
        m_source3d.Update();

        m_player3d.SetCue(acb, cueName);
        m_player3d.Start();
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