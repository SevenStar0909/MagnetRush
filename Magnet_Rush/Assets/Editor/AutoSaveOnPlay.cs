using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// PlayMode突入前にシーンとアセットを自動保存する。
/// </summary>
[InitializeOnLoad]
public static class AutoSaveOnPlay
{
    static AutoSaveOnPlay()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[AutoSave] PlayMode前に自動保存しました");
    }
}
