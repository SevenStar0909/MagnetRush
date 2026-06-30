using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine.Playables;
using CriWare;
using CriWare.Editor;

/// <summary>
/// Edit Mode の Timeline プレビュー再生中に SoundCueClip の SE を CRI のエディタ試聴で鳴らす。
/// ランタイムは SoundCueMixerBehaviour が Sound.Play で鳴らすので、ここは Edit Mode 専用の登録役。
/// プレビュー「再生」中のみ鳴らし、スクラブ・停止では鳴らさない（誤爆防止）。
/// CameraShakeTimelinePreview と同型。ACF/ACB はランタイムの SoundManager と同じ実体を参照する。
/// </summary>
[InitializeOnLoad]
public static class SoundCueTimelinePreview
{
    private const string k_AcfPath = "Assets/_Project/Asset/Audio/MagnetRush.acf";

    // キューシート名 → ACB アセットパス（SoundManager.LoadCueSheet と同じ実体）
    private static readonly Dictionary<string, string> s_acbPaths = new()
    {
        { "SE", "Assets/_Project/Asset/Audio/SE/SE.acb" },
        { "BGM", "Assets/_Project/Asset/Audio/BGM/BGM.acb" },
    };

    private static readonly Dictionary<string, CriAtomExAcb> s_acbs = new();
    private static CriAtomEditorUtilities.PreviewPlayer s_player;
    private static bool s_acfRegistered;

    static SoundCueTimelinePreview()
    {
        SoundCueMixerBehaviour.EditorPreviewPlay = Play;
        AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        EditorApplication.playModeStateChanged += _ => Cleanup();
    }

    private static void Play(string cueSheet, string cueName)
    {
        // プレビュー「再生」中だけ鳴らす。スクラブ・停止・パークでは鳴らさない。
        var director = TimelineEditor.inspectedDirector;
        if (director == null || director.state != PlayState.Playing) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (string.IsNullOrEmpty(cueName)) return;

        // 既に初期化済みなら InitializeLibrary は false を返すので IsLibraryInitialized で確認する。
        if (!CriAtomEditorUtilities.InitializeLibrary() && !CriAtomPlugin.IsLibraryInitialized()) return;

        EnsureAcf();

        var acb = GetAcb(cueSheet);
        if (acb == null || !acb.isAvailable) return;

        s_player ??= new CriAtomEditorUtilities.PreviewPlayer();
        s_player.Play(acb, cueName);
    }

    private static void EnsureAcf()
    {
        if (s_acfRegistered) return;
        try { CriAtomEx.UnregisterAcf(); } catch { }
        try
        {
            CriAtomEx.RegisterAcf(null, Path.GetFullPath(k_AcfPath));
            s_acfRegistered = true;
        }
        catch { }
    }

    private static CriAtomExAcb GetAcb(string cueSheet)
    {
        if (s_acbs.TryGetValue(cueSheet, out var cached) && cached != null && cached.isAvailable)
            return cached;
        if (!s_acbPaths.TryGetValue(cueSheet, out var path))
            return null;

        var acb = CriAtomEditorUtilities.LoadAcbFile(null, Path.GetFullPath(path), null);
        s_acbs[cueSheet] = acb;
        return acb;
    }

    // ドメインリロード・PlayMode 切替で CRI のエディタ試聴状態を確実に片付ける（ランタイムと二重初期化させない）。
    private static void Cleanup()
    {
        if (s_player != null)
        {
            try { s_player.Stop(false); } catch { }
            s_player.Dispose();
            s_player = null;
        }

        foreach (var acb in s_acbs.Values)
            acb?.Dispose();
        s_acbs.Clear();

        if (s_acfRegistered)
        {
            try { CriAtomEx.UnregisterAcf(); } catch { }
            s_acfRegistered = false;
        }
    }
}
