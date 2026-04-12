using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Projectウィンドウにgitステータスアイコンを表示する。
/// Modified=黄、Added=緑、Deleted=赤、Untracked=青のドットを描画。
/// </summary>
[InitializeOnLoad]
public static class GitStatusOverlay
{
    static Dictionary<string, char> s_statusMap = new Dictionary<string, char>();
    static double s_lastRefreshTime;
    const double k_RefreshInterval = 10.0;

    static readonly Color k_ModifiedColor = new Color(1f, 0.8f, 0.2f);
    static readonly Color k_AddedColor = new Color(0.3f, 0.9f, 0.3f);
    static readonly Color k_DeletedColor = new Color(1f, 0.3f, 0.3f);
    static readonly Color k_UntrackedColor = new Color(0.4f, 0.7f, 1f);

    static GitStatusOverlay()
    {
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        EditorApplication.update += OnUpdate;
        RefreshStatus();
    }

    static void OnUpdate()
    {
        if (EditorApplication.timeSinceStartup - s_lastRefreshTime > k_RefreshInterval)
        {
            RefreshStatus();
        }
    }

    static void OnProjectWindowItemGUI(string guid, Rect rect)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return;

        // Assets/ 以下のパスに変換して検索
        char status = FindStatus(path);
        if (status == '\0') return;

        Color color = status switch
        {
            'M' => k_ModifiedColor,
            'A' => k_AddedColor,
            'D' => k_DeletedColor,
            '?' => k_UntrackedColor,
            _ => Color.gray
        };

        string tooltip = status switch
        {
            'M' => "変更あり (Modified)",
            'A' => "新規追加 (Added)",
            'D' => "削除済み (Deleted)",
            '?' => "未追跡 (Untracked)",
            _ => "不明"
        };

        // 右上にステータスドットを描画
        float size = 8f;
        var dotRect = new Rect(rect.xMax - size - 2, rect.y + 2, size, size);
        var prevColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(dotRect, Texture2D.whiteTexture, ScaleMode.ScaleToFit);
        GUI.color = prevColor;

        // ドット範囲にツールチップ
        GUI.Label(dotRect, new GUIContent("", tooltip));
    }

    static char FindStatus(string assetPath)
    {
        // Magnet_Rush/ を先頭に付けたパスで検索（gitはリポジトリルートからの相対パス）
        string gitPath = "Magnet_Rush/" + assetPath;
        if (s_statusMap.TryGetValue(gitPath, out char status))
            return status;

        // フォルダの場合、配下にステータスがあるか確認
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            string prefix = gitPath + "/";
            foreach (var kvp in s_statusMap)
            {
                if (kvp.Key.StartsWith(prefix))
                    return kvp.Value;
            }
        }

        return '\0';
    }

    static void RefreshStatus()
    {
        s_lastRefreshTime = EditorApplication.timeSinceStartup;
        s_statusMap.Clear();

        string repoRoot = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Application.dataPath, "..", ".."));

        try
        {
            var process = new Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = "status --porcelain";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WorkingDirectory = repoRoot;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0) return;

            foreach (string line in output.Split('\n'))
            {
                if (line.Length < 4) continue;
                char indexStatus = line[0];
                char workTreeStatus = line[1];
                string filePath = line.Substring(3).Trim().Trim('"');

                // 優先順位: worktree > index
                char finalStatus = workTreeStatus != ' ' ? workTreeStatus : indexStatus;
                s_statusMap[filePath] = finalStatus;
            }
        }
        catch
        {
            // git未インストールや実行失敗時は無視
        }

        EditorApplication.RepaintProjectWindow();
    }

    /// <summary>手動リフレッシュ用メニュー。</summary>
    [MenuItem("Tools/Git/Status Refresh")]
    internal static void ManualRefresh()
    {
        RefreshStatus();
    }

}

public class GitStatusLegendWindow : EditorWindow
{
    [MenuItem("Tools/Git/Status 凡例")]
    static void ShowLegend()
    {
        GetWindow<GitStatusLegendWindow>("Git Status 凡例");
    }

    void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Projectウィンドウのドット色", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        DrawLegendRow(new Color(1f, 0.8f, 0.2f), "黄", "変更あり (Modified)");
        DrawLegendRow(new Color(0.3f, 0.9f, 0.3f), "緑", "新規追加 (Added)");
        DrawLegendRow(new Color(1f, 0.3f, 0.3f), "赤", "削除済み (Deleted)");
        DrawLegendRow(new Color(0.4f, 0.7f, 1f), "青", "未追跡 (Untracked)");

        EditorGUILayout.Space(12);
        if (GUILayout.Button("リフレッシュ", GUILayout.Height(28)))
        {
            GitStatusOverlay.ManualRefresh();
        }
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("自動更新: 10秒ごと", EditorStyles.miniLabel);
    }

    static void DrawLegendRow(Color color, string label, string desc)
    {
        EditorGUILayout.BeginHorizontal();
        var rect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
        var prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
        EditorGUILayout.LabelField($"{label} — {desc}");
        EditorGUILayout.EndHorizontal();
    }
}
