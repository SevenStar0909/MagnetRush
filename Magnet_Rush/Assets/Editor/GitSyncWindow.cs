using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Git 同期ウィンドウ。GitFlowWindow とは別の役割で、リモート状態への強制同期を担当する。
/// 主な機能:
///   - 現在ブランチを origin/<current> で上書き
///   - 現在ブランチを別ブランチ (origin/...) で上書き
///   - develop / main を origin の状態へ更新（元のブランチに自動復帰）
///   - 現在ブランチを fast-forward pull
/// 安全策: 操作前にシーン保存 + AssetDatabase.SaveAssets()、操作後に AssetDatabase.Refresh()。
///         破壊的操作は確認ダイアログを表示し、未コミット変更がある場合は abort する。
/// メニュー: Tools/Git 同期
/// </summary>
public class GitSyncWindow : EditorWindow
{
    string m_currentBranch = "";
    List<string> m_remoteBranches = new List<string>();
    int m_selectedRemoteIndex;
    string m_lastOutput = "";
    Vector2 m_scrollPos;

    static GUIStyle s_btnPrimary, s_btnDanger, s_btnNeutral;

    [MenuItem("Tools/Git/同期")]
    static void Open()
    {
        var w = GetWindow<GitSyncWindow>("Git 同期");
        w.minSize = new Vector2(460, 380);
    }

    void OnEnable()
    {
        RefreshState();
    }

