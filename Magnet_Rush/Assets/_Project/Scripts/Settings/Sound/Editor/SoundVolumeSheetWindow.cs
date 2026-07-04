using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CriWare;
using CriWare.Editor;

/// <summary>
/// プランナー用のサウンド音量調整シート。
/// SoundVolumeSettings（1音1項目・日本語ラベル）をそのまま一覧表示し、各行の ▶ でその場で試聴できる。
/// 試聴はゲームと同じ最終音量（全体 × 各音）で鳴る。Inspectorで直接編集しても同じ。
/// CRI のエディタ試聴は SoundCueTimelinePreview と同型（ACF/ACB はランタイムと同じ実体をフルパスで読む）。
/// メニュー: Tools/サウンド調整シート
/// </summary>
public class SoundVolumeSheetWindow : EditorWindow
{
    private const string k_AcfPath = "Assets/_Project/Asset/Audio/MagnetRush.acf";

    // キューシート名 → ACB アセットパス（SoundManager.ResolveAcbPath と同じ実体）
    private static readonly Dictionary<string, string> s_acbPaths = new()
    {
        { SoundData.CueSheet.SE, "Assets/_Project/Asset/Audio/SE/SE.acb" },
        { SoundData.CueSheet.BGM, "Assets/_Project/Asset/Audio/BGM/BGM.acb" },
    };

    // フィールド名 → 試聴するキュー。SoundVolumeSettings にフィールドを足したらここにも1行足すと ▶ が付く
    private static readonly Dictionary<string, (string sheet, string cue)> s_previewCues = new()
    {
        { "playerShot", (SoundData.CueSheet.SE, SoundData.SE.PlayerShot) },
        { "selfShot", (SoundData.CueSheet.SE, SoundData.SE.SelfShot) },
        { "aim", (SoundData.CueSheet.SE, SoundData.SE.Aim) },
        { "reload", (SoundData.CueSheet.SE, SoundData.SE.Reload) },
        { "magChange", (SoundData.CueSheet.SE, SoundData.SE.MagChange) },
        { "playerFoot", (SoundData.CueSheet.SE, SoundData.SE.PlayerFoot01) },
        { "stab", (SoundData.CueSheet.SE, SoundData.SE.Stab) },
        { "magField", (SoundData.CueSheet.SE, SoundData.SE.MagField) },
        { "hitObject", (SoundData.CueSheet.SE, SoundData.SE.HitObject) },
        { "turretShot", (SoundData.CueSheet.SE, SoundData.SE.TurretShot) },
        { "turretImpact", (SoundData.CueSheet.SE, SoundData.SE.TurretImpact) },
        { "dysonEnemy", (SoundData.CueSheet.SE, SoundData.SE.DysonEnemy) },
        { "droneEnemy", (SoundData.CueSheet.SE, SoundData.SE.DroneEnemy) },
        { "enemyWheel", (SoundData.CueSheet.SE, SoundData.SE.EnemyWheel) },
        { "missileShot", (SoundData.CueSheet.SE, SoundData.SE.MissileShot) },
        { "missileImpact", (SoundData.CueSheet.SE, SoundData.SE.MissileImpact) },
        { "bossEnemySlam", (SoundData.CueSheet.SE, SoundData.SE.BossEnemySlam) },
        { "bossEnemyFoot", (SoundData.CueSheet.SE, SoundData.SE.BossEnemyFoot) },
        { "button", (SoundData.CueSheet.SE, SoundData.SE.Button) },
        { "select", (SoundData.CueSheet.SE, SoundData.SE.Select) },
        { "stageSelect", (SoundData.CueSheet.SE, SoundData.SE.StageSelect) },
        { "bossBattleBgm", (SoundData.CueSheet.BGM, SoundData.BGM.BossBattle) },
        { "titleBgm", (SoundData.CueSheet.BGM, SoundData.BGM.Title) },
        { "stageSelectBgm", (SoundData.CueSheet.BGM, SoundData.BGM.StageSelect) },
        { "tutorialBgm", (SoundData.CueSheet.BGM, SoundData.BGM.Tutorial) },
        { "stage01Bgm", (SoundData.CueSheet.BGM, SoundData.BGM.Stage01) },
    };

    private static readonly Dictionary<string, CriAtomExAcb> s_acbs = new();
    private static CriAtomEditorUtilities.PreviewPlayer s_player;
    private static bool s_acfRegistered;

    private SoundVolumeSettings m_settings;
    private Vector2 m_scroll;

