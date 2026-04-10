using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// シーン/Prefabのファイルロックシステム。
/// .locks.json でチーム間のロック状態を共有する（git追跡）。
/// メニュー: Tools/File Lock
/// </summary>
[InitializeOnLoad]
public class FileLockWindow : EditorWindow
{
    static readonly string k_LockFilePath = Path.Combine(
        Application.dataPath, "..", ".locks.json");

    private Vector2 m_scrollPos;
    private static LockDatabase s_db;
    private static string s_currentUser;

    [Serializable]
    class LockDatabase
    {
        public List<LockEntry> locks = new List<LockEntry>();
    }

    [Serializable]
    class LockEntry
    {
        public string path;
        public string user;
        public string timestamp;
    }

    static FileLockWindow()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        s_currentUser = GetGitUserName();
        LoadDatabase();
    }

    [MenuItem("Tools/File Lock")]
    static void ShowWindow()
    {
        GetWindow<FileLockWindow>("File Lock");
    }

    void OnEnable()
    {
        LoadDatabase();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField($"ユーザー: {s_currentUser}", GUILayout.Width(200));
        if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(40)))
            LoadDatabase();
        if (GUILayout.Button("自分のロック全解除", EditorStyles.toolbarButton, GUILayout.Width(120)))
            UnlockAllMine();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 現在開いているシーンのロック操作
        EditorGUILayout.LabelField("開いているシーン", EditorStyles.boldLabel);
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            DrawLockRow(scene.path, scene.name);
        }

        EditorGUILayout.Space(8);

        // 全ロック一覧
        EditorGUILayout.LabelField("全ロック一覧", EditorStyles.boldLabel);
        if (s_db == null || s_db.locks.Count == 0)
        {
            EditorGUILayout.HelpBox("ロックされているファイルはありません。", MessageType.Info);
        }
        else
        {
            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
            foreach (var entry in s_db.locks.ToList())
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(Path.GetFileName(entry.path), GUILayout.Width(200));
                EditorGUILayout.LabelField(entry.user, GUILayout.Width(100));
                EditorGUILayout.LabelField(entry.timestamp, GUILayout.Width(140));

                bool isMine = entry.user == s_currentUser;
                GUI.enabled = isMine;
                if (GUILayout.Button("解除", GUILayout.Width(40)))
                {
                    Unlock(entry.path);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }

    void DrawLockRow(string scenePath, string displayName)
    {
        var existing = FindLock(scenePath);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"  {displayName}", GUILayout.Width(200));

        if (existing != null)
        {
            bool isMine = existing.user == s_currentUser;
            var color = isMine ? Color.green : Color.red;
            var prevColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField($"ロック中: {existing.user}", GUILayout.Width(150));
            GUI.color = prevColor;

            if (isMine && GUILayout.Button("解除", GUILayout.Width(40)))
                Unlock(scenePath);
        }
        else
        {
            EditorGUILayout.LabelField("未ロック", GUILayout.Width(150));
            if (GUILayout.Button("ロック", GUILayout.Width(40)))
                Lock(scenePath);
        }
        EditorGUILayout.EndHorizontal();
    }

    // --- シーンOpen時の自動チェック ---

    static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        LoadDatabase();
        var existing = FindLock(scene.path);
        if (existing != null && existing.user != s_currentUser)
        {
            EditorUtility.DisplayDialog(
                "ファイルロック警告",
                $"{scene.name} は {existing.user} がロック中です。\n" +
                "編集するとコンフリクトの原因になります。",
                "OK");
        }
    }

    // --- ロック操作 ---

    static void Lock(string path)
    {
        LoadDatabase();
        var existing = FindLock(path);
        if (existing != null)
        {
            if (existing.user != s_currentUser)
            {
                EditorUtility.DisplayDialog("ロック失敗",
                    $"{path} は {existing.user} がロック中です。", "OK");
                return;
            }
            return;
        }

        s_db.locks.Add(new LockEntry
        {
            path = path,
            user = s_currentUser,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
        SaveDatabase();
    }

    static void Unlock(string path)
    {
        LoadDatabase();
        s_db.locks.RemoveAll(l => l.path == path && l.user == s_currentUser);
        SaveDatabase();
    }

    void UnlockAllMine()
    {
        LoadDatabase();
        s_db.locks.RemoveAll(l => l.user == s_currentUser);
        SaveDatabase();
    }

    static LockEntry FindLock(string path)
    {
        if (s_db == null) return null;
        return s_db.locks.FirstOrDefault(l => l.path == path);
    }

    // --- データベース ---

    static void LoadDatabase()
    {
        if (File.Exists(k_LockFilePath))
        {
            string json = File.ReadAllText(k_LockFilePath);
            s_db = JsonUtility.FromJson<LockDatabase>(json);
        }
        s_db ??= new LockDatabase();
    }

    static void SaveDatabase()
    {
        string json = JsonUtility.ToJson(s_db, true);
        File.WriteAllText(k_LockFilePath, json);
    }

    static string GetGitUserName()
    {
        try
        {
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = "config user.name";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WorkingDirectory = Application.dataPath;
            process.Start();
            string result = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return string.IsNullOrEmpty(result) ? "Unknown" : result;
        }
        catch
        {
            return "Unknown";
        }
    }
}
