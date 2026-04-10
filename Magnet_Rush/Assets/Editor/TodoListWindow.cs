using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// コード内のTODO/FIXME/HACKコメントを自動収集して一覧表示する。
/// メニュー: Tools/TODO List
/// </summary>
public class TodoListWindow : EditorWindow
{
    private Vector2 m_scrollPos;
    private List<TodoEntry> m_entries = new List<TodoEntry>();
    private string m_filterType = "ALL";
    private string m_searchText = "";

    // TODO/FIXME/HACK のパターン（コメントガイドライン準拠: 大文字+コロン）
    static readonly Regex k_TodoPattern = new Regex(
        @"//\s*(TODO|FIXME|HACK):\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly string[] k_FilterOptions = { "ALL", "TODO", "FIXME", "HACK" };
    static readonly Dictionary<string, Color> k_Colors = new Dictionary<string, Color>
    {
        { "TODO", new Color(0.3f, 0.7f, 1f) },
        { "FIXME", new Color(1f, 0.4f, 0.4f) },
        { "HACK", new Color(1f, 0.8f, 0.3f) }
    };

    struct TodoEntry
    {
        public string filePath;
        public int lineNumber;
        public string type;
        public string message;
        public string fileName;
    }

    [MenuItem("Tools/TODO List")]
    static void ShowWindow()
    {
        GetWindow<TodoListWindow>("TODO List");
    }

    void OnEnable()
    {
        ScanAll();
    }

    void OnGUI()
    {
        DrawToolbar();
        DrawStats();
        DrawEntries();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("再スキャン", EditorStyles.toolbarButton, GUILayout.Width(70)))
            ScanAll();

        GUILayout.Space(8);
        EditorGUILayout.LabelField("フィルタ:", GUILayout.Width(45));
        foreach (string option in k_FilterOptions)
        {
            bool selected = m_filterType == option;
            if (GUILayout.Toggle(selected, option, EditorStyles.toolbarButton, GUILayout.Width(50)) && !selected)
                m_filterType = option;
        }

        GUILayout.FlexibleSpace();
        m_searchText = EditorGUILayout.TextField(m_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();
    }

    void DrawStats()
    {
        int todoCount = m_entries.Count(e => e.type == "TODO");
        int fixmeCount = m_entries.Count(e => e.type == "FIXME");
        int hackCount = m_entries.Count(e => e.type == "HACK");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"合計: {m_entries.Count}", EditorStyles.boldLabel, GUILayout.Width(80));
        DrawColorLabel($"TODO: {todoCount}", k_Colors["TODO"]);
        DrawColorLabel($"FIXME: {fixmeCount}", k_Colors["FIXME"]);
        DrawColorLabel($"HACK: {hackCount}", k_Colors["HACK"]);
        EditorGUILayout.EndHorizontal();
    }

    void DrawColorLabel(string text, Color color)
    {
        var prevColor = GUI.color;
        GUI.color = color;
        EditorGUILayout.LabelField(text, EditorStyles.boldLabel, GUILayout.Width(80));
        GUI.color = prevColor;
    }

    void DrawEntries()
    {
        var filtered = m_entries.AsEnumerable();

        if (m_filterType != "ALL")
            filtered = filtered.Where(e => e.type == m_filterType);

        if (!string.IsNullOrEmpty(m_searchText))
            filtered = filtered.Where(e =>
                e.message.ToLower().Contains(m_searchText.ToLower())
                || e.fileName.ToLower().Contains(m_searchText.ToLower()));

        var list = filtered.ToList();

        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
        string lastFile = null;

        foreach (var entry in list)
        {
            if (entry.fileName != lastFile)
            {
                EditorGUILayout.Space(4);
                if (GUILayout.Button(entry.filePath, EditorStyles.linkLabel))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(entry.filePath);
                    if (asset != null)
                        AssetDatabase.OpenAsset(asset, entry.lineNumber);
                }
                lastFile = entry.fileName;
            }

            EditorGUILayout.BeginHorizontal();

            // タイプバッジ
            var prevColor = GUI.color;
            GUI.color = k_Colors.ContainsKey(entry.type) ? k_Colors[entry.type] : Color.white;
            EditorGUILayout.LabelField(entry.type, EditorStyles.miniLabel, GUILayout.Width(45));
            GUI.color = prevColor;

            // 行番号
            EditorGUILayout.LabelField($"L{entry.lineNumber}", GUILayout.Width(45));

            // メッセージ（クリックでファイルを開く）
            if (GUILayout.Button(entry.message, EditorStyles.label))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(entry.filePath);
                if (asset != null)
                    AssetDatabase.OpenAsset(asset, entry.lineNumber);
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    void ScanAll()
    {
        m_entries.Clear();
        var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Project/Scripts", "Assets/Editor" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".cs")) continue;
            ScanFile(path);
        }

        // ファイル名→行番号順にソート
        m_entries = m_entries.OrderBy(e => e.filePath).ThenBy(e => e.lineNumber).ToList();
    }

    void ScanFile(string assetPath)
    {
        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath)) return;

        string[] lines = File.ReadAllLines(fullPath);
        string fileName = Path.GetFileName(assetPath);

        for (int i = 0; i < lines.Length; i++)
        {
            var match = k_TodoPattern.Match(lines[i]);
            if (!match.Success) continue;

            m_entries.Add(new TodoEntry
            {
                filePath = assetPath,
                lineNumber = i + 1,
                type = match.Groups[1].Value.ToUpper(),
                message = match.Groups[2].Value.Trim(),
                fileName = fileName
            });
        }
    }
}
