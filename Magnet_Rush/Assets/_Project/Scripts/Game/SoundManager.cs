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
    private readonly Dictionary<string, CriAtomExAcb> m_acbCache = new();

    protected override void Awake()
    {
        base.Awake();
        m_player = new CriAtomExPlayer();
        DontDestroyOnLoad(gameObject);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        m_player?.Dispose();
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
    public CriAtomExPlayback PlayWithHandle(string cueSheetName, string cueName)
    {
        m_player.SetCue(GetAcb(cueSheetName), cueName);
        return m_player.Start();
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