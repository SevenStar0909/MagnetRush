using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// プランナー用調整シート。同じ型の ScriptableObject 群を行=フィールド・列=アセットのテーブル形式で並べて編集する。
/// 1 ウィンドウ内に複数のシートを行（縦）+列（横）のグリッド配置でき、行間ドラッグで高さ調整可能。
/// メニュー: Tools/プランナー用調整シート
/// </summary>
public class SOBrowserWindow : EditorWindow
{
    const float k_FieldColumnWidth = 240f;
    const float k_AssetColumnWidth = 200f;
    const float k_RowHeight = 20f;
    const float k_SplitterSize = 4f;
    const float k_MinPaneHeight = 80f;
    const float k_MinPaneWidth = 200f;

    /// <summary>1 シート分の状態。</summary>
    [Serializable]
    public class Pane
    {
        public int selectedTypeIndex = -1;
        public string searchFilter = "";
        public Vector2 scrollPos;
        public bool showReferences;
        public ScriptableObject trackingTarget;

        [NonSerialized] public List<ScriptableObject> assets = new List<ScriptableObject>();
        [NonSerialized] public Dictionary<ScriptableObject, SerializedObject> serObjects = new Dictionary<ScriptableObject, SerializedObject>();
        [NonSerialized] public List<SheetRow> rows = new List<SheetRow>();
        [NonSerialized] public List<string> references = new List<string>();
    }

    /// <summary>横方向に並ぶペイン群を持つ 1 行。行ごとに高さを保持。</summary>
    [Serializable]
    public class Row
    {
        public List<Pane> panes = new List<Pane>();
        public float height = 300f;
    }

    public class SheetRow
    {
        public bool IsHeader;
        public string HeaderText;
        public string Label;
        public string FieldName;
    }

    [SerializeField] List<Row> m_rows = new List<Row>();

    List<Type> m_availableTypes = new List<Type>();
    string[] m_typeNames = new string[0];

    // 行間ドラッグ状態
    int m_draggingRow = -1;
    float m_dragStartY;
    float m_dragStartHeight;

    [MenuItem("Tools/プランナー用調整シート")]
    static void ShowWindow()
    {
        var w = GetWindow<SOBrowserWindow>("プランナー用調整シート");
        w.minSize = new Vector2(540, 400);
    }

    static void OpenNewWindow()
    {
        var w = CreateInstance<SOBrowserWindow>();
        w.titleContent = new GUIContent("プランナー用調整シート");
        w.minSize = new Vector2(540, 400);
        w.Show();
    }

    void OnEnable()
    {
        ScanAvailableTypes();
        if (m_rows == null) m_rows = new List<Row>();
        EnsureAtLeastOneRowAndPane();
        foreach (var r in m_rows)
            foreach (var p in r.panes)
                ReloadPaneData(p);
    }

    void EnsureAtLeastOneRowAndPane()
    {
        if (m_rows.Count == 0)
        {
            m_rows.Add(new Row());
        }
        if (m_rows[0].panes.Count == 0)
        {
            var p = new Pane();
            if (m_availableTypes.Count > 0) p.selectedTypeIndex = 0;
            m_rows[0].panes.Add(p);
        }
    }

    void OnGUI()
    {
        DrawGlobalToolbar();

        if (m_availableTypes.Count == 0)
        {
            EditorGUILayout.HelpBox("Assets/_Project/ScriptableObjects 配下に ScriptableObject が見つかりません。", MessageType.Info);
            return;
        }

        EnsureAtLeastOneRowAndPane();

        // 行高さの正規化（最終行を残り高さに）
        NormalizeRowHeights();

        for (int r = 0; r < m_rows.Count; r++)
        {
            var row = m_rows[r];
            DrawRow(row, r);

            if (r < m_rows.Count - 1)
                DrawRowSplitter(r);
        }

        HandleRowSplitterDrag();
    }

