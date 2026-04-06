using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 命名規則バリデータ。
/// - AssetPostprocessor: スクリプトファイル名のPascalCaseチェック（インポート時自動）
/// - EditorWindow: コード内のフィールド命名規則チェック（手動スキャン）
/// メニュー: Tools/Naming Convention Validator
/// </summary>
public class NamingConventionValidator : AssetPostprocessor
{
    // --- AssetPostprocessor: ファイル名チェック ---

    static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (!path.StartsWith("Assets/_Project/")) continue;
            if (!path.EndsWith(".cs")) continue;

            string fileName = Path.GetFileNameWithoutExtension(path);

            // テストファイルは末尾Tests許容
            // PascalCase: 先頭大文字、英数字のみ
            if (!Regex.IsMatch(fileName, @"^[A-Z][a-zA-Z0-9]+$"))
            {
                Debug.LogWarning($"[命名規則] スクリプト名がPascalCaseではありません: {path}");
            }
        }
    }
}

/// <summary>
/// コード内命名規則のスキャンウィンドウ。
/// private フィールドの m_ プレフィックス、定数の k_ プレフィックス等をチェック。
/// </summary>
public class NamingConventionWindow : EditorWindow
{
    private Vector2 m_scrollPos;
    private List<NamingIssue> m_issues = new List<NamingIssue>();

    struct NamingIssue
    {
        public string filePath;
        public int lineNumber;
        public string line;
        public string rule;
    }

    [MenuItem("Tools/Naming Convention Validator")]
    static void ShowWindow()
    {
        GetWindow<NamingConventionWindow>("命名規則チェック");
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("スキャン実行", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            ScanAllScripts();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"問題: {m_issues.Count} 件");
        EditorGUILayout.EndHorizontal();

        if (m_issues.Count == 0)
        {
            EditorGUILayout.HelpBox("スキャンを実行してください。問題がなければ何も表示されません。", MessageType.Info);
            return;
        }

        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
        string lastFile = null;
        foreach (var issue in m_issues)
        {
            if (issue.filePath != lastFile)
            {
                EditorGUILayout.Space(4);
                if (GUILayout.Button(issue.filePath, EditorStyles.linkLabel))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(issue.filePath);
                    if (asset != null)
                        AssetDatabase.OpenAsset(asset, issue.lineNumber);
                }
                lastFile = issue.filePath;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"  L{issue.lineNumber}", GUILayout.Width(50));
            EditorGUILayout.LabelField(issue.rule, EditorStyles.miniLabel, GUILayout.Width(200));
            EditorGUILayout.SelectableLabel(issue.line.Trim(), GUILayout.Height(16));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    void ScanAllScripts()
    {
        m_issues.Clear();
        var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Project/Scripts" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".cs")) continue;
            ScanFile(path);
        }
    }

    void ScanFile(string assetPath)
    {
        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath)) return;

        string[] lines = File.ReadAllLines(fullPath);
        bool inMultiLineComment = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            // 複数行コメントの簡易スキップ
            if (line.Contains("/*")) inMultiLineComment = true;
            if (line.Contains("*/")) { inMultiLineComment = false; continue; }
            if (inMultiLineComment) continue;

            // 単行コメント・空行・using・属性をスキップ
            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("//")) continue;
            if (line.StartsWith("using ")) continue;
            if (line.StartsWith("[")) continue;

            // private/protectedフィールドの m_ プレフィックスチェック
            // パターン: (private|protected) Type fieldName;
            var privateFieldMatch = Regex.Match(line,
                @"(?:private|protected)\s+(?:readonly\s+)?(?:static\s+)?(\w+(?:<[^>]+>)?(?:\[\])?)\s+(\w+)\s*[;=]");
            if (privateFieldMatch.Success)
            {
                string fieldName = privateFieldMatch.Groups[2].Value;
                string typeName = privateFieldMatch.Groups[1].Value;

                // static → s_ プレフィックス
                if (line.Contains(" static ") && !line.Contains(" const "))
                {
                    if (!fieldName.StartsWith("s_"))
                    {
                        AddIssue(assetPath, i + 1, line, $"staticフィールドは s_ プレフィックス: s_{fieldName}");
                    }
                }
                // const → k_ プレフィックス
                else if (line.Contains(" const "))
                {
                    if (!fieldName.StartsWith("k_"))
                    {
                        AddIssue(assetPath, i + 1, line, $"定数は k_ プレフィックス: k_{ToPascalCase(fieldName)}");
                    }
                }
                // 通常のprivate/protected → m_ プレフィックス
                else
                {
                    if (!fieldName.StartsWith("m_"))
                    {
                        AddIssue(assetPath, i + 1, line, $"private/protectedフィールドは m_ プレフィックス: m_{fieldName}");
                    }
                }
            }

            // publicフィールドの camelCase チェック
            var publicFieldMatch = Regex.Match(line,
                @"public\s+(?:readonly\s+)?(\w+(?:<[^>]+>)?(?:\[\])?)\s+(\w+)\s*[;=]");
            if (publicFieldMatch.Success && !line.Contains(" const ") && !line.Contains(" static ") && !line.Contains(" event "))
            {
                string fieldName = publicFieldMatch.Groups[2].Value;
                // プロパティ（{get; 等）は除外
                if (!line.Contains("{") && !char.IsLower(fieldName[0]) && fieldName != fieldName.ToUpper())
                {
                    AddIssue(assetPath, i + 1, line, $"publicフィールドは camelCase: {ToCamelCase(fieldName)}");
                }
            }
        }
    }

    void AddIssue(string path, int line, string content, string rule)
    {
        m_issues.Add(new NamingIssue
        {
            filePath = path,
            lineNumber = line,
            line = content,
            rule = rule
        });
    }

    static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToLower(s[0]) + s.Substring(1);
    }

    static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }
}
