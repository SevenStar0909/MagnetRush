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
/// プロジェクトスキャナ。Missing Script/Reference検出、タグ/レイヤー整合性、コード規約、命名規則、ビルド前チェックを提供する。
/// メニュー: Tools/プロジェクトスキャン
/// </summary>
public class ProjectScanner : EditorWindow, IPreprocessBuildWithReport
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

    [MenuItem("Tools/プロジェクトスキャン")]
    static void ShowWindow()
    {
        GetWindow<ProjectScanner>("プロジェクトスキャン");
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
        if (GUILayout.Button("命名規則", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            m_results.Clear();
            ValidateNamingConventions();
            if (m_results.Count == 0)
                m_results.Add(new ValidationResult { category = "結果", message = "命名規則違反なし。", severity = MessageType.Info });
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
            EditorUtility.DisplayProgressBar("プロジェクトスキャン", "タグ/レイヤーチェック中...", 0.1f);
            ValidateTagsAndLayers();
            EditorUtility.DisplayProgressBar("プロジェクトスキャン", "シーンチェック中...", 0.3f);
            ValidateOpenScenes();
            EditorUtility.DisplayProgressBar("プロジェクトスキャン", "Prefabチェック中...", 0.5f);
            ValidatePrefabs();
            EditorUtility.DisplayProgressBar("プロジェクトスキャン", "SOチェック中...", 0.7f);
            ValidateScriptableObjects();
            EditorUtility.DisplayProgressBar("プロジェクトスキャン", "コード規約チェック中...", 0.8f);
            ValidateCodeConventions();
            EditorUtility.DisplayProgressBar("プロジェクトスキャン", "命名規則チェック中...", 0.85f);
            ValidateNamingConventions();
            EditorUtility.DisplayProgressBar("プロジェクトスキャン", "コリジョン設計チェック中...", 0.88f);
            ValidateCollisionDesign();
            EditorUtility.DisplayProgressBar("プロジェクトスキャン", "ビルドシーンチェック中...", 0.9f);
            ValidateBuildScenes();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProjectScanner] チェック中にエラー: {e}");
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

        string[] layerNames = { "Ground", "Wall", "Player", "Enemy", "PlayerBullet", "EnemyBullet", "MeleeHitbox", "MagnetField", "PhysicsObject", "EntityBody" };
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

    // コリジョンコールバック内のCompareTag検出用
    static readonly Regex s_collisionCallback = new Regex(@"void\s+(OnTriggerEnter|OnCollisionEnter|OnTriggerStay|OnCollisionStay)", RegexOptions.Compiled);
    static readonly Regex s_compareTagInCode = new Regex(@"CompareTag\s*\(", RegexOptions.Compiled);
    static readonly Regex s_layerCheck = new Regex(@"\.layer\s*[!=]=", RegexOptions.Compiled);

    void ValidateCollisionDesign()
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

            // コリジョンコールバック内のCompareTag/layer判定を検出
            bool inCallback = false;
            int braceDepth = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("///")) continue;

                if (s_collisionCallback.IsMatch(lines[i]))
                {
                    inCallback = true;
                    braceDepth = 0;
                }

                if (inCallback)
                {
                    foreach (char c in lines[i])
                    {
                        if (c == '{') braceDepth++;
                        if (c == '}') braceDepth--;
                    }

                    if (s_compareTagInCode.IsMatch(lines[i]))
                    {
                        m_results.Add(new ValidationResult
                        {
                            category = "コリジョン設計",
                            message = $"{relativePath}:{i + 1} — コリジョンコールバック内でCompareTag使用。Layer Matrixで制御してください",
                            severity = MessageType.Warning
                        });
                    }

                    if (s_layerCheck.IsMatch(lines[i]))
                    {
                        m_results.Add(new ValidationResult
                        {
                            category = "コリジョン設計",
                            message = $"{relativePath}:{i + 1} — コリジョンコールバック内でlayer判定。Layer Matrixで制御してください",
                            severity = MessageType.Warning
                        });
                    }

                    if (braceDepth <= 0 && inCallback)
                        inCallback = false;
                }
            }
        }

        // PhysicsObject層のKinematicチェック
        var propGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
        int propLayer = LayerMask.NameToLayer("PhysicsObject");
        foreach (string guid in propGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.layer != propLayer) continue;

            var rb = prefab.GetComponent<Rigidbody>();
            if (rb != null && rb.isKinematic)
            {
                m_results.Add(new ValidationResult
                {
                    category = "コリジョン設計",
                    message = $"{path} — PhysicsObject層のRigidbodyがKinematic。OnCollisionEnterが発火しません",
                    severity = MessageType.Error
                });
            }
        }
    }

    void ValidateNamingConventions()
    {
        var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Project/Scripts" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".cs")) continue;
            ScanNamingConventions(path);
        }
    }

    void ScanNamingConventions(string assetPath)
    {
        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath)) return;

        string[] lines = File.ReadAllLines(fullPath);
        bool inMultiLineComment = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.Contains("/*")) inMultiLineComment = true;
            if (line.Contains("*/")) { inMultiLineComment = false; continue; }
            if (inMultiLineComment) continue;

            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("//")) continue;
            if (line.StartsWith("using ")) continue;
            if (line.StartsWith("[")) continue;

            bool isProperty = line.Contains("=>") || line.Contains("{ get");

            if (!isProperty)
            {
                var privateFieldMatch = Regex.Match(line,
                    @"(?:private|protected)\s+(?:readonly\s+)?(?:static\s+)?(\w+(?:<[^>]+>)?(?:\[\])?)\s+(\w+)\s*[;=]");
                if (privateFieldMatch.Success)
                {
                    string fieldName = privateFieldMatch.Groups[2].Value;

                    if (line.Contains(" static ") && !line.Contains(" const "))
                    {
                        if (!fieldName.StartsWith("s_"))
                        {
                            m_results.Add(new ValidationResult
                            {
                                category = "命名規則: staticフィールド",
                                message = $"{assetPath}:{i + 1} — s_ プレフィックスを使ってください: s_{fieldName}",
                                severity = MessageType.Warning
                            });
                        }
                    }
                    else if (line.Contains(" const "))
                    {
                        if (!fieldName.StartsWith("k_"))
                        {
                            m_results.Add(new ValidationResult
                            {
                                category = "命名規則: 定数",
                                message = $"{assetPath}:{i + 1} — k_ プレフィックスを使ってください: k_{ToPascalCase(fieldName)}",
                                severity = MessageType.Warning
                            });
                        }
                    }
                    else
                    {
                        if (!fieldName.StartsWith("m_"))
                        {
                            m_results.Add(new ValidationResult
                            {
                                category = "命名規則: privateフィールド",
                                message = $"{assetPath}:{i + 1} — m_ プレフィックスを使ってください: m_{fieldName}",
                                severity = MessageType.Warning
                            });
                        }
                    }
                }
            }

            var publicFieldMatch = Regex.Match(line,
                @"public\s+(?:readonly\s+)?(\w+(?:<[^>]+>)?(?:\[\])?)\s+(\w+)\s*[;=]");
            if (publicFieldMatch.Success && !line.Contains(" const ") && !line.Contains(" static ") && !line.Contains(" event "))
            {
                string fieldName = publicFieldMatch.Groups[2].Value;
                if (!line.Contains("{") && !line.Contains("=>") && !char.IsLower(fieldName[0]) && fieldName != fieldName.ToUpper())
                {
                    m_results.Add(new ValidationResult
                    {
                        category = "命名規則: publicフィールド",
                        message = $"{assetPath}:{i + 1} — camelCaseを使ってください: {ToCamelCase(fieldName)}",
                        severity = MessageType.Warning
                    });
                }
            }
        }
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

    void LogResultsToConsole()
    {
        int errorCount = m_results.Count(r => r.severity == MessageType.Error);
        int warningCount = m_results.Count(r => r.severity == MessageType.Warning);

        if (errorCount == 0 && warningCount == 0)
        {
            Debug.Log("[ProjectScanner] 全チェック完了。問題なし。");
            return;
        }

        foreach (var result in m_results)
        {
            string msg = $"[ProjectScanner] [{result.category}] {result.message}";
            if (result.severity == MessageType.Error)
                Debug.LogError(msg, result.targetObject);
            else if (result.severity == MessageType.Warning)
                Debug.LogWarning(msg, result.targetObject);
        }

        Debug.Log($"[ProjectScanner] 完了: エラー {errorCount} 件 / 警告 {warningCount} 件");
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

        string[] layerNames = { "Ground", "Wall", "Player", "Enemy", "PlayerBullet", "EnemyBullet", "MeleeHitbox", "MagnetField", "PhysicsObject", "EntityBody" };
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

        var scanner = CreateInstance<ProjectScanner>();
        scanner.m_results.Clear();
        scanner.ValidateTagsAndLayers();
        scanner.ValidateOpenScenes();
        scanner.ValidatePrefabs();
        scanner.ValidateScriptableObjects();
        scanner.ValidateCodeConventions();
        scanner.ValidateNamingConventions();
        scanner.ValidateBuildScenes();
        scanner.LogResultsToConsole();
        DestroyImmediate(scanner);
    }

}

/// <summary>
/// スクリプトファイル名のPascalCaseチェック（インポート時自動実行）。
/// </summary>
public class ScriptNameValidator : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (!path.StartsWith("Assets/_Project/")) continue;
            if (!path.EndsWith(".cs")) continue;

            string fileName = Path.GetFileNameWithoutExtension(path);

            if (!Regex.IsMatch(fileName, @"^[A-Z][a-zA-Z0-9]+$"))
            {
                Debug.LogWarning($"[ProjectScanner] スクリプト名がPascalCaseではありません: {path}");
            }
        }
    }
}