    // ボタンスタイル（一度だけ作って使い回す）
    static GUIStyle s_btnRefresh, s_btnAdd, s_btnNew, s_btnClose, s_btnNeutral;
    static void EnsureStyles()
    {
        if (s_btnRefresh != null) return;
        GUIStyle Make(Color color) => new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 24,
            margin = new RectOffset(2, 2, 2, 2),
            padding = new RectOffset(8, 8, 2, 2),
            normal = { textColor = color },
            hover = { textColor = Color.white },
        };
        s_btnRefresh = Make(new Color(0.55f, 0.85f, 1f));   // 水色
        s_btnAdd     = Make(new Color(0.55f, 1f, 0.55f));   // 緑
        s_btnNew     = Make(new Color(1f, 0.85f, 0.45f));   // 黄
        s_btnClose   = Make(new Color(1f, 0.55f, 0.55f));   // 赤
        s_btnNeutral = Make(new Color(0.85f, 0.85f, 0.90f));// 灰白
    }

    void DrawGlobalToolbar()
    {
        EnsureStyles();
        EditorGUILayout.BeginHorizontal(GUILayout.Height(28));

        if (GUILayout.Button("⟳ 更新", s_btnRefresh, GUILayout.Width(70)))
        {
            ScanAvailableTypes();
            foreach (var r in m_rows) foreach (var p in r.panes) ReloadPaneData(p);
        }

        if (GUILayout.Button("＋ 縦分割（行追加）", s_btnAdd, GUILayout.Width(160)))
        {
            EditorApplication.delayCall += () =>
            {
                var p = new Pane();
                if (m_availableTypes.Count > 0) p.selectedTypeIndex = 0;
                ReloadPaneData(p);
                var newRow = new Row();
                newRow.panes.Add(p);
                m_rows.Add(newRow);
                NormalizeRowHeights();
                Repaint();
            };
        }

        if (GUILayout.Button("＋ 横分割（列追加）", s_btnAdd, GUILayout.Width(160)))
        {
            EditorApplication.delayCall += () =>
            {
                var p = new Pane();
                if (m_availableTypes.Count > 0) p.selectedTypeIndex = 0;
                ReloadPaneData(p);
                var lastRow = m_rows[m_rows.Count - 1];
                lastRow.panes.Add(p);
                Repaint();
            };
        }

        if (GUILayout.Button("⧉ 新規ウィンドウ", s_btnNew, GUILayout.Width(140)))
        {
            OpenNewWindow();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    void NormalizeRowHeights()
    {
        if (m_rows.Count == 0) return;
        float toolbarHeight = EditorStyles.toolbar.fixedHeight;
        float available = position.height - toolbarHeight - k_SplitterSize * (m_rows.Count - 1);
        if (m_rows.Count == 1)
        {
            m_rows[0].height = Mathf.Max(k_MinPaneHeight, available);
        }
        else
        {
            // 既存値の合計が available を超えたら均等再分配
            float sum = m_rows.Sum(r => r.height);
            if (sum > available + 0.5f || sum < available - 0.5f)
            {
                float each = Mathf.Max(k_MinPaneHeight, available / m_rows.Count);
                foreach (var r in m_rows) r.height = each;
            }
        }
    }

    void DrawRow(Row row, int rowIndex)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(row.height));

        int paneCount = row.panes.Count;
        float paneWidth = Mathf.Max(k_MinPaneWidth, position.width / paneCount);

        for (int c = 0; c < paneCount; c++)
        {
            var pane = row.panes[c];
            EditorGUILayout.BeginVertical(GUILayout.Width(paneWidth), GUILayout.Height(row.height));
            DrawPane(pane, rowIndex, c);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndHorizontal();
    }

    void DrawPane(Pane pane, int rowIndex, int colIndex)
    {
        DrawPaneToolbar(pane, rowIndex, colIndex);

        if (pane.showReferences && pane.trackingTarget != null)
        {
            DrawReferences(pane);
            return;
        }

        if (pane.selectedTypeIndex < 0 || pane.selectedTypeIndex >= m_availableTypes.Count)
        {
            EditorGUILayout.HelpBox("型を選択してください。", MessageType.Info);
            return;
        }

        DrawSheet(pane);
    }

    void DrawPaneToolbar(Pane pane, int rowIndex, int colIndex)
    {
        EnsureStyles();
        EditorGUILayout.BeginHorizontal(GUILayout.Height(28));

        if (m_typeNames.Length > 0)
        {
            int newIdx = EditorGUILayout.Popup(pane.selectedTypeIndex, m_typeNames, GUILayout.Width(180), GUILayout.Height(22));
            if (newIdx != pane.selectedTypeIndex)
            {
                pane.selectedTypeIndex = newIdx;
                ReloadPaneData(pane);
            }
        }

        GUILayout.FlexibleSpace();

        pane.searchFilter = GUILayout.TextField(pane.searchFilter, GUILayout.Width(120), GUILayout.Height(22));

        if (pane.showReferences && GUILayout.Button("← 戻る", s_btnNeutral, GUILayout.Width(70)))
            pane.showReferences = false;

        // ペイン削除（最後の1ペインなら無効）
        bool canClose = !(m_rows.Count == 1 && m_rows[0].panes.Count == 1);
        EditorGUI.BeginDisabledGroup(!canClose);
        if (GUILayout.Button("× 閉じる", s_btnClose, GUILayout.Width(80)))
        {
            int rIdx = rowIndex; int cIdx = colIndex;
            EditorApplication.delayCall += () =>
            {
                if (rIdx < m_rows.Count && cIdx < m_rows[rIdx].panes.Count)
                {
                    m_rows[rIdx].panes.RemoveAt(cIdx);
                    if (m_rows[rIdx].panes.Count == 0)
                        m_rows.RemoveAt(rIdx);
                    EnsureAtLeastOneRowAndPane();
                    NormalizeRowHeights();
                    Repaint();
                }
            };
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
    }

    void DrawSheet(Pane pane)
    {
        var visibleAssets = string.IsNullOrEmpty(pane.searchFilter)
            ? pane.assets
            : pane.assets.Where(a => a.name.ToLower().Contains(pane.searchFilter.ToLower())).ToList();

        if (visibleAssets.Count == 0)
        {
            EditorGUILayout.HelpBox("この型のアセットが見つかりません（または検索にヒットしません）。", MessageType.Info);
            return;
        }
        if (pane.rows.Count == 0)
        {
            EditorGUILayout.HelpBox("シリアライズ可能なフィールドが見つかりません。", MessageType.Info);
            return;
        }

        pane.scrollPos = EditorGUILayout.BeginScrollView(pane.scrollPos, true, true);

        DrawAssetHeaderRow(pane, visibleAssets);

        foreach (var row in pane.rows)
        {
            if (row.IsHeader)
                DrawHeaderRow(row, visibleAssets.Count);
            else
                DrawFieldRow(pane, row, visibleAssets);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawAssetHeaderRow(Pane pane, List<ScriptableObject> assets)
    {
        var headerStyle = new GUIStyle(EditorStyles.toolbar) { fixedHeight = 0 };
        EditorGUILayout.BeginHorizontal(headerStyle, GUILayout.Height(k_RowHeight * 2 + 4));

        EditorGUILayout.BeginVertical(GUILayout.Width(k_FieldColumnWidth));
        GUILayout.Label("フィールド", EditorStyles.boldLabel);
        GUILayout.Label("", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        foreach (var asset in assets)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(k_AssetColumnWidth));
            GUILayout.Label(new GUIContent(asset.name, asset.name), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📁 表示", s_btnNeutral, GUILayout.Width(70), GUILayout.Height(20)))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            if (GUILayout.Button("使用箇所", s_btnNeutral, GUILayout.Width(70), GUILayout.Height(20)))
            {
                pane.trackingTarget = asset;
                FindReferences(pane, AssetDatabase.GetAssetPath(asset));
                pane.showReferences = true;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndHorizontal();
    }

    void DrawHeaderRow(SheetRow row, int assetCount)
    {
        var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(k_RowHeight + 4));
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.20f, 1f));
        var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.85f, 0.95f, 1f) } };
        GUILayout.Space(4);
        GUILayout.Label(row.HeaderText, style, GUILayout.Width(k_FieldColumnWidth - 4));
        GUILayout.Space(k_AssetColumnWidth * assetCount);
        EditorGUILayout.EndHorizontal();
    }

    void DrawFieldRow(Pane pane, SheetRow row, List<ScriptableObject> assets)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(k_RowHeight + 2));

        GUILayout.Label(new GUIContent("  " + row.Label, row.Label), EditorStyles.label, GUILayout.Width(k_FieldColumnWidth));

        foreach (var asset in assets)
        {
            var so = pane.serObjects[asset];
            so.Update();
            var prop = so.FindProperty(row.FieldName);
            var cellRect = GUILayoutUtility.GetRect(k_AssetColumnWidth, k_RowHeight,
                GUILayout.Width(k_AssetColumnWidth), GUILayout.Height(k_RowHeight));
            if (prop != null)
            {
                EditorGUI.BeginChangeCheck();
                DrawCell(cellRect, prop);
                if (EditorGUI.EndChangeCheck())
                    so.ApplyModifiedProperties();
            }
            else
            {
                EditorGUI.LabelField(cellRect, "-");
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>セル 1 つを型別に値だけ描画する。ラベルは出さない（左端列が担当）。</summary>
    void DrawCell(Rect rect, SerializedProperty prop)
    {
        rect.x += 2;
        rect.width -= 4;

        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer:
                prop.intValue = EditorGUI.IntField(rect, prop.intValue);
                break;
            case SerializedPropertyType.Float:
                prop.floatValue = EditorGUI.FloatField(rect, prop.floatValue);
                break;
            case SerializedPropertyType.Boolean:
                prop.boolValue = EditorGUI.Toggle(rect, prop.boolValue);
                break;
            case SerializedPropertyType.String:
                prop.stringValue = EditorGUI.TextField(rect, prop.stringValue);
                break;
            case SerializedPropertyType.Color:
                prop.colorValue = EditorGUI.ColorField(rect, prop.colorValue);
                break;
            case SerializedPropertyType.ObjectReference:
                prop.objectReferenceValue = EditorGUI.ObjectField(rect,
                    prop.objectReferenceValue, typeof(UnityEngine.Object), false);
                break;
            case SerializedPropertyType.Enum:
                prop.enumValueIndex = EditorGUI.Popup(rect, prop.enumValueIndex, prop.enumDisplayNames);
                break;
            case SerializedPropertyType.Vector2:
                prop.vector2Value = EditorGUI.Vector2Field(rect, GUIContent.none, prop.vector2Value);
                break;
            case SerializedPropertyType.Vector3:
                prop.vector3Value = EditorGUI.Vector3Field(rect, GUIContent.none, prop.vector3Value);
                break;
            case SerializedPropertyType.Vector4:
                prop.vector4Value = EditorGUI.Vector4Field(rect, GUIContent.none, prop.vector4Value);
                break;
            case SerializedPropertyType.LayerMask:
                prop.intValue = EditorGUI.MaskField(rect, prop.intValue,
                    UnityEditorInternal.InternalEditorUtility.layers);
                break;
            case SerializedPropertyType.AnimationCurve:
                prop.animationCurveValue = EditorGUI.CurveField(rect, prop.animationCurveValue);
                break;
            default:
                EditorGUI.LabelField(rect, $"({prop.propertyType})");
                break;
        }
    }

    void DrawReferences(Pane pane)
    {
        EditorGUILayout.LabelField($"\"{pane.trackingTarget.name}\" の使用箇所", EditorStyles.boldLabel);
        EditorGUILayout.ObjectField("対象", pane.trackingTarget, typeof(ScriptableObject), false);
        EditorGUILayout.Space();

        if (pane.references.Count == 0)
        {
            EditorGUILayout.HelpBox("使用箇所が見つかりませんでした。未使用の可能性があります。", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField($"{pane.references.Count} 件のアセットから参照:");
        pane.scrollPos = EditorGUILayout.BeginScrollView(pane.scrollPos);
        foreach (string refPath in pane.references)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(refPath);
            if (GUILayout.Button("開く", GUILayout.Width(40)))
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(refPath);
                if (obj != null) Selection.activeObject = obj;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawRowSplitter(int rowIndex)
    {
        var rect = GUILayoutUtility.GetRect(1, k_SplitterSize, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.10f, 0.10f, 0.10f));
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);

        var ev = Event.current;
        if (ev.type == EventType.MouseDown && rect.Contains(ev.mousePosition))
        {
            m_draggingRow = rowIndex;
            m_dragStartY = ev.mousePosition.y;
            m_dragStartHeight = m_rows[rowIndex].height;
            ev.Use();
        }
    }

    void HandleRowSplitterDrag()
    {
        var ev = Event.current;
        if (m_draggingRow < 0) return;

        if (ev.type == EventType.MouseDrag)
        {
            float delta = ev.mousePosition.y - m_dragStartY;
            m_rows[m_draggingRow].height = Mathf.Max(k_MinPaneHeight, m_dragStartHeight + delta);
            // 最終行の高さは OnGUI 内で再計算されるので、ここでは触らない
            Repaint();
            ev.Use();
        }
        else if (ev.type == EventType.MouseUp)
        {
            m_draggingRow = -1;
            ev.Use();
        }
    }

    void ScanAvailableTypes()
    {
        var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/ScriptableObjects" });
        var typeSet = new HashSet<Type>();
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so == null) continue;
            typeSet.Add(so.GetType());
        }
        m_availableTypes = typeSet.OrderBy(t => GetTypeDisplayName(t)).ToList();
        m_typeNames = m_availableTypes.Select(t => GetTypeDisplayName(t)).ToArray();
    }

    /// <summary>
    /// SO 型のドロップダウン表示名を返す。[ClassLabelSO("...")] が付いていれば日本語名、無ければ型名そのまま。
    /// </summary>
    static string GetTypeDisplayName(Type t)
    {
        var attr = t.GetCustomAttribute<ClassLabelSOAttribute>(inherit: true);
        return attr != null ? attr.Text : t.Name;
    }

    void ReloadPaneData(Pane pane)
    {
        pane.serObjects.Clear();
        pane.assets.Clear();
        pane.rows.Clear();

        if (pane.selectedTypeIndex < 0 || pane.selectedTypeIndex >= m_availableTypes.Count) return;
        var type = m_availableTypes[pane.selectedTypeIndex];

        var guids = AssetDatabase.FindAssets($"t:{type.Name}", new[] { "Assets/_Project/ScriptableObjects" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so == null || so.GetType() != type) continue;
            pane.assets.Add(so);
            pane.serObjects[so] = new SerializedObject(so);
        }
        pane.assets = pane.assets.OrderBy(a => a.name).ToList();

        BuildRows(pane, type);
    }

    void BuildRows(Pane pane, Type type)
    {
        pane.rows.Clear();

        var inheritanceChain = new List<Type>();
        var t = type;
        while (t != null && t != typeof(ScriptableObject) && t != typeof(UnityEngine.Object))
        {
            inheritanceChain.Add(t);
            t = t.BaseType;
        }
        inheritanceChain.Reverse();

        var bindings = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var allFields = new List<FieldInfo>();
        foreach (var cls in inheritanceChain)
        {
            var fields = cls.GetFields(bindings).OrderBy(f => f.MetadataToken).ToList();
            allFields.AddRange(fields);
        }

        foreach (var f in allFields)
        {
            bool isSerialized;
            if (f.IsPublic)
                isSerialized = !HasAttribute(f, "System.NonSerializedAttribute");
            else
                isSerialized = f.GetCustomAttribute<SerializeField>() != null;

            if (!isSerialized) continue;
            if (f.GetCustomAttribute<HideInInspector>() != null) continue;

            var headerAttr = f.GetCustomAttribute<HeaderAttribute>();
            if (headerAttr != null)
                pane.rows.Add(new SheetRow { IsHeader = true, HeaderText = headerAttr.header });

            string label = ObjectNames.NicifyVariableName(f.Name);
            var labelAttr = f.GetCustomAttribute<LabelAttribute>();
            if (labelAttr != null)
            {
                label = labelAttr.Text;
            }
            else
            {
                var lr = f.GetCustomAttribute<LabelRangeAttribute>();
                if (lr != null) label = lr.Text;
            }
            pane.rows.Add(new SheetRow { IsHeader = false, Label = label, FieldName = f.Name });
        }
    }

    static bool HasAttribute(FieldInfo f, string fullTypeName)
    {
        foreach (var attr in f.GetCustomAttributes(false))
            if (attr.GetType().FullName == fullTypeName) return true;
        return false;
    }

    void FindReferences(Pane pane, string soPath)
    {
        pane.references.Clear();
        string soGuid = AssetDatabase.AssetPathToGUID(soPath);
        if (string.IsNullOrEmpty(soGuid)) return;

        var allPaths = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/_Project/")
                && (p.EndsWith(".prefab") || p.EndsWith(".unity") || p.EndsWith(".asset")));

        foreach (var assetPath in allPaths)
        {
            if (assetPath == soPath) continue;
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) continue;
            string content = File.ReadAllText(fullPath);
            if (content.Contains(soGuid)) pane.references.Add(assetPath);
        }
    }
}
