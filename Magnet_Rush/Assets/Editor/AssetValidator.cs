using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
}
