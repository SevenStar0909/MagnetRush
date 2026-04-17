using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ScriptableObjectブラウザ。カードグリッドで複数SOを横並び表示・編集。
/// ウィンドウ幅に応じて列数が自動調整される。
/// メニュー: Tools/SO Browser
/// </summary>
public class SOBrowserWindow : EditorWindow
{
    const float k_CardWidth = 280f;
    const float k_CardPadding = 6f;

    private Vector2 m_scrollPos;
    private List<ScriptableObject> m_assets = new List<ScriptableObject>();
    private Dictionary<ScriptableObject, Editor> m_editors = new Dictionary<ScriptableObject, Editor>();
    private Dictionary<ScriptableObject, bool> m_foldouts = new Dictionary<ScriptableObject, bool>();
    private string m_searchFilter = "";
    private int m_typeFilterIndex;
    private string[] m_typeOptions = { "All" };

    // 使用箇所追跡
    private ScriptableObject m_trackingTarget;
    private List<string> m_references = new List<string>();
    private bool m_showReferences;

    [MenuItem("Tools/SO Browser")]
    static void ShowWindow()
    {
        var w = GetWindow<SOBrowserWindow>("SO Browser");
        w.minSize = new Vector2(320, 300);
    }

    void OnEnable()
    {
        RefreshAssets();
    }

    void OnDisable()
    {
        foreach (var editor in m_editors.Values)
        {
            if (editor != null) DestroyImmediate(editor);
        }
        m_editors.Clear();
    }

    void OnGUI()
    {
        DrawToolbar();

        if (m_showReferences && m_trackingTarget != null)
        {
            DrawReferences();
            return;
        }

        DrawCardGrid();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(40)))
            RefreshAssets();

        // 型フィルタ
        int newIdx = EditorGUILayout.Popup(m_typeFilterIndex, m_typeOptions,
            EditorStyles.toolbarPopup, GUILayout.Width(140));
        if (newIdx != m_typeFilterIndex)
        {
            m_typeFilterIndex = newIdx;
        }

        // 検索
        m_searchFilter = EditorGUILayout.TextField(m_searchFilter, EditorStyles.toolbarSearchField);

        if (m_showReferences && GUILayout.Button("← 一覧", EditorStyles.toolbarButton, GUILayout.Width(60)))
            m_showReferences = false;

        EditorGUILayout.EndHorizontal();
    }

    void DrawCardGrid()
    {
        string typeFilter = m_typeOptions[m_typeFilterIndex];
        string search = m_searchFilter.ToLower();

        var filtered = m_assets.Where(so =>
        {
            if (so == null) return false;
            if (typeFilter != "All" && so.GetType().Name != typeFilter) return false;
            if (!string.IsNullOrEmpty(search) && !so.name.ToLower().Contains(search)) return false;
            return true;
        }).ToList();

        if (filtered.Count == 0)
        {
            EditorGUILayout.HelpBox("該当するScriptableObjectがありません。", MessageType.Info);
            return;
        }

        // ウィンドウ幅から列数を自動計算
        float available = position.width - k_CardPadding * 2;
        int columns = Mathf.Max(1, Mathf.FloorToInt(available / (k_CardWidth + k_CardPadding)));

        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);

        for (int i = 0; i < filtered.Count; i += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < columns; col++)
            {
                int idx = i + col;
                if (idx < filtered.Count)
                    DrawCard(filtered[idx]);
                else
                    GUILayout.Space(k_CardWidth + k_CardPadding);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawCard(ScriptableObject so)
    {
        if (!m_editors.ContainsKey(so)) return;

        EditorGUILayout.BeginVertical("box", GUILayout.Width(k_CardWidth));

        // ヘッダー
        EditorGUILayout.BeginHorizontal();
        var icon = EditorGUIUtility.ObjectContent(so, so.GetType()).image;
        if (icon != null)
            GUILayout.Label(icon, GUILayout.Width(16), GUILayout.Height(16));

        if (!m_foldouts.ContainsKey(so)) m_foldouts[so] = true;
        m_foldouts[so] = EditorGUILayout.Foldout(m_foldouts[so], so.name, true, EditorStyles.boldLabel);

        if (GUILayout.Button("使用箇所", EditorStyles.miniButton, GUILayout.Width(50)))
        {
            m_trackingTarget = so;
            FindReferences(AssetDatabase.GetAssetPath(so));
            m_showReferences = true;
        }
        if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(32)))
        {
            Selection.activeObject = so;
            EditorGUIUtility.PingObject(so);
        }
        EditorGUILayout.EndHorizontal();

        // 型名
        EditorGUILayout.LabelField(so.GetType().Name, EditorStyles.miniLabel);

        // Inspector
        if (m_foldouts[so] && m_editors[so] != null)
        {
            EditorGUILayout.Space(2);
            m_editors[so].OnInspectorGUI();
        }

        EditorGUILayout.EndVertical();
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

        EditorGUILayout.LabelField($"{m_references.Count} 件のアセットから参照:");
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

    void RefreshAssets()
    {
        foreach (var editor in m_editors.Values)
        {
            if (editor != null) DestroyImmediate(editor);
        }
        m_editors.Clear();
        m_foldouts.Clear();
        m_assets.Clear();

        var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/ScriptableObjects" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so == null) continue;

            m_assets.Add(so);
            m_editors[so] = Editor.CreateEditor(so);
            m_foldouts[so] = true;
        }

        m_assets = m_assets.OrderBy(so => so.GetType().Name).ThenBy(so => so.name).ToList();

        // 型フィルタ選択肢を構築
        var types = m_assets.Select(so => so.GetType().Name).Distinct().ToList();
        types.Insert(0, "All");
        m_typeOptions = types.ToArray();
        m_typeFilterIndex = 0;
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
}
