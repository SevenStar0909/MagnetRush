using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 開いているシーン上のプレハブインスタンスの差分を一括してプレハブに適用するエディタ拡張。
/// メニュー: Tools/Prefab Override Applier
/// </summary>
public class ScenePrefabOverrideApplier : EditorWindow
{
    private class Entry
    {
        public GameObject instanceRoot;
        public string sceneName;
        public int modCount;
        public int addCount;
        public int removeCount;
        public bool selected = true;
    }

    private readonly List<Entry> m_entries = new();
    private Vector2 m_scroll;

    [MenuItem("Tools/Prefab Override Applier")]
    static void ShowWindow()
    {
        var w = GetWindow<ScenePrefabOverrideApplier>("Prefab Override Applier");
        w.minSize = new Vector2(420, 300);
        w.Refresh();
    }

    void OnEnable() => Refresh();

    void Refresh()
    {
        m_entries.Clear();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                CollectRecursive(root, scene.name);
        }
        Repaint();
    }

    // ProBuilder等のランタイム再生成メッシュはシーンを開くたびにoverride扱いになる。
    // 適用しても次回再発するのでフィルタする。
    private static readonly HashSet<string> k_IgnoredTypeNames = new()
    {
        "ProBuilderMesh",
        "MeshFilter",
    };

    void CollectRecursive(GameObject go, string sceneName)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(go)
            && PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go)
        {
            var mods = PrefabUtility.GetObjectOverrides(go);
            var adds = PrefabUtility.GetAddedComponents(go);
            var rems = PrefabUtility.GetRemovedComponents(go);

            int applicableMods = 0;
            foreach (var m in mods)
            {
                if (m.instanceObject == null) continue;
                if (k_IgnoredTypeNames.Contains(m.instanceObject.GetType().Name)) continue;
                applicableMods++;
            }

            if (applicableMods > 0 || adds.Count > 0 || rems.Count > 0)
            {
                m_entries.Add(new Entry
                {
                    instanceRoot = go,
                    sceneName = sceneName,
                    modCount = applicableMods,
                    addCount = adds.Count,
                    removeCount = rems.Count,
                });
            }
            // ネストされたプレハブインスタンスを探すためにここでreturnしない
        }

        for (int i = 0; i < go.transform.childCount; i++)
            CollectRecursive(go.transform.GetChild(i).gameObject, sceneName);
    }

    void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("再スキャン", GUILayout.Width(100))) Refresh();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"差分あり: {m_entries.Count}", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全選択")) foreach (var e in m_entries) e.selected = true;
        if (GUILayout.Button("全解除")) foreach (var e in m_entries) e.selected = false;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        if (m_entries.Count == 0)
        {
            EditorGUILayout.HelpBox("差分のあるプレハブインスタンスはありません。", MessageType.Info);
            return;
        }

        m_scroll = EditorGUILayout.BeginScrollView(m_scroll);
        foreach (var e in m_entries)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            e.selected = EditorGUILayout.Toggle(e.selected, GUILayout.Width(18));
            if (e.instanceRoot == null)
            {
                EditorGUILayout.LabelField("(削除済み)");
                EditorGUILayout.EndHorizontal();
                continue;
            }
            EditorGUILayout.LabelField($"[{e.sceneName}] {e.instanceRoot.name}", GUILayout.MinWidth(180));
            EditorGUILayout.LabelField($"変更:{e.modCount} 追加:{e.addCount} 削除:{e.removeCount}", GUILayout.Width(160));
            if (GUILayout.Button("選択", GUILayout.Width(50)))
            {
                Selection.activeObject = e.instanceRoot;
                EditorGUIUtility.PingObject(e.instanceRoot);
            }
            if (GUILayout.Button("適用", GUILayout.Width(50)))
            {
                ApplyOne(e);
                Refresh();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("選択中を一括適用", GUILayout.Height(30)))
        {
            ApplySelected();
        }
        GUI.backgroundColor = Color.white;
    }

    void ApplyOne(Entry e)
    {
        if (e.instanceRoot == null) return;
        try
        {
            PrefabUtility.ApplyPrefabInstance(e.instanceRoot, InteractionMode.AutomatedAction);
            Debug.Log($"[PrefabOverrideApplier] 適用: [{e.sceneName}] {e.instanceRoot.name}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PrefabOverrideApplier] 失敗: {e.instanceRoot.name} - {ex.Message}");
        }
    }

    void ApplySelected()
    {
        var targets = new List<Entry>();
        foreach (var e in m_entries)
            if (e.selected && e.instanceRoot != null) targets.Add(e);

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("Prefab Override Applier", "選択項目がありません。", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "Prefab Override Applier",
            $"{targets.Count} 件のプレハブインスタンスをプレハブに上書きします。よろしいですか？",
            "実行", "キャンセル")) return;

        int done = 0, fail = 0;
        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var e = targets[i];
                EditorUtility.DisplayProgressBar("Prefab Override Applier",
                    $"適用中 ({i + 1}/{targets.Count}) {e.instanceRoot.name}",
                    (float)i / targets.Count);
                try
                {
                    PrefabUtility.ApplyPrefabInstance(e.instanceRoot, InteractionMode.AutomatedAction);
                    done++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[PrefabOverrideApplier] 失敗: {e.instanceRoot.name} - {ex.Message}");
                    fail++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[PrefabOverrideApplier] 完了: 成功 {done} 件 / 失敗 {fail} 件");
        EditorUtility.DisplayDialog("Prefab Override Applier",
            $"完了\n成功: {done} 件\n失敗: {fail} 件", "OK");
        Refresh();
    }
}
