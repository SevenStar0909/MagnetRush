using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 現在開いているシーンにマップシーンをアディティブで追加するエディタウィンドウ。
/// マップシーンは Assets/_Project/Scenes/ 直下のシーンから自動検出。
/// メニュー: MagnetRush > Scene Setup
/// </summary>
public class SceneSetupWindow : EditorWindow
{
    private const string k_MapSceneFolder = "Assets/_Project/Scenes";
    private Vector2 m_scrollPos;

    [MenuItem("Tools/Scene Setup")]
    static void Open() => GetWindow<SceneSetupWindow>("Scene Setup");

    void OnGUI()
    {
        // 現在のシーン構成
        GUILayout.Label("現在のシーン構成", EditorStyles.boldLabel);
        var setup = EditorSceneManager.GetSceneManagerSetup();
        foreach (var s in setup)
        {
            string name = Path.GetFileNameWithoutExtension(s.path);
            string status = s.isActive ? " (Active)" : "";
            EditorGUILayout.LabelField($"  {name}{status}", s.isLoaded ? EditorStyles.label : EditorStyles.miniLabel);
        }

        GUILayout.Space(8);
        DrawSeparator();
        GUILayout.Space(4);

        // マップシーン追加
        GUILayout.Label("マップシーンを追加", EditorStyles.boldLabel);
        GUILayout.Space(4);

        var mapScenes = FindMapScenes();
        var loadedSceneNames = GetLoadedSceneNames();

        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
        foreach (var mapPath in mapScenes)
        {
            string mapName = Path.GetFileNameWithoutExtension(mapPath);
            bool alreadyLoaded = loadedSceneNames.Contains(mapName);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(mapName, GUILayout.Width(200));

            if (alreadyLoaded)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(mapName);
                bool isActive = scene.IsValid() && scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene();

                GUI.enabled = false;
                GUILayout.Button("読み込み済み", GUILayout.Width(100));
                GUI.enabled = true;

                // アクティブシーンは除去不可、それ以外は除去可能
                if (!isActive)
                {
                    if (GUILayout.Button("除去", GUILayout.Width(60)))
                    {
                        if (scene.IsValid())
                            EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            else
            {
                if (GUILayout.Button("追加", GUILayout.Width(100)))
                {
                    EditorSceneManager.OpenScene(mapPath, OpenSceneMode.Additive);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        if (mapScenes.Length == 0)
        {
            EditorGUILayout.HelpBox($"{k_MapSceneFolder} にマップシーンが見つかりません。", MessageType.Info);
        }
    }

    /// <summary>マップシーン候補を検出。Scenes直下の.unityファイル（メンバーシーン以外）。</summary>
    static string[] FindMapScenes()
    {
        if (!AssetDatabase.IsValidFolder(k_MapSceneFolder))
            return System.Array.Empty<string>();

        string activeScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

        return AssetDatabase.FindAssets("t:SceneAsset", new[] { k_MapSceneFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            // 現在のアクティブシーンは除外
            .Where(p => p != activeScenePath)
            .OrderBy(p => p)
            .ToArray();
    }

    static HashSet<string> GetLoadedSceneNames()
    {
        var names = new HashSet<string>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            names.Add(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name);
        return names;
    }

    static void DrawSeparator()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
    }
}
