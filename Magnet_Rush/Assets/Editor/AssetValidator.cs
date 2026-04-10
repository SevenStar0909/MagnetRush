using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// アセットバリデータ。Missing Reference検出、タグ/レイヤー整合性チェック、ビルド前チェックを提供する。
/// メニュー: Tools/Asset Validator
/// </summary>
public class AssetValidator : EditorWindow, IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    private Vector2 m_scrollPos;
    private List<ValidationResult> m_results = new List<ValidationResult>();
    private bool m_showErrors = true;
    private bool m_showWarnings = true;

    struct ValidationResult
    {
        public string category;
        public string message;
        public string assetPath;
        public MessageType severity;
        public Object targetObject;
    }

    [MenuItem("Tools/Asset Validator")]
    static void ShowWindow()
    {
        GetWindow<AssetValidator>("Asset Validator");
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("全チェック実行", EditorStyles.toolbarButton, GUILayout.Width(120)))
        {
            ExecuteAllChecks();
        }
        if (GUILayout.Button("シーンのみ", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            m_results.Clear();
            ValidateOpenScenes();
        }
        if (GUILayout.Button("Prefabのみ", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            m_results.Clear();
            ValidatePrefabs();
        }
        if (GUILayout.Button("SO参照のみ", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            m_results.Clear();
            ValidateScriptableObjects();
        }
        if (GUILayout.Button("コード規約", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            m_results.Clear();
            ValidateCodeConventions();
            if (m_results.Count == 0)
                m_results.Add(new ValidationResult { category = "結果", message = "コード規約違反なし。", severity = MessageType.Info });
            Repaint();
        }
        GUILayout.FlexibleSpace();

        m_showErrors = GUILayout.Toggle(m_showErrors, $"エラー ({m_results.Count(r => r.severity == MessageType.Error)})", EditorStyles.toolbarButton);
        m_showWarnings = GUILayout.Toggle(m_showWarnings, $"警告 ({m_results.Count(r => r.severity == MessageType.Warning)})", EditorStyles.toolbarButton);
        EditorGUILayout.EndHorizontal();

        if (m_results.Count == 0)
        {
            EditorGUILayout.HelpBox("チェックを実行してください。", MessageType.Info);
            return;
        }

        // Info結果のみの場合（問題なし）
        if (m_results.All(r => r.severity == MessageType.Info))
        {
            EditorGUILayout.HelpBox(m_results[0].message, MessageType.Info);
            return;
        }

        int errorCount = m_results.Count(r => r.severity == MessageType.Error);
        int warningCount = m_results.Count(r => r.severity == MessageType.Warning);
        EditorGUILayout.LabelField($"結果: エラー {errorCount} 件 / 警告 {warningCount} 件", EditorStyles.boldLabel);

        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
        string lastCategory = null;
        foreach (var result in m_results)
        {
            if (result.severity == MessageType.Error && !m_showErrors) continue;
            if (result.severity == MessageType.Warning && !m_showWarnings) continue;

            if (result.category != lastCategory)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(result.category, EditorStyles.boldLabel);
                lastCategory = result.category;
            }

            EditorGUILayout.BeginHorizontal();
            var icon = result.severity == MessageType.Error ? MessageType.Error : MessageType.Warning;
            EditorGUILayout.HelpBox(result.message, icon);

            if (result.targetObject != null)
            {
                if (GUILayout.Button("選択", GUILayout.Width(40), GUILayout.Height(36)))
                {
                    Selection.activeObject = result.targetObject;
                    EditorGUIUtility.PingObject(result.targetObject);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    void ExecuteAllChecks()
    {
        m_results.Clear();
        try
        {
            EditorUtility.DisplayProgressBar("Asset Validator", "タグ/レイヤーチェック中...", 0.1f);
            ValidateTagsAndLayers();
            EditorUtility.DisplayProgressBar("Asset Validator", "シーンチェック中...", 0.3f);
            ValidateOpenScenes();
            EditorUtility.DisplayProgressBar("Asset Validator", "Prefabチェック中...", 0.5f);
            ValidatePrefabs();
            EditorUtility.DisplayProgressBar("Asset Validator", "SOチェック中...", 0.7f);
            ValidateScriptableObjects();
            EditorUtility.DisplayProgressBar("Asset Validator", "コード規約チェック中...", 0.8f);
            ValidateCodeConventions();
            EditorUtility.DisplayProgressBar("Asset Validator", "ビルドシーンチェック中...", 0.9f);
            ValidateBuildScenes();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AssetValidator] チェック中にエラー: {e}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        LogResultsToConsole();

        if (m_results.Count == 0)
        {
            m_results.Add(new ValidationResult
            {
                category = "結果",
                message = "全チェック完了。問題は見つかりませんでした。",
                severity = MessageType.Info
            });
        }
        Repaint();
    }

    void ValidateTagsAndLayers()
    {
        var registeredTags = new HashSet<string>(UnityEditorInternal.InternalEditorUtility.tags);
        var tagFields = typeof(GameTags).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        foreach (var field in tagFields)
        {
            if (field.FieldType != typeof(string)) continue;
            string tagValue = (string)field.GetValue(null);
            if (!registeredTags.Contains(tagValue))
            {
                m_results.Add(new ValidationResult
                {
                    category = "タグ整合性",
                    message = $"GameTags.{field.Name} = \"{tagValue}\" がTagManagerに未登録",
                    severity = MessageType.Error
                });
            }
        }

        string[] layerNames = { "Ground", "Wall", "Player", "Enemy", "Bullet", "MagnetField" };
        foreach (string layerName in layerNames)
        {
            if (LayerMask.NameToLayer(layerName) == -1)
            {
                m_results.Add(new ValidationResult
                {
                    category = "レイヤー整合性",
                    message = $"レイヤー \"{layerName}\" がProjectSettingsに未定義",
                    severity = MessageType.Error
                });
            }
        }
    }

    void ValidateOpenScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                ScanGameObject(root, scene.name);
        }
    }

    void ScanGameObject(GameObject go, string context)
    {
        var components = go.GetComponents<Component>();
        foreach (var component in components)
        {
            if (component == null)
            {
                m_results.Add(new ValidationResult
                {
                    category = $"Missing Script [{context}]",
                    message = $"{BuildPath(go)} にMissing Scriptがあります",
                    severity = MessageType.Error,
                    targetObject = go
                });
                continue;
            }
            ScanSerializedFields(component, BuildPath(go), context);
        }

        for (int i = 0; i < go.transform.childCount; i++)
            ScanGameObject(go.transform.GetChild(i).gameObject, context);
    }

    void ScanSerializedFields(Object obj, string objectPath, string context)
    {
        var so = new SerializedObject(obj);
        var prop = so.GetIterator();

        while (prop.NextVisible(true))
        {
            if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
            {
                m_results.Add(new ValidationResult
                {
                    category = $"Missing Reference [{context}]",
                    message = $"{objectPath} → {obj.GetType().Name}.{prop.propertyPath} が参照切れ",
                    severity = MessageType.Error,
                    targetObject = obj
                });
            }
        }
    }

    void ValidatePrefabs()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                ScanGameObject(prefab, $"Prefab: {path}");
        }
    }

    void ValidateScriptableObjects()
    {
        var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/ScriptableObjects" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so != null)
                ScanSerializedFields(so, path, "ScriptableObject");
        }
    }

    void ValidateBuildScenes()
    {
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            if (!System.IO.File.Exists(scene.path))
            {
                m_results.Add(new ValidationResult
                {
                    category = "ビルドシーン",
                    message = $"Build Settingsのシーンが存在しません: {scene.path}",
                    severity = MessageType.Error
                });
            }
        }
    }

    // PhysicsLayers.cs と GameTags.cs は除外（定義元なので直接文字列を使う）
    static readonly HashSet<string> k_ExcludedFiles = new HashSet<string>
    {
        "PhysicsLayers.cs",
        "GameTags.cs",
        "RenderingLayers.cs"
    };

    static readonly Regex s_nameToLayer = new Regex(@"NameToLayer\s*\(\s*""[^""]+""", RegexOptions.Compiled);
    static readonly Regex s_compareTag = new Regex(@"CompareTag\s*\(\s*""[^""]+""", RegexOptions.Compiled);
    static readonly Regex s_tagEquals = new Regex(@"\.tag\s*[!=]=\s*""[^""]+""", RegexOptions.Compiled);
    // renderingLayerMask に直接数値リテラルを代入/演算しているパターン
    static readonly Regex s_renderingLayerLiteral = new Regex(@"renderingLayerMask\s*[|&^]?=\s*\d+", RegexOptions.Compiled);

    void ValidateCodeConventions()
    {
        string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
        if (!Directory.Exists(scriptsRoot)) return;

        var csFiles = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);

        foreach (string filePath in csFiles)
        {
            string fileName = Path.GetFileName(filePath);
            if (k_ExcludedFiles.Contains(fileName)) continue;

            string[] lines = File.ReadAllLines(filePath);
            string relativePath = "Assets" + filePath.Substring(Application.dataPath.Length).Replace('\\', '/');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // コメント行はスキップ
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("///")) continue;

                if (s_nameToLayer.IsMatch(line))
                {
                    m_results.Add(new ValidationResult
                    {
                        category = "コード規約: PhysicsLayers",
                        message = $"{relativePath}:{i + 1} — NameToLayer(\"\") の直接使用。PhysicsLayers.XXX を使ってください",
                        severity = MessageType.Warning
                    });
                }

                if (s_compareTag.IsMatch(line))
                {
                    m_results.Add(new ValidationResult
                    {
                        category = "コード規約: GameTags",
                        message = $"{relativePath}:{i + 1} — CompareTag(\"\") の直接使用。GameTags.XXX を使ってください",
                        severity = MessageType.Warning
                    });
                }

                if (s_tagEquals.IsMatch(line))
                {
                    m_results.Add(new ValidationResult
                    {
                        category = "コード規約: GameTags",
                        message = $"{relativePath}:{i + 1} — .tag == \"\" の直接比較。GameTags.XXX を使ってください",
                        severity = MessageType.Warning
                    });
                }

                if (s_renderingLayerLiteral.IsMatch(line))
                {
                    m_results.Add(new ValidationResult
                    {
                        category = "コード規約: RenderingLayers",
                        message = $"{relativePath}:{i + 1} — renderingLayerMask に数値リテラル直接指定。RenderingLayers.XXX を使ってください",
                        severity = MessageType.Warning
                    });
                }
            }
        }
    }

    void LogResultsToConsole()
    {
        int errorCount = m_results.Count(r => r.severity == MessageType.Error);
        int warningCount = m_results.Count(r => r.severity == MessageType.Warning);

        if (errorCount == 0 && warningCount == 0)
        {
            Debug.Log("[AssetValidator] 全チェック完了。問題なし。");
            return;
        }

        foreach (var result in m_results)
        {
            string msg = $"[AssetValidator] [{result.category}] {result.message}";
            if (result.severity == MessageType.Error)
                Debug.LogError(msg, result.targetObject);
            else if (result.severity == MessageType.Warning)
                Debug.LogWarning(msg, result.targetObject);
        }

        Debug.Log($"[AssetValidator] 完了: エラー {errorCount} 件 / 警告 {warningCount} 件");
    }

    static string BuildPath(GameObject go)
    {
        string path = go.name;
        var parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    // --- IPreprocessBuildWithReport: ビルド前自動チェック ---

    public void OnPreprocessBuild(BuildReport report)
    {
        var errors = new List<string>();

        var registeredTags = new HashSet<string>(UnityEditorInternal.InternalEditorUtility.tags);
        var tagFields = typeof(GameTags).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        foreach (var field in tagFields)
        {
            if (field.FieldType != typeof(string)) continue;
            string tagValue = (string)field.GetValue(null);
            if (!registeredTags.Contains(tagValue))
                errors.Add($"GameTags.{field.Name} = \"{tagValue}\" がTagManagerに未登録");
        }

        string[] layerNames = { "Ground", "Wall", "Player", "Enemy", "Bullet", "MagnetField" };
        foreach (string name in layerNames)
        {
            if (LayerMask.NameToLayer(name) == -1)
                errors.Add($"レイヤー \"{name}\" がProjectSettingsに未定義");
        }

        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && !System.IO.File.Exists(scene.path))
                errors.Add($"ビルドシーンが存在しません: {scene.path}");
        }

        if (errors.Count > 0)
        {
            string message = string.Join("\n", errors);
            throw new BuildFailedException($"ビルド前チェック失敗:\n{message}");
        }
    }

    // --- PlayMode開始時の自動チェック ---

    [InitializeOnLoadMethod]
    static void RegisterPlayModeCheck()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;

        int errorCount = 0;
        int warningCount = 0;

        // Missing Script / Missing Reference（開いているシーン）
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                errorCount += ScanGameObjectRuntime(root, scene.name);
        }

        // タグ/レイヤー整合性
        var registeredTags = new HashSet<string>(UnityEditorInternal.InternalEditorUtility.tags);
        var tagFields = typeof(GameTags).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        foreach (var field in tagFields)
        {
            if (field.FieldType != typeof(string)) continue;
            string tagValue = (string)field.GetValue(null);
            if (!registeredTags.Contains(tagValue))
            {
                Debug.LogError($"[AssetValidator] GameTags.{field.Name} = \"{tagValue}\" がTagManagerに未登録");
                errorCount++;
            }
        }

        string[] layerNames = { "Ground", "Wall", "Player", "Enemy", "Bullet", "MagnetField" };
        foreach (string layerName in layerNames)
        {
            if (LayerMask.NameToLayer(layerName) == -1)
            {
                Debug.LogError($"[AssetValidator] レイヤー \"{layerName}\" がProjectSettingsに未定義");
                errorCount++;
            }
        }

        // コード規約（静的解析）
        warningCount += ScanCodeConventionsRuntime();

        if (errorCount > 0 || warningCount > 0)
            Debug.LogWarning($"[AssetValidator] PlayMode検証: エラー {errorCount} 件 / 警告 {warningCount} 件");
        else
            Debug.Log("[AssetValidator] PlayMode検証: 問題なし");
    }

    static int ScanGameObjectRuntime(GameObject go, string sceneName)
    {
        int count = 0;
        var components = go.GetComponents<Component>();
        foreach (var component in components)
        {
            if (component == null)
            {
                Debug.LogError($"[AssetValidator] Missing Script: {BuildPath(go)} [{sceneName}]", go);
                count++;
                continue;
            }

            var so = new SerializedObject(component);
            var prop = so.GetIterator();
            while (prop.NextVisible(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
                {
                    Debug.LogError($"[AssetValidator] Missing Reference: {BuildPath(go)} → {component.GetType().Name}.{prop.propertyPath} [{sceneName}]", go);
                    count++;
                }
            }
        }

        for (int i = 0; i < go.transform.childCount; i++)
            count += ScanGameObjectRuntime(go.transform.GetChild(i).gameObject, sceneName);

        return count;
    }

    static int ScanCodeConventionsRuntime()
    {
        int count = 0;
        string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
        if (!Directory.Exists(scriptsRoot)) return 0;

        var nameToLayer = new Regex(@"NameToLayer\s*\(\s*""[^""]+""", RegexOptions.Compiled);
        var compareTag = new Regex(@"CompareTag\s*\(\s*""[^""]+""", RegexOptions.Compiled);
        var tagEquals = new Regex(@"\.tag\s*[!=]=\s*""[^""]+""", RegexOptions.Compiled);
        var renderingLiteral = new Regex(@"renderingLayerMask\s*[|&^]?=\s*\d+", RegexOptions.Compiled);

        var excluded = new HashSet<string> { "PhysicsLayers.cs", "GameTags.cs", "RenderingLayers.cs" };

        foreach (string filePath in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(filePath);
            if (excluded.Contains(fileName)) continue;

            string relativePath = "Assets" + filePath.Substring(Application.dataPath.Length).Replace('\\', '/');
            string[] lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("///")) continue;

                string line = lines[i];
                if (nameToLayer.IsMatch(line))
                {
                    Debug.LogWarning($"[AssetValidator] コード規約: {relativePath}:{i + 1} — NameToLayer直接使用。PhysicsLayers.XXX を使ってください");
                    count++;
                }
                if (compareTag.IsMatch(line))
                {
                    Debug.LogWarning($"[AssetValidator] コード規約: {relativePath}:{i + 1} — CompareTag直接使用。GameTags.XXX を使ってください");
                    count++;
                }
                if (tagEquals.IsMatch(line))
                {
                    Debug.LogWarning($"[AssetValidator] コード規約: {relativePath}:{i + 1} — .tag==\"\"直接比較。GameTags.XXX を使ってください");
                    count++;
                }
                if (renderingLiteral.IsMatch(line))
                {
                    Debug.LogWarning($"[AssetValidator] コード規約: {relativePath}:{i + 1} — renderingLayerMask数値直接指定。RenderingLayers.XXX を使ってください");
                    count++;
                }
            }
        }

        return count;
    }
}