    [MenuItem("Tools/サウンド調整シート")]
    public static void Open()
    {
        var window = GetWindow<SoundVolumeSheetWindow>("サウンド調整シート");
        window.minSize = new Vector2(440f, 320f);
    }

    private void OnEnable()
    {
        if (m_settings == null)
            m_settings = FindSettingsAsset();

        AssemblyReloadEvents.beforeAssemblyReload += CleanupPreview;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= CleanupPreview;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        CleanupPreview();
    }

    private void OnGUI()
    {
        m_settings = (SoundVolumeSettings)EditorGUILayout.ObjectField("調整データ", m_settings, typeof(SoundVolumeSettings), false);
        if (m_settings == null)
        {
            EditorGUILayout.HelpBox(
                "SoundVolumeSettings アセットが見つかりません。\nProject右クリック → Create → MagnetRush → SoundVolumeSettings で作成してください。",
                MessageType.Warning);
            return;
        }

        bool isPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;
        if (isPlayMode)
            EditorGUILayout.HelpBox("PlayMode 中は ▶ 試聴を使えません。スライダーの変更はゲーム内の次の再生から反映されます。", MessageType.Info);

        var serialized = new SerializedObject(m_settings);
        serialized.Update();

        m_scroll = EditorGUILayout.BeginScrollView(m_scroll);

        // SO のフィールドを宣言順に描画する。[Header]/[LabelRange] は各 Drawer がそのまま日本語で描く。
        // 試聴対象のフィールドには行末に ▶ を付ける
        var prop = serialized.GetIterator();
        prop.NextVisible(true); // m_Script はスキップ
        while (prop.NextVisible(false))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(prop, true);

                if (s_previewCues.TryGetValue(prop.name, out var cueRef))
                {
                    using (new EditorGUI.DisabledScope(isPlayMode))
                    {
                        if (GUILayout.Button("▶", GUILayout.Width(28f)))
                        {
                            // 押した瞬間の編集値で鳴らすため、先に反映してから最終音量を計算する
                            serialized.ApplyModifiedProperties();
                            float volume = cueRef.sheet == SoundData.CueSheet.BGM
                                ? m_settings.GetBgmVolume(cueRef.cue)
                                : m_settings.GetSeVolume(cueRef.cue);
                            PlayPreview(cueRef.sheet, cueRef.cue, volume);
                        }
                    }
                }
            }
        }

        EditorGUILayout.EndScrollView();

        if (serialized.ApplyModifiedProperties())
            EditorUtility.SetDirty(m_settings);

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("■ 試聴停止"))
                StopPreview();
            if (GUILayout.Button("保存"))
                AssetDatabase.SaveAssets();
        }
    }

    private static SoundVolumeSettings FindSettingsAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:SoundVolumeSettings");
        if (guids.Length == 0)
            return null;
        return AssetDatabase.LoadAssetAtPath<SoundVolumeSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void PlayPreview(string cueSheet, string cueName, float volume)
    {
        if (!CriAtomEditorUtilities.InitializeLibrary() && !CriAtomPlugin.IsLibraryInitialized())
            return;

        EnsureAcf();

        var acb = GetPreviewAcb(cueSheet);
        if (acb == null || !acb.isAvailable)
            return;

        s_player ??= new CriAtomEditorUtilities.PreviewPlayer();
        s_player.player?.SetVolume(Mathf.Max(0f, volume));
        s_player.Play(acb, cueName);
    }

    private static void StopPreview()
    {
        try { s_player?.Stop(false); } catch { }
    }

    private static void EnsureAcf()
    {
        if (s_acfRegistered)
            return;

        try { CriAtomEx.UnregisterAcf(); } catch { }
        try
        {
            CriAtomEx.RegisterAcf(null, Path.GetFullPath(k_AcfPath));
            s_acfRegistered = true;
        }
        catch { }
    }

    private static CriAtomExAcb GetPreviewAcb(string cueSheet)
    {
        if (s_acbs.TryGetValue(cueSheet, out var cached) && cached != null && cached.isAvailable)
            return cached;
        if (!s_acbPaths.TryGetValue(cueSheet, out var path))
            return null;

        var acb = CriAtomEditorUtilities.LoadAcbFile(null, Path.GetFullPath(path), null);
        s_acbs[cueSheet] = acb;
        return acb;
    }

    private void OnPlayModeChanged(PlayModeStateChange _) => CleanupPreview();

    // ドメインリロード・PlayMode 切替・ウィンドウ破棄で CRI のエディタ試聴状態を片付ける（ランタイムと二重初期化させない）
    private static void CleanupPreview()
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
