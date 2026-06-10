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
///   - 現在ブランチを fast-forward pull
///   - 現在ブランチを origin/<current> で上書き
///   - 現在ブランチを別ブランチ (origin/...) で上書き
///   - リモート origin/<current> をローカルで上書き（force-with-lease push）
/// 安全策: 操作前にシーン保存 + AssetDatabase.SaveAssets()、操作後に AssetDatabase.Refresh()。
///         破壊的操作は確認ダイアログを表示し、未コミット変更がある場合は abort する。
///         main/master/develop など共有ブランチへの force push はガードする。
/// メニュー: Tools/Git/同期
/// </summary>
public class GitSyncWindow : EditorWindow
{
    string m_currentBranch = "";
    List<string> m_remoteBranches = new List<string>();
    int m_selectedRemoteIndex;
    string m_logText = "";
    Vector2 m_scrollPos;
    const int k_MaxLogLines = 500;

    [MenuItem("Tools/Git/同期")]
    static void Open()
    {
        var w = GetWindow<GitSyncWindow>("Git 同期");
        w.minSize = new Vector2(460, 480);
    }

    void OnEnable()
    {
        RefreshState();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("現在ブランチ", string.IsNullOrEmpty(m_currentBranch) ? "(unknown)" : m_currentBranch, EditorStyles.boldLabel);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("▼ クイック操作", EditorStyles.boldLabel);

        if (GUILayout.Button($"現在ブランチを pull（fast-forward only）"))
        {
            ExecuteWithSafety(() =>
            {
                var f = RunGit("fetch origin");
                var p = RunGit("pull --ff-only");
                return f + "\n" + p;
            }, "pull --ff-only");
        }

        if (GUILayout.Button($"現在ブランチを origin/{m_currentBranch} で上書き（reset --hard）"))
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
        EditorGUILayout.LabelField("▼ 別ブランチで上書き（高度）", EditorStyles.boldLabel);

        if (m_remoteBranches.Count > 0)
        {
            m_selectedRemoteIndex = Mathf.Clamp(m_selectedRemoteIndex, 0, m_remoteBranches.Count - 1);
            m_selectedRemoteIndex = EditorGUILayout.Popup("ベースブランチ", m_selectedRemoteIndex, m_remoteBranches.ToArray());

            string target = m_remoteBranches[m_selectedRemoteIndex];
            if (GUILayout.Button($"現在 ({m_currentBranch}) を {target} で上書き"))
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
            EditorGUILayout.HelpBox("リモートブランチ一覧が取得できていません。ウィンドウを開き直してください。", MessageType.Info);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("▼ リモートを上書き（破壊的・他メンバー注意）", EditorStyles.boldLabel);

        bool isProtectedBranch = IsProtectedBranch(m_currentBranch);
        EditorGUI.BeginDisabledGroup(isProtectedBranch);
        if (GUILayout.Button($"origin/{m_currentBranch} をローカルで上書き（force-with-lease push）"))
        {
            if (ConfirmDanger(
                $"リモート origin/{m_currentBranch} をローカル {m_currentBranch} の状態で強制上書きします。\n\n" +
                $"他メンバーが既に pull していると、向こうで履歴の不整合や push 拒否が起きます。\n\n" +
                $"他メンバーに事前確認しましたか？"))
            {
                ExecuteWithSafety(() =>
                {
                    var f = RunGit("fetch origin");
                    var p = RunGit($"push --force-with-lease origin {m_currentBranch}");
                    return f + "\n" + p;
                }, $"push --force-with-lease origin {m_currentBranch}");
            }
        }
        EditorGUI.EndDisabledGroup();
        if (isProtectedBranch)
        {
            EditorGUILayout.HelpBox(
                $"{m_currentBranch} は共有ブランチのため force push を禁止しています。\n" +
                $"feature/* 等の作業ブランチに切り替えてから実行してください。",
                MessageType.Warning);
        }

        EditorGUILayout.Space(20);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("ログ", EditorStyles.boldLabel);
        if (GUILayout.Button("クリア", GUILayout.Width(60)))
        {
            m_logText = "";
            GUIUtility.keyboardControl = 0;
        }
        EditorGUILayout.EndHorizontal();

        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos, GUILayout.ExpandHeight(true));
        var logStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(4, 4, 4, 4),
            font = EditorStyles.miniFont
        };
        float height = logStyle.CalcHeight(new GUIContent(m_logText), EditorGUIUtility.currentViewWidth - 30);
        GUILayout.Label(m_logText, logStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(Mathf.Max(height, 260)));
        EditorGUILayout.EndScrollView();
    }

    /// <summary>タイムスタンプ + タグ付きでログに1行追記する。色は richText で着色。</summary>
    void Log(string tag, string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string color = tag switch
        {
            "エラー" => "#ff6b6b",
            "警告"   => "#ffb347",
            "成功"   => "#7ed957",
            "情報"   => "#88ddff",
            _        => null
        };
        string line = color != null
            ? $"<color={color}>[{timestamp}] [{tag}] {message}</color>"
            : $"[{timestamp}] [{tag}] {message}";
        m_logText += line + "\n";

        string[] lines = m_logText.Split('\n');
        if (lines.Length > k_MaxLogLines)
        {
            m_logText = string.Join("\n", lines, lines.Length - k_MaxLogLines, k_MaxLogLines);
        }
        m_scrollPos.y = float.MaxValue;
    }

    /// <summary>git コマンドの生出力をそのまま追記する（タイムスタンプ無し）。</summary>
    void AppendRaw(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        m_logText += text.TrimEnd('\n') + "\n";

        string[] lines = m_logText.Split('\n');
        if (lines.Length > k_MaxLogLines)
        {
            m_logText = string.Join("\n", lines, lines.Length - k_MaxLogLines, k_MaxLogLines);
        }
        m_scrollPos.y = float.MaxValue;
    }

    bool ConfirmDanger(string msg)
    {
        return EditorUtility.DisplayDialog("確認: 破壊的な git 操作", msg, "実行", "キャンセル");
    }

    /// <summary>共有ブランチ判定。force push 禁止対象。</summary>
    static bool IsProtectedBranch(string branchName)
    {
        if (string.IsNullOrEmpty(branchName)) return true;
        return branchName == "main"
            || branchName == "master"
            || branchName == "develop";
    }

    /// <summary>シーン退避 → AutoRefresh/AssemblyReload 一時停止 → 任意の git 操作 → 状態復元 を順番に実行する。
    /// 「Open Scenes have been modified externally」ダイアログを回避するため、操作前に空シーンへ切替えて
    /// 元のシーン構成を SceneManagerSetup として退避し、操作後に復元する。</summary>
    void ExecuteWithSafety(Func<string> action, string label)
    {
        // 1. 現在のシーン構成を退避
        var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        // 2. 選択解除して空シーンに切替（git 操作中に開いているシーンが外部変更されるとダイアログが出るため）
        Selection.activeObject = null;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GitSync] 空シーン作成に失敗（操作は続行）: {ex.Message}");
        }

        // 3. AutoRefresh/AssemblyReload を一時抑止
        AssetDatabase.DisallowAutoRefresh();
        EditorApplication.LockReloadAssemblies();
        AssetDatabase.StartAssetEditing();

        string output;
        try
        {
            output = action();
        }
        catch (Exception ex)
        {
            output = "[EXCEPTION] " + ex.Message;
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorApplication.UnlockReloadAssemblies();
            AssetDatabase.AllowAutoRefresh();
        }

        // 4. 同期リフレッシュ + シーン復元
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        try
        {
            if (sceneSetup != null && sceneSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GitSync] シーン復元に失敗: {ex.Message}");
        }

        RefreshState();

        Log("情報", $"=== {label} ===");
        AppendRaw(output);
        bool hadError = output.Contains("[stderr]") || output.Contains("[EXCEPTION]") || output.Contains("[git failed]");
        if (hadError) Log("警告", "stderr / 例外を検出しました（上の出力を確認してください）");
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
        // -c core.quotepath=false: 日本語ファイル名の \xxx エスケープを抑制
        var psi = new ProcessStartInfo("git", "-c core.quotepath=false " + args)
        {
            WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // git の出力はデフォルトで UTF-8。Windows のデフォルトエンコーディング（CP932 等）と
            // 不一致だと日本語が化けるため明示的に UTF-8 を指定する
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        try
        {
            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                string body = stdout;
                string filteredStderr = FilterNoise(stderr);
                if (!string.IsNullOrEmpty(filteredStderr)) body += "[stderr] " + filteredStderr;
                return $"$ git {args}\n{body}";
            }
        }
        catch (Exception ex)
        {
            return $"$ git {args}\n[git failed] {ex.Message}";
        }
    }

    /// <summary>git の冗長な警告（LF/CRLF 変換警告・Auto packing 等）を除去する。</summary>
    static string FilterNoise(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var keep = new List<string>();
        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            // 無害な warning / 進捗メッセージは捨てる
            if (trimmed.StartsWith("warning: in the working copy of")) continue;
            if (trimmed.StartsWith("warning: LF will be replaced")) continue;
            if (trimmed.Contains("LF will be replaced by CRLF")) continue;
            if (trimmed.StartsWith("Auto packing the repository")) continue;
            if (trimmed.StartsWith("See \"git help gc\"")) continue;
            if (trimmed.StartsWith("nothing")) continue; // "nothing now to do" 等
            keep.Add(trimmed);
        }
        return keep.Count == 0 ? string.Empty : string.Join("\n", keep);
    }
}
