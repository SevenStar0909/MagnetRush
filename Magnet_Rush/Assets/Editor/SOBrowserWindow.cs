using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ScriptableObjectブラウザ。型別グルーピング、使用箇所追跡、クイック作成を提供。
/// メニュー: Tools/SO Browser
/// </summary>
public class SOBrowserWindow : EditorWindow
{
    private Vector2 m_scrollPos;
    private Dictionary<string, List<SOEntry>> m_grouped = new Dictionary<string, List<SOEntry>>();
    private Dictionary<string, bool> m_foldouts = new Dictionary<string, bool>();
    private string m_searchFilter = "";

    // 使用箇所追跡
    private ScriptableObject m_trackingTarget;
    private List<string> m_references = new List<string>();
    private bool m_showReferences;

    struct SOEntry
    {
        public ScriptableObject asset;
        public string path;
        public Editor cachedEditor;
    }

    [MenuItem("Tools/SO Browser")]
    static void ShowWindow()
    {
        GetWindow<SOBrowserWindow>("SO Browser");
    }

    void OnEnable()
    {
        RefreshList();
    }

    void OnGUI()
    {
        DrawToolbar();

        if (m_showReferences && m_trackingTarget != null)
        {
            DrawReferences();
            return;
        }

        DrawSOList();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(40)))
            RefreshList();

        m_searchFilter = EditorGUILayout.TextField(m_searchFilter, EditorStyles.toolbarSearchField);

        if (m_showReferences && GUILayout.Button("← 一覧に戻る", EditorStyles.toolbarButton, GUILayout.Width(100)))
            m_showReferences = false;

        EditorGUILayout.EndHorizontal();
    }

    void DrawSOList()
    {
        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);

        foreach (var kvp in m_grouped.OrderBy(k => k.Key))
        {
            string typeName = kvp.Key;
            var entries = kvp.Value;

            // 検索フィルタ
            if (!string.IsNullOrEmpty(m_searchFilter))
            {
                bool match = typeName.ToLower().Contains(m_searchFilter.ToLower())
                    || entries.Any(e => e.asset.name.ToLower().Contains(m_searchFilter.ToLower()));
                if (!match) continue;
            }

            if (!m_foldouts.ContainsKey(typeName))
                m_foldouts[typeName] = false;

            EditorGUILayout.Space(2);
            m_foldouts[typeName] = EditorGUILayout.Foldout(m_foldouts[typeName],
                $"{typeName} ({entries.Count})", true, EditorStyles.foldoutHeader);

            if (!m_foldouts[typeName]) continue;

            EditorGUI.indentLevel++;
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(m_searchFilter)
                    && !entry.asset.name.ToLower().Contains(m_searchFilter.ToLower())
                    && !typeName.ToLower().Contains(m_searchFilter.ToLower()))
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // ヘッダー行: 名前 + ボタン
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(entry.asset, typeof(ScriptableObject), false);

                if (GUILayout.Button("使用箇所", GUILayout.Width(60)))
                {
                    m_trackingTarget = entry.asset;
                    FindReferences(entry.path);
                    m_showReferences = true;
                }
                if (GUILayout.Button("選択", GUILayout.Width(40)))
                {
                    Selection.activeObject = entry.asset;
                    EditorGUIUtility.PingObject(entry.asset);
                }
                EditorGUILayout.EndHorizontal();

                // インラインInspector
                if (entry.cachedEditor != null)
                {
                    EditorGUI.indentLevel++;
                    entry.cachedEditor.OnInspectorGUI();
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(1);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawReferences()
    {
        EditorGUILayout.LabelField($"\"{m_trackingTarget.name}\" の使用箇所", EditorStyles.boldLabel);
        EditorGUILayout.ObjectField("対象", m_trackingTarget, typeof(ScriptableObject), false);
        EditorGUILayout.Space();

        if (m_references.Count == 0)
        {
            EditorGUILayout.HelpBox("使用箇所が見つかりませんでした。未使用の可能性があります。", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField($"{m_references.Count} 件のアセットから参照されています:");
        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
        foreach (string refPath in m_references)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(refPath);
            if (GUILayout.Button("開く", GUILayout.Width(40)))
            {
                var obj = AssetDatabase.LoadAssetAtPath<Object>(refPath);
                if (obj != null) Selection.activeObject = obj;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    void RefreshList()
    {
        m_grouped.Clear();

        var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/ScriptableObjects" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so == null) continue;

            string typeName = so.GetType().Name;
            if (!m_grouped.ContainsKey(typeName))
                m_grouped[typeName] = new List<SOEntry>();

            m_grouped[typeName].Add(new SOEntry
            {
                asset = so,
                path = path,
                cachedEditor = Editor.CreateEditor(so)
            });
        }
    }

    void FindReferences(string soPath)
    {
        m_references.Clear();
        string soGuid = AssetDatabase.AssetPathToGUID(soPath);
        if (string.IsNullOrEmpty(soGuid)) return;

        var allPaths = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/_Project/")
                && (p.EndsWith(".prefab") || p.EndsWith(".unity") || p.EndsWith(".asset")));

        foreach (string assetPath in allPaths)
        {
            if (assetPath == soPath) continue;
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) continue;

            string content = File.ReadAllText(fullPath);
            if (content.Contains(soGuid))
                m_references.Add(assetPath);
        }
    }

    void OnDisable()
    {
        // キャッシュしたEditorを破棄
        foreach (var kvp in m_grouped)
        {
            foreach (var entry in kvp.Value)
            {
                if (entry.cachedEditor != null)
                    DestroyImmediate(entry.cachedEditor);
            }
        }
    }
}