    static void EnsureStyles()
    {
        if (s_btnPrimary != null) return;
        GUIStyle Make(Color c) => new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 26,
            margin = new RectOffset(2, 2, 2, 2),
            padding = new RectOffset(8, 8, 2, 2),
            normal = { textColor = c },
            hover = { textColor = Color.white },
        };
        s_btnPrimary = Make(new Color(0.55f, 0.85f, 1f));
        s_btnDanger  = Make(new Color(1f, 0.55f, 0.55f));
        s_btnNeutral = Make(new Color(0.85f, 0.85f, 0.90f));
    }

    void OnGUI()
    {
        EnsureStyles();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("現在ブランチ", string.IsNullOrEmpty(m_currentBranch) ? "(unknown)" : m_currentBranch, EditorStyles.boldLabel);

        if (GUILayout.Button("⟳ 状態更新（git fetch + ブランチ一覧再取得）", s_btnNeutral))
        {
            ExecuteWithSafety(() => RunGit("fetch origin --prune"), "fetch");
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("▼ クイック操作", EditorStyles.boldLabel);

        if (GUILayout.Button($"現在ブランチを pull（fast-forward only）", s_btnPrimary))
        {
            ExecuteWithSafety(() =>
            {
                var f = RunGit("fetch origin");
                var p = RunGit("pull --ff-only");
                return f + "\n" + p;
            }, "pull --ff-only");
        }

        if (GUILayout.Button($"現在ブランチを origin/{m_currentBranch} で上書き（reset --hard）", s_btnDanger))
        {
            if (ConfirmDanger($"現在ブランチ {m_currentBranch} を origin/{m_currentBranch} の状態に強制上書きします。\n\n未push のコミット・未commitの変更は失われます。"))
            {
                ExecuteWithSafety(() =>
                {
                    var f = RunGit("fetch origin");
                    var r = RunGit($"reset --hard origin/{m_currentBranch}");
                    return f + "\n" + r;
                }, $"reset --hard origin/{m_currentBranch}");
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("▼ 共有ブランチ更新（元のブランチに自動復帰）", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("develop を更新", s_btnPrimary))
        {
            if (ConfirmDanger("ローカル develop を origin/develop で上書きします。\nそのあと現在のブランチに戻ります。"))
                UpdateBranchAndReturn("develop");
        }
        if (GUILayout.Button("main を更新", s_btnPrimary))
        {
            if (ConfirmDanger("ローカル main を origin/main で上書きします。\nそのあと現在のブランチに戻ります。"))
                UpdateBranchAndReturn("main");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("▼ 別ブランチで上書き（高度）", EditorStyles.boldLabel);

        if (m_remoteBranches.Count > 0)
        {
            m_selectedRemoteIndex = Mathf.Clamp(m_selectedRemoteIndex, 0, m_remoteBranches.Count - 1);
            m_selectedRemoteIndex = EditorGUILayout.Popup("ベースブランチ", m_selectedRemoteIndex, m_remoteBranches.ToArray());

            string target = m_remoteBranches[m_selectedRemoteIndex];
            if (GUILayout.Button($"現在 ({m_currentBranch}) を {target} で上書き", s_btnDanger))
            {
                if (ConfirmDanger($"現在ブランチ {m_currentBranch} の内容を {target} の状態で完全に上書きします。\n\nブランチの内容が別物になります。"))
                {
                    ExecuteWithSafety(() =>
                    {
                        var f = RunGit("fetch origin");
                        var r = RunGit($"reset --hard {target}");
                        return f + "\n" + r;
                    }, $"reset --hard {target}");
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("リモートブランチ一覧が取得できていません。「状態更新」を押してください。", MessageType.Info);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("▼ ログ出力", EditorStyles.boldLabel);
        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos, GUILayout.MinHeight(100));
        EditorGUILayout.TextArea(m_lastOutput, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void UpdateBranchAndReturn(string branch)
    {
        ExecuteWithSafety(() =>
        {
            var fetch = RunGit("fetch origin");
            var dirty = RunGit("status --porcelain").Trim();
            // 出力に "$ git ..." の echo 部分が含まれるので、コマンド行を除いた残りで判定
            var statusOnly = string.Join("\n", dirty.Split('\n').Where(l => !l.StartsWith("$ git "))).Trim();
            if (!string.IsNullOrEmpty(statusOnly))
            {
                return fetch + "\n[ABORT] 未コミットの変更があります。先にコミットまたは破棄してください:\n" + statusOnly;
            }
            var co = RunGit($"checkout {branch}");
            var reset = RunGit($"reset --hard origin/{branch}");
            var back = RunGit($"checkout {m_currentBranch}");
            return $"{fetch}\n{co}\n{reset}\n{back}";
        }, $"update {branch}");
    }

    bool ConfirmDanger(string msg)
    {
        return EditorUtility.DisplayDialog("確認: 破壊的な git 操作", msg, "実行", "キャンセル");
    }

    /// <summary>シーン保存 → 任意の git 操作 → AssetDatabase.Refresh() を順番に実行する。</summary>
    void ExecuteWithSafety(Func<string> action, string label)
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        string output;
        try { output = action(); }
        catch (Exception ex) { output = "[EXCEPTION] " + ex.Message; }

        AssetDatabase.Refresh();
        RefreshState();

        m_lastOutput = $"=== {label} ===\n{output}\n";
        Repaint();
        Debug.Log($"[GitSync] {label}\n{output}");
    }

    void RefreshState()
    {
        m_currentBranch = ParseGitOutput(RunGit("rev-parse --abbrev-ref HEAD")).Trim();

        var raw = ParseGitOutput(RunGit("branch -r --format=%(refname:short)"));
        m_remoteBranches = raw.Split('\n')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s) && !s.Contains("HEAD"))
            .Distinct()
            .OrderBy(s => s)
            .ToList();
    }

    /// <summary>RunGit() の出力先頭の "$ git ..." 行を除いた本文だけ返す。</summary>
    static string ParseGitOutput(string raw)
    {
        var lines = raw.Split('\n');
        return string.Join("\n", lines.Where(l => !l.StartsWith("$ git ") && !l.StartsWith("[stderr]")));
    }

    string RunGit(string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                string body = stdout;
                if (!string.IsNullOrEmpty(stderr)) body += "[stderr] " + stderr;
                return $"$ git {args}\n{body}";
            }
        }
        catch (Exception ex)
        {
            return $"$ git {args}\n[git failed] {ex.Message}";
        }
    }
}
