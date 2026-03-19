using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unity Editor 上で Git Flow 操作を行うエディタ拡張
/// Tools > Git Flow メニューからアクセス
/// </summary>
public class GitFlowWindow : EditorWindow
{
    // ステータス情報
    private string currentBranch = "";
    private string currentCommit = "";
    private int ahead = 0;
    private int behind = 0;
    private int changedFiles = 0;
    private bool remoteExists = false;

    // ブランチ作成
    private string newFeatureName = "";

    // ブランチ切り替え
    private List<string> branches = new List<string>();
    private List<bool> branchIsRemoteOnly = new List<bool>();
    private string[] branchDisplayNames = new string[0];
    private int selectedBranchIndex = 0;

    // ログ
    private string logText = "";
    private Vector2 logScroll;

    // リポジトリルート
    private string repoRoot;

    // 環境チェック結果
    private bool hasGitRepo = false;
    private bool hasOrigin = false;
    private bool hasUserConfig = false;
    private string envError = "";

    // origin 登録用
    private string newOriginUrl = "";

    // git user 設定用
    private string inputUserName = "";
    private string inputUserEmail = "";

    // ========================================
    // 操作パイプライン（状態機械）
    // ========================================

    private enum OperationStage
    {
        Idle,
        Preparing,              // シーン保存・退避・ロック・AutoCommit
        GitRunning,             // RunStepAsync 実行中
        PostGitRefresh,         // enter: Unlock + Refresh
        WaitingForRefresh,      // isCompiling/isUpdating 待ち
        RestoringScenes,        // enter: TryRestoreSceneSetup
        WaitingForSceneRestore, // isCompiling/isUpdating 待ち
        Finalizing,             // ClearFlag, RefreshStatus, etc.
        Completed               // FinalizeOperation済み
    }

    private class OperationContext
    {
        public string Title;
        public SceneSetup[] SceneSetup;
        public OperationStage Stage = OperationStage.Idle;
        public bool ClearConsoleOnFinish;  // ブランチ切り替え系のみ true
    }

    private static class BranchPolicy
    {
        public static bool IsFullyProtected(string branch) => branch == "main";
        public static bool IsWarnTarget(string branch) => branch == "main" || branch == "develop";
    }

    [Serializable]
    private class SerializableSceneEntry
    {
        public string path;
        public bool isLoaded;
        public bool isActive;
        public bool isSubScene;
    }

    [Serializable]
    private class SerializableSceneSetup
    {
        public SerializableSceneEntry[] entries;
    }

    private static class PipelineSessionKeys
    {
        public const string Stage          = "GitFlowWindow.Pipeline.Stage";
        public const string IsRunning      = "GitFlowWindow.Pipeline.IsRunning";
        public const string Title          = "GitFlowWindow.Pipeline.Title";
        public const string ClearConsole   = "GitFlowWindow.Pipeline.ClearConsole";
        public const string SceneSetupJson = "GitFlowWindow.Pipeline.SceneSetupJson";
        public const string StageEntered   = "GitFlowWindow.Pipeline.StageEntered";
    }

    // 現在の操作コンテキスト（操作中のみ非null）
    private OperationContext currentOp;

    // 操作中フラグ（連打防止・UI無効化）
    private bool isOperationRunning = false;

    // アセンブリリロード制御
    private bool isAssemblyReloadLocked = false;

    // 状態機械: 各ステージの初回実行フラグ（enter action の二重実行防止）
    private bool stageEntered;

    // ドメインリロード検出用（beforeAssemblyReload → OnDisable の順で呼ばれる）
    private bool isAboutToReloadAssemblies;

    // フォーカス復帰時のシーンフラグクリア要求
    private bool pendingFocusClearFlag;

    // タイムスタンプ復元用（ドメインリロード抑制）
    private Dictionary<string, (byte[] hash, DateTime lastWrite)> savedScriptTimestamps;

    // ウィンドウ全体のスクロール
    private Vector2 mainScroll;

    // 非同期プロセス管理
    private Process activeProcess;
    private int activePercent;
    private string activeOpTitle;
    private string activeStepName;
    private int activeStepNum;
    private int activeTotalSteps;
    private float activeBaseProgress;
    private float activeStepSize;
    private object activeOutputLock;
    private StringBuilder activeStdout;
    private StringBuilder activeStderr;
    private Action<int, string> activeOnComplete;
    private double lastPercentTime;
    private bool activeHasRealProgress;
    private int activeRealPercent;
    private string activeRealPhase;

    // タイムアウト検出用（idle状態の継続を監視）
    private double idleStartTime;
    private bool wasIdleLastFrame;
    private const double IdleTimeoutSeconds = 30.0;

    [MenuItem("Tools/Git Flow")]
    public static void ShowWindow()
    {
        GetWindow<GitFlowWindow>("Git Flow");
    }

    private void OnEnable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        isAboutToReloadAssemblies = false;
        repoRoot = FindGitRoot(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")));
        CheckEnvironment();
        if (hasGitRepo && hasOrigin)
        {
            RefreshStatus();
            RefreshBranches();
        }

        // ドメインリロード後のパイプライン状態復元
        if (TryRestorePipelineState())
        {
            AssetDatabase.AllowAutoRefresh();
            isAssemblyReloadLocked = false;
            Log("情報", "パイプラインを復元しました。処理を継続します...");
        }
    }

    private void OnBeforeAssemblyReload()
    {
        isAboutToReloadAssemblies = true;
    }

    private void OnDisable()
    {
        EditorUtility.ClearProgressBar();
        EditorApplication.update -= OnEditorUpdate;
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        bool processWasKilled = false;
        if (activeProcess != null)
        {
            try { if (!activeProcess.HasExited) { activeProcess.Kill(); processWasKilled = true; } } catch { }
            activeProcess.Dispose();
            activeProcess = null;
            activeOnComplete = null;
        }

        // ドメインリロード中は状態を保持（OnEnable で復元される）
        // beforeAssemblyReload フラグ + isCompiling/isUpdating の併用で誤判定を防ぐ
        // Kill後は必ずリセット（プロセスKillはドメインリロードではない）
        bool isDomainReload = !processWasKilled && isOperationRunning &&
            (isAboutToReloadAssemblies || EditorApplication.isCompiling || EditorApplication.isUpdating);
        if (!isDomainReload)
        {
            UnlockAssemblyReloadIfLocked();
            currentOp = null;
            isOperationRunning = false;
            ClearPipelineState();
        }
    }

    /// <summary>
    /// 指定ディレクトリから上方向に .git ディレクトリを探し、リポジトリルートを返す
    /// </summary>
    private string FindGitRoot(string startDir)
    {
        string dir = startDir;
        while (!string.IsNullOrEmpty(dir))
        {
            if (System.IO.Directory.Exists(System.IO.Path.Combine(dir, ".git")) || System.IO.File.Exists(System.IO.Path.Combine(dir, ".git")))
            {
                return dir;
            }
            string parent = System.IO.Directory.GetParent(dir)?.FullName;
            if (parent == dir) break;
            dir = parent;
        }
        return startDir;
    }

    /// <summary>
    /// Git リポジトリと origin リモートの存在を確認する
    /// </summary>
    private void CheckEnvironment()
    {
        envError = "";
        hasGitRepo = false;
        hasOrigin = false;

        if (!System.IO.Directory.Exists(System.IO.Path.Combine(repoRoot, ".git")) && !System.IO.File.Exists(System.IO.Path.Combine(repoRoot, ".git")))
        {
            envError = ".git ディレクトリが見つかりません。\nこのフォルダは Git リポジトリではありません。";
            return;
        }
        hasGitRepo = true;

        var (code, output) = RunGit("remote get-url origin");
        if (code != 0 || string.IsNullOrEmpty(output))
        {
            envError = "リモート origin が設定されていません。\n以下のコマンドでリモートを設定してください:\n  git remote add origin <リポジトリURL>";
            return;
        }
        hasOrigin = true;

        // user.name / user.email のチェック
        hasUserConfig = false;
        var (nameCode, userName) = RunGit("config user.name");
        var (emailCode, userEmail) = RunGit("config user.email");
        if (nameCode == 0 && !string.IsNullOrEmpty(userName?.Trim()) &&
            emailCode == 0 && !string.IsNullOrEmpty(userEmail?.Trim()))
        {
            hasUserConfig = true;
        }
    }

    private void OnGUI()
    {
        // ウィンドウ全体をスクロール可能にする
        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

        // ── ステータス表示 ──
        GUILayout.Label("Git Flow", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 環境エラー表示
        if (!hasGitRepo || !hasOrigin)
        {
            EditorGUILayout.HelpBox(envError, MessageType.Error);

            // origin 未設定の場合、エディタ上で登録できるUIを表示
            if (hasGitRepo && !hasOrigin)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("リモート origin の登録", EditorStyles.boldLabel);
                newOriginUrl = EditorGUILayout.TextField("リポジトリURL", newOriginUrl);
                if (GUILayout.Button("origin を登録"))
                {
                    if (!string.IsNullOrEmpty(newOriginUrl))
                    {
                        // URL バリデーション（コマンドインジェクション防止）
                        string trimmedUrl = newOriginUrl.Trim();
                        if (trimmedUrl.Contains(" ") || trimmedUrl.Contains(";") ||
                            trimmedUrl.Contains("&") || trimmedUrl.Contains("|") ||
                            trimmedUrl.Contains("`") || trimmedUrl.Contains("$"))
                        {
                            Log("エラー", "無効な URL です。特殊文字を含めないでください。");
                        }
                        else
                        {
                            var (code, output) = RunGit($"remote add origin {trimmedUrl}");
                            if (code == 0)
                            {
                                Log("成功", $"origin を登録しました: {trimmedUrl}");
                                newOriginUrl = "";
                                CheckEnvironment();
                                if (hasGitRepo && hasOrigin)
                                {
                                    RefreshStatus();
                                    RefreshBranches();
                                }
                            }
                            else
                            {
                                Log("エラー", $"origin の登録に失敗しました: {output}");
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("再チェック"))
            {
                CheckEnvironment();
                if (hasGitRepo && hasOrigin)
                {
                    RefreshStatus();
                    RefreshBranches();
                }
            }

            EditorGUILayout.EndScrollView();
            return;
        }

        // git user.name / user.email 未設定時の設定UI
        if (!hasUserConfig)
        {
            EditorGUILayout.HelpBox(
                "Git のユーザー情報が設定されていません。\nコミットするには名前とメールアドレスが必要です。\nGitHub に登録しているメールアドレスを入力してください。",
                MessageType.Warning);
            EditorGUILayout.Space();
            inputUserName = EditorGUILayout.TextField("名前", inputUserName);
            inputUserEmail = EditorGUILayout.TextField("メールアドレス", inputUserEmail);
            if (GUILayout.Button("保存"))
            {
                string trimName = inputUserName?.Trim();
                string trimEmail = inputUserEmail?.Trim();
                if (string.IsNullOrEmpty(trimName) || string.IsNullOrEmpty(trimEmail))
                {
                    Log("エラー", "名前とメールアドレスの両方を入力してください。");
                }
                else if (trimName.Contains("\"") || trimEmail.Contains("\"") ||
                         trimName.Contains(";") || trimEmail.Contains(";"))
                {
                    Log("エラー", "無効な文字が含まれています。");
                }
                else
                {
                    RunGit($"config user.name \"{trimName}\"");
                    RunGit($"config user.email \"{trimEmail}\"");
                    Log("成功", $"Git ユーザーを設定しました: {trimName} <{trimEmail}>");
                    hasUserConfig = true;
                    RefreshStatus();
                    RefreshBranches();
                }
            }
            EditorGUILayout.Space();
        }

        EditorGUILayout.LabelField("ブランチ", currentBranch);

        // main / develop ブランチ警告（赤文字）
        if (currentBranch == "main")
        {
            var warnStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField(
                "<color=#FF6B6B>⚠ このブランチは main（リリース用）です。直接作業しないでください。</color>",
                warnStyle);
        }
        else if (currentBranch == "develop")
        {
            var warnStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField(
                "<color=#FF6B6B>⚠ このブランチは develop（統合用）です。feature/ ブランチで作業してください。</color>",
                warnStyle);
        }

        EditorGUILayout.LabelField("コミット", currentCommit);

        if (remoteExists)
        {
            EditorGUILayout.LabelField("状態", $"↑{ahead} 未プッシュ / ↓{behind} 未取得");
        }
        else
        {
            EditorGUILayout.LabelField("状態", "リモートにブランチなし（ローカル専用）");
        }

        EditorGUILayout.LabelField("変更", $"{changedFiles} ファイル");

        if (GUILayout.Button("更新"))
        {
            CheckEnvironment();
            RefreshStatus();
            RefreshBranches();
        }

        EditorGUILayout.Space();

        // ── 操作 ──
        GUILayout.Label("操作", EditorStyles.boldLabel);

        // 操作中はボタンを無効化
        GUI.enabled = !isOperationRunning;

        if (GUILayout.Button("最新を取得 (Fetch)"))
        {
            PerformFetch();
        }

        if (GUILayout.Button("プッシュ (Push)"))
        {
            PerformPush();
        }

        GUI.enabled = true;

        if (isOperationRunning)
        {
            EditorGUILayout.HelpBox("処理中...", MessageType.Info);
        }

        EditorGUILayout.Space();

        // ── ブランチ ──
        GUILayout.Label("ブランチ", EditorStyles.boldLabel);

        newFeatureName = EditorGUILayout.TextField("ブランチ名", newFeatureName);
        EditorGUILayout.LabelField("ベース", $"{currentBranch}（現在のブランチ）");

        GUI.enabled = !isOperationRunning;

        if (GUILayout.Button("新規ブランチ作成"))
        {
            CreateNewFeature();
        }

        GUI.enabled = true;

        EditorGUILayout.Space();

        if (branchDisplayNames.Length > 0)
        {
            selectedBranchIndex = EditorGUILayout.Popup("切り替え先", selectedBranchIndex, branchDisplayNames);

            GUI.enabled = !isOperationRunning;

            if (GUILayout.Button("ブランチ切り替え"))
            {
                SwitchBranch();
            }

            GUI.enabled = true;
        }

        EditorGUILayout.Space();

        // ── ログ ──
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("ログ", EditorStyles.boldLabel);
        if (GUILayout.Button("クリア", GUILayout.Width(60)))
        {
            logText = "";
            GUIUtility.keyboardControl = 0;
        }
        EditorGUILayout.EndHorizontal();

        logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.ExpandHeight(true));
        var logStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(4, 4, 4, 4),
            font = EditorStyles.miniFont
        };
        float height = logStyle.CalcHeight(new GUIContent(logText), EditorGUIUtility.currentViewWidth - 30);
        GUILayout.Label(logText, logStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(Mathf.Max(height, 200)));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndScrollView();
    }

    // ========================================
    // Git コマンド実行
    // ========================================

    /// <summary>
    /// git コマンドを同期実行し、終了コードと出力を返す（軽量コマンド用）
    /// </summary>
    private (int exitCode, string output) RunGit(string args)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.Environment["LC_MESSAGES"] = "ja_JP.UTF-8";

            using (Process process = Process.Start(psi))
            {
                // stdout と stderr を並列読み取りしてデッドロックを防止
                string stderr = null;
                var stderrTask = System.Threading.Tasks.Task.Run(() => process.StandardError.ReadToEnd());
                string stdout = process.StandardOutput.ReadToEnd();
                stderr = stderrTask.Result;
                process.WaitForExit();

                // 成功時: stdout のみ（パース安全）
                // 失敗時: stdout + stderr（エラー情報を含む）
                if (process.ExitCode != 0 && !string.IsNullOrEmpty(stderr))
                {
                    stdout += (string.IsNullOrEmpty(stdout) ? "" : "\n") + stderr;
                }

                return (process.ExitCode, stdout.TrimEnd());
            }
        }
        catch (Exception e)
        {
            return (-1, $"Git 実行エラー: {e.Message}");
        }
    }

    // ========================================
    // ステータス更新
    // ========================================

    private void RefreshStatus()
    {
        // 残留 rebase セッションを検出して自動 abort
        string rebaseMerge = System.IO.Path.Combine(repoRoot, ".git", "rebase-merge");
        string rebaseApply = System.IO.Path.Combine(repoRoot, ".git", "rebase-apply");
        if (System.IO.Directory.Exists(rebaseMerge) || System.IO.Directory.Exists(rebaseApply))
        {
            Log("警告", "残留した rebase セッションを検出しました。自動で abort します。");
            RunGit("rebase --abort");
        }

        var (code, output) = RunGit("symbolic-ref --short HEAD");
        currentBranch = code == 0 ? output : "(detached HEAD)";

        (code, output) = RunGit("rev-parse --short HEAD");
        currentCommit = code == 0 ? output : "-";

        (code, output) = RunGit("status --porcelain");
        if (code == 0 && !string.IsNullOrEmpty(output))
        {
            changedFiles = output.Split('\n').Length;
        }
        else
        {
            changedFiles = 0;
        }

        ahead = 0;
        behind = 0;
        remoteExists = false;

        if (currentBranch != "(detached HEAD)")
        {
            (code, _) = RunGit($"rev-parse --verify origin/{currentBranch}");
            if (code == 0)
            {
                remoteExists = true;

                (code, output) = RunGit($"rev-list --count origin/{currentBranch}..HEAD");
                if (code == 0) int.TryParse(output.Trim(), out ahead);

                (code, output) = RunGit($"rev-list --count HEAD..origin/{currentBranch}");
                if (code == 0) int.TryParse(output.Trim(), out behind);
            }
        }

        Repaint();
    }

    /// <summary>
    /// main / develop にいるとき Editor Console をクリアしてから警告を出す。
    /// 古いブランチの警告が残留しないようにする。
    /// </summary>
    private void WarnIfProtectedBranch()
    {
        if (!BranchPolicy.IsWarnTarget(currentBranch)) return;
        ClearEditorConsole();
        if (currentBranch == "main")
        {
            UnityEngine.Debug.LogError("[Git Flow] ⚠ 現在 main ブランチ（リリース用）にいます。直接作業しないでください。");
        }
        else if (currentBranch == "develop")
        {
            UnityEngine.Debug.LogWarning("[Git Flow] ⚠ 現在 develop ブランチ（統合用）にいます。feature/ ブランチで作業してください。");
        }
    }

    private static void ClearEditorConsole()
    {
        var logEntries = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
        logEntries?.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.Invoke(null, null);
    }

    // ========================================
    // ブランチリスト更新
    // ========================================

    private void RefreshBranches()
    {
        // 現在選択中のブランチ名を保持
        string previousSelection = (selectedBranchIndex >= 0 && selectedBranchIndex < branches.Count)
            ? branches[selectedBranchIndex]
            : null;

        branches.Clear();
        branchIsRemoteOnly.Clear();

        // ローカルブランチ
        var (code, output) = RunGit("branch --format=%(refname:short)");
        HashSet<string> localSet = new HashSet<string>();
        if (code == 0 && !string.IsNullOrEmpty(output))
        {
            foreach (string line in output.Split('\n'))
            {
                string name = line.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    localSet.Add(name);
                    branches.Add(name);
                    branchIsRemoteOnly.Add(false);
                }
            }
        }

        // リモートブランチ（ローカルにないもの）
        (code, output) = RunGit("branch -r --format=%(refname:short)");
        if (code == 0 && !string.IsNullOrEmpty(output))
        {
            foreach (string line in output.Split('\n'))
            {
                string name = line.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                if (name.Contains("origin/HEAD")) continue;

                string localName = name.StartsWith("origin/") ? name.Substring("origin/".Length) : name;
                if (!localSet.Contains(localName))
                {
                    branches.Add(localName);
                    branchIsRemoteOnly.Add(true);
                }
            }
        }

        // main → develop → feature/ の順に並び替え
        branches.Sort((a, b) =>
        {
            int OrderOf(string name)
            {
                if (name == "main") return 0;
                if (name == "develop") return 1;
                return 2;
            }
            int cmp = OrderOf(a).CompareTo(OrderOf(b));
            return cmp != 0 ? cmp : string.Compare(a, b, StringComparison.Ordinal);
        });
        // branchIsRemoteOnly も同じ順序に並び替え
        var remoteMap = new System.Collections.Generic.Dictionary<string, bool>();
        for (int i = 0; i < branches.Count; i++)
            remoteMap[branches[i]] = branchIsRemoteOnly[i];
        branchIsRemoteOnly.Clear();
        foreach (string b in branches)
            branchIsRemoteOnly.Add(remoteMap[b]);

        // ドロップダウン表示名を作成
        branchDisplayNames = new string[branches.Count];
        for (int i = 0; i < branches.Count; i++)
        {
            if (branches[i] == currentBranch)
            {
                int lastSlash = branches[i].LastIndexOf('/');
                if (lastSlash >= 0)
                    branchDisplayNames[i] = $"{branches[i].Substring(0, lastSlash + 1)}★ {branches[i].Substring(lastSlash + 1)}";
                else
                    branchDisplayNames[i] = $"★ {branches[i]}";
            }
            else if (branchIsRemoteOnly[i])
            {
                branchDisplayNames[i] = $"{branches[i]} (リモートのみ)";
            }
            else
            {
                branchDisplayNames[i] = branches[i];
            }
        }

        // 以前の選択を復元（見つからなければ先頭）
        selectedBranchIndex = 0;
        if (previousSelection != null)
        {
            int idx = branches.IndexOf(previousSelection);
            if (idx >= 0) selectedBranchIndex = idx;
        }

        Repaint();
    }

    // ========================================
    // ログ出力
    // ========================================

    private const int MaxLogLines = 500;

    private void Log(string tag, string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string color;
        switch (tag)
        {
            case "エラー": color = "#FF6B6B"; break;
            case "警告":   color = "#FFA94D"; break;
            case "成功":   color = "#69DB7C"; break;
            default:       color = null; break;
        }

        if (color != null)
            logText += $"<color={color}>[{timestamp}] [{tag}] {message}</color>\n";
        else
            logText += $"[{timestamp}] [{tag}] {message}\n";

        // ログ行数を制限（古い行を削除）
        string[] lines = logText.Split('\n');
        if (lines.Length > MaxLogLines)
        {
            logText = string.Join("\n", lines, lines.Length - MaxLogLines, MaxLogLines);
        }

        logScroll.y = float.MaxValue;
        Repaint();
    }

    /// <summary>
    /// ログの最終行を上書きする（プログレス表示用）
    /// </summary>
    private void LogReplaceLast(string tag, string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string color;
        switch (tag)
        {
            case "エラー": color = "#FF6B6B"; break;
            case "成功":   color = "#69DB7C"; break;
            default:       color = null; break;
        }

        string newLine;
        if (color != null)
            newLine = $"<color={color}>[{timestamp}] [{tag}] {message}</color>";
        else
            newLine = $"[{timestamp}] [{tag}] {message}";

        int lastNewline = logText.Length > 1 ? logText.LastIndexOf('\n', logText.Length - 2) : -1;
        if (lastNewline >= 0)
        {
            logText = logText.Substring(0, lastNewline + 1) + newLine + "\n";
        }
        else
        {
            logText = newLine + "\n";
        }

        logScroll.y = float.MaxValue;
        Repaint();
    }

    // ========================================
    // 非同期プロセス実行（EditorApplication.update ベース）
    // ========================================

    /// <summary>
    /// EditorApplication.update から毎フレーム呼ばれる。
    /// 1. フォーカスクリア用ポーリング（独立）
    /// 2. git プロセス監視（GitRunning 用）
    /// 3. パイプライン状態機械（PostGitRefresh 以降）
    /// 4. タイムアウト（最後の保険、30秒）
    /// </summary>
    private void OnEditorUpdate()
    {
        // 1. フォーカスクリア用ポーリング（独立）
        if (pendingFocusClearFlag && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            pendingFocusClearFlag = false;
            TryClearChangedOnDiskFlag();
        }

        // 2. プロセス監視（GitRunning 用、既存ロジック維持）
        if (activeProcess != null)
        {
            if (!activeProcess.HasExited)
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - lastPercentTime >= 0.03)
                {
                    lastPercentTime = now;

                    bool cancelled = false;
                    if (activeHasRealProgress)
                    {
                        int pct = activeRealPercent;
                        string phase = activeRealPhase ?? activeStepName;
                        float barValue = activeBaseProgress + activeStepSize * (pct / 100f);
                        cancelled = EditorUtility.DisplayCancelableProgressBar(activeOpTitle, $"{phase} ({pct}%)", barValue);
                        LogReplaceLast("情報", $"[{activeStepNum}/{activeTotalSteps}] {phase} ... {pct}%");
                    }
                    else
                    {
                        if (activePercent < 99)
                        {
                            activePercent++;
                            float barValue = activeBaseProgress + activeStepSize * (activePercent / 100f);
                            cancelled = EditorUtility.DisplayCancelableProgressBar(activeOpTitle, $"{activeStepName} ({activePercent}%)", barValue);
                            LogReplaceLast("情報", $"[{activeStepNum}/{activeTotalSteps}] {activeStepName} ... {activePercent}%");
                        }
                    }

                    if (cancelled)
                    {
                        try { if (!activeProcess.HasExited) activeProcess.Kill(); } catch { }
                        activeProcess.Dispose();
                        activeProcess = null;
                        activeOnComplete = null;
                        Log("警告", "操作がキャンセルされました。");
                        if (currentOp != null)
                            FinalizeOperation(currentOp);
                        return;
                    }
                }
            }
            else
            {
                activeProcess.WaitForExit();

                EditorUtility.DisplayProgressBar(activeOpTitle, $"{activeStepName} (100%)", activeBaseProgress + activeStepSize);
                LogReplaceLast("情報", $"[{activeStepNum}/{activeTotalSteps}] {activeStepName} ... 100%");

                string combined, err;
                lock (activeOutputLock)
                {
                    combined = activeStdout.ToString();
                    err = activeStderr.ToString();
                }
                if (!string.IsNullOrEmpty(err))
                {
                    combined += (string.IsNullOrEmpty(combined) ? "" : "\n") + err;
                }

                int exitCode = activeProcess.ExitCode;
                activeProcess.Dispose();
                activeProcess = null;

                activeOnComplete?.Invoke(exitCode, combined.TrimEnd());
            }
            return;
        }

        // 3. パイプライン状態機械
        if (currentOp != null && isOperationRunning)
        {
            switch (currentOp.Stage)
            {
                case OperationStage.PostGitRefresh:
                    if (!stageEntered)
                    {
                        MarkStageEntered();
                        Log("情報", "プロジェクトを更新しています...");
                        UnlockAssemblyReloadIfLocked();
                        AssetDatabase.Refresh();
                    }
                    TransitionTo(OperationStage.WaitingForRefresh);
                    return;

                case OperationStage.WaitingForRefresh:
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                        break;
                    TransitionTo(OperationStage.RestoringScenes);
                    return;

                case OperationStage.RestoringScenes:
                    if (!stageEntered)
                    {
                        MarkStageEntered();
                        TryRestoreSceneSetup(currentOp);
                    }
                    TransitionTo(OperationStage.WaitingForSceneRestore);
                    return;

                case OperationStage.WaitingForSceneRestore:
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                        break;
                    TransitionTo(OperationStage.Finalizing);
                    return;

                case OperationStage.Finalizing:
                    if (!stageEntered)
                    {
                        MarkStageEntered();
                        Log("情報", "プロジェクトの更新が完了しました。");
                        FinalizeOperation(currentOp);
                    }
                    return;
            }
        }

        // 4. タイムアウト（最後の保険、30秒）
        if (isOperationRunning && activeProcess == null)
        {
            bool isIdle = !EditorApplication.isCompiling && !EditorApplication.isUpdating;
            if (isIdle)
            {
                if (!wasIdleLastFrame)
                {
                    wasIdleLastFrame = true;
                    idleStartTime = EditorApplication.timeSinceStartup;
                }
                else if (EditorApplication.timeSinceStartup - idleStartTime > IdleTimeoutSeconds)
                {
                    Log("警告", "操作がタイムアウトしました。復帰処理を実行します。");
                    if (currentOp != null)
                    {
                        FinalizeOperation(currentOp);
                    }
                    else
                    {
                        isOperationRunning = false;
                        EditorUtility.ClearProgressBar();
                        ClearPipelineState();
                        Repaint();
                    }
                    return;
                }
            }
            else
            {
                wasIdleLastFrame = false;
            }
        }
    }

    /// <summary>
    /// git コマンドを非同期で実行し、プログレスバーとログを更新する。
    /// プロセス完了時に onComplete コールバックが呼ばれる。
    /// </summary>
    private static readonly Regex ProgressRegex = new Regex(@"^([\w\s]+?):\s+(\d+)%", RegexOptions.Compiled);

    private void RunStepAsync(string operationTitle, string stepName, int stepNum, int totalSteps, string gitArgs, Action<int, string> onComplete)
    {
        activeOpTitle = operationTitle;
        activeStepName = stepName;
        activeStepNum = stepNum;
        activeTotalSteps = totalSteps;
        activeBaseProgress = (float)(stepNum - 1) / totalSteps;
        activeStepSize = 1f / totalSteps;
        activePercent = 0;
        activeHasRealProgress = false;
        activeRealPercent = 0;
        activeRealPhase = null;
        activeOutputLock = new object();
        activeStdout = new StringBuilder();
        activeStderr = new StringBuilder();
        activeOnComplete = onComplete;
        lastPercentTime = EditorApplication.timeSinceStartup;

        Log("情報", $"[{stepNum}/{totalSteps}] {stepName} ... 0%");

        // --progress を付与（リダイレクト時でも進捗を出力させる）
        string argsWithProgress = gitArgs;
        if (gitArgs.StartsWith("fetch ") || gitArgs.StartsWith("push ") || gitArgs.Contains("pull "))
        {
            argsWithProgress += " --progress";
        }

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = argsWithProgress,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.Environment["LC_MESSAGES"] = "ja_JP.UTF-8";

            activeProcess = Process.Start(psi);
            activeProcess.OutputDataReceived += (s, e) => { if (e.Data != null) { lock (activeOutputLock) { activeStdout.AppendLine(e.Data); } } };
            activeProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                lock (activeOutputLock) { activeStderr.AppendLine(e.Data); }

                var match = ProgressRegex.Match(e.Data);
                if (match.Success)
                {
                    activeHasRealProgress = true;
                    activeRealPercent = int.Parse(match.Groups[2].Value);
                    activeRealPhase = match.Groups[1].Value.Trim();
                }
            };
            activeProcess.BeginOutputReadLine();
            activeProcess.BeginErrorReadLine();
        }
        catch (Exception e)
        {
            activeProcess = null;
            onComplete?.Invoke(-1, $"Git 実行エラー: {e.Message}");
        }
    }

    /// <summary>
    /// git実行を伴わないステップのプログレス開始表示
    /// </summary>
    private void BeginStep(string operationTitle, string stepName, int stepNum, int totalSteps)
    {
        float baseProgress = (float)(stepNum - 1) / totalSteps;
        EditorUtility.DisplayProgressBar(operationTitle, $"{stepName} (0%)", baseProgress);
        Log("情報", $"[{stepNum}/{totalSteps}] {stepName} ... 0%");
    }

    /// <summary>
    /// git実行を伴わないステップの完了表示
    /// </summary>
    private void EndStep(string operationTitle, string stepName, int stepNum, int totalSteps)
    {
        float progress = (float)stepNum / totalSteps;
        EditorUtility.DisplayProgressBar(operationTitle, $"{stepName} (100%)", progress);
        Log("情報", $"[{stepNum}/{totalSteps}] {stepName} ... 100%");
    }

    // ========================================
    // 冪等ヘルパー（新パイプライン用）
    // ========================================

    /// <summary>
    /// ロック中の場合のみアセンブリリロードを解除する。
    /// 既に解除済みなら何もしない。
    /// </summary>
    private void UnlockAssemblyReloadIfLocked()
    {
        if (isAssemblyReloadLocked)
        {
            RestoreUnchangedScriptTimestamps();
            AssetDatabase.AllowAutoRefresh();
            EditorApplication.UnlockReloadAssemblies();
            isAssemblyReloadLocked = false;
        }
    }

    /// <summary>
    /// シーン構成を復元する。null/空なら何もしない。例外は握りつぶしてログに記録。
    /// 復元後にコンテキストの SceneSetup を null にクリアする。
    /// </summary>
    private void TryRestoreSceneSetup(OperationContext ctx)
    {
        if (ctx == null || ctx.SceneSetup == null || ctx.SceneSetup.Length == 0) return;
        try
        {
            EditorSceneManager.RestoreSceneManagerSetup(ctx.SceneSetup);
            Log("情報", "シーンを復元しました。");
        }
        catch (Exception ex)
        {
            Log("警告", $"シーン復元に失敗しました: {ex.Message}");
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                Log("情報", "デフォルトシーンを開きました。");
            }
            catch (Exception fallbackEx)
            {
                Log("エラー", $"デフォルトシーンのオープンにも失敗しました: {fallbackEx.Message}");
            }
        }
        ctx.SceneSetup = null;
    }

    /// <summary>
    /// ClearSceneChangedOnDiskFlag を安全に実行する。例外は握りつぶす。
    /// </summary>
    private void TryClearChangedOnDiskFlag()
    {
        try { ClearSceneChangedOnDiskFlag(); }
        catch (Exception ex) { Log("警告", $"ClearChangedOnDiskFlag 失敗: {ex.Message}"); }
    }

    /// <summary>
    /// RegisterOneTimeFocusClear を安全に実行する。例外は握りつぶす。
    /// </summary>
    private void TryRegisterFocusClear()
    {
        try { RegisterOneTimeFocusClear(); }
        catch (Exception ex) { Log("警告", $"RegisterFocusClear 失敗: {ex.Message}"); }
    }

    // ========================================
    // 共通後処理（新パイプライン用）
    // ========================================

    /// <summary>
    /// すべての操作パス（正常完了・エラー・タイムアウト）から呼ばれる唯一の出口。
    /// 冪等ヘルパーを使用しているため、どの状態から呼ばれても安全。
    ///
    /// 正常パスでは OnEditorUpdate 状態機械が PostGitRefresh → WaitingForRefresh →
    /// RestoringScenes → WaitingForSceneRestore → Finalizing と遷移し、
    /// シーン復元済みの状態で呼ばれる。
    /// エラー・タイムアウト時は途中ステージからジャンプして呼ばれるため、
    /// 未実行の後処理を冪等ヘルパーで安全に補完する。
    /// </summary>
    private void FinalizeOperation(OperationContext ctx)
    {
        if (ctx == null) return;
        try
        {
            // 1. アセンブリリロード解除（ロック中のみ）
            UnlockAssemblyReloadIfLocked();

            // 2. アセットインポート（エラー・タイムアウト時のみ）
            //    正常パスでは PostGitRefresh で Refresh 済み（WaitingForRefresh 以降）のためスキップ
            if (ctx.Stage < OperationStage.WaitingForRefresh)
            {
                AssetDatabase.Refresh();
            }

            // 3. シーン復元（正常パスでは RestoringScenes で済んでいるので null → スキップ）
            //    エラー・タイムアウト時は未復元のため実行される
            TryRestoreSceneSetup(ctx);

            // 4. シーン変更フラグクリア
            TryClearChangedOnDiskFlag();

            // 5. フォーカス復帰時の1回クリア登録
            TryRegisterFocusClear();

            // 6. ブランチ切り替え系のみコンソールクリア
            if (ctx.ClearConsoleOnFinish)
            {
                ClearEditorConsole();
            }

            // 7. ステータス・ブランチ情報を更新
            RefreshStatus();
            RefreshBranches();

            // 8. 保護ブランチ警告
            WarnIfProtectedBranch();
        }
        catch (Exception ex)
        {
            Log("エラー", $"FinalizeOperation 中に例外: {ex.Message}");
        }
        finally
        {
            // 9. 操作完了: 状態をリセット
            ctx.Stage = OperationStage.Completed;
            currentOp = null;
            isOperationRunning = false;
            EditorUtility.ClearProgressBar();
            ClearPipelineState();
            Repaint();
        }
    }

    /// <summary>
    /// git操作前にファイルロック解放 + 自動リフレッシュ抑制 + アセンブリリロード抑制
    /// </summary>
    private void LockAssemblyReload()
    {
        if (!isAssemblyReloadLocked)
        {
            AssetDatabase.DisallowAutoRefresh();
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.ReleaseCachedFileHandles();
            isAssemblyReloadLocked = true;
            SaveScriptTimestamps();
        }
    }

    // ========================================
    // パイプライン状態永続化（SessionState）
    // ========================================

    /// <summary>
    /// 現在のパイプライン状態を SessionState に保存する。
    /// ドメインリロードを跨いで状態を復元するため。
    /// </summary>
    private void SavePipelineState()
    {
        if (currentOp == null) return;

        SessionState.SetInt(PipelineSessionKeys.Stage, (int)currentOp.Stage);
        SessionState.SetBool(PipelineSessionKeys.IsRunning, isOperationRunning);
        SessionState.SetString(PipelineSessionKeys.Title, currentOp.Title ?? "");
        SessionState.SetBool(PipelineSessionKeys.ClearConsole, currentOp.ClearConsoleOnFinish);
        SessionState.SetBool(PipelineSessionKeys.StageEntered, stageEntered);

        // SceneSetup を JSON 化して保存
        if (currentOp.SceneSetup != null && currentOp.SceneSetup.Length > 0)
        {
            var serializable = new SerializableSceneSetup
            {
                entries = new SerializableSceneEntry[currentOp.SceneSetup.Length]
            };
            for (int i = 0; i < currentOp.SceneSetup.Length; i++)
            {
                serializable.entries[i] = new SerializableSceneEntry
                {
                    path = currentOp.SceneSetup[i].path,
                    isLoaded = currentOp.SceneSetup[i].isLoaded,
                    isActive = currentOp.SceneSetup[i].isActive,
                    isSubScene = currentOp.SceneSetup[i].isSubScene
                };
            }
            SessionState.SetString(PipelineSessionKeys.SceneSetupJson, JsonUtility.ToJson(serializable));
        }
        else
        {
            SessionState.SetString(PipelineSessionKeys.SceneSetupJson, "");
        }
    }

    /// <summary>
    /// SessionState からパイプライン状態を復元する。
    /// ドメインリロード後に OnEnable から呼ばれる。
    /// Preparing/GitRunning はアセンブリリロードがロックされており
    /// ドメインリロードが発生しないため、復元対象は PostGitRefresh 以降のみ。
    /// </summary>
    private bool TryRestorePipelineState()
    {
        if (!SessionState.GetBool(PipelineSessionKeys.IsRunning, false))
            return false;

        var stage = (OperationStage)SessionState.GetInt(PipelineSessionKeys.Stage, (int)OperationStage.Idle);
        if (stage < OperationStage.PostGitRefresh || stage >= OperationStage.Completed)
        {
            // 復元不可ステージの残留データをクリア
            ClearPipelineState();
            return false;
        }

        currentOp = new OperationContext
        {
            Title = SessionState.GetString(PipelineSessionKeys.Title, ""),
            Stage = stage,
            ClearConsoleOnFinish = SessionState.GetBool(PipelineSessionKeys.ClearConsole, false)
        };

        // SceneSetup を JSON から復元
        string json = SessionState.GetString(PipelineSessionKeys.SceneSetupJson, "");
        if (!string.IsNullOrEmpty(json))
        {
            var serializable = JsonUtility.FromJson<SerializableSceneSetup>(json);
            if (serializable?.entries != null)
            {
                currentOp.SceneSetup = new SceneSetup[serializable.entries.Length];
                for (int i = 0; i < serializable.entries.Length; i++)
                {
                    currentOp.SceneSetup[i] = new SceneSetup
                    {
                        path = serializable.entries[i].path,
                        isLoaded = serializable.entries[i].isLoaded,
                        isActive = serializable.entries[i].isActive,
                        isSubScene = serializable.entries[i].isSubScene
                    };
                }
            }
        }

        stageEntered = SessionState.GetBool(PipelineSessionKeys.StageEntered, false);
        isOperationRunning = true;
        wasIdleLastFrame = false;

        return true;
    }

    /// <summary>
    /// SessionState のパイプライン状態をクリアする。
    /// </summary>
    private void ClearPipelineState()
    {
        SessionState.SetInt(PipelineSessionKeys.Stage, (int)OperationStage.Idle);
        SessionState.SetBool(PipelineSessionKeys.IsRunning, false);
        SessionState.SetString(PipelineSessionKeys.Title, "");
        SessionState.SetBool(PipelineSessionKeys.ClearConsole, false);
        SessionState.SetString(PipelineSessionKeys.SceneSetupJson, "");
        SessionState.SetBool(PipelineSessionKeys.StageEntered, false);
    }

    /// <summary>
    /// パイプラインのステージを遷移する。stageEntered をリセットし、状態を保存する。
    /// </summary>
    private void TransitionTo(OperationStage newStage)
    {
        if (currentOp == null) return;
        currentOp.Stage = newStage;
        stageEntered = false;
        wasIdleLastFrame = false;
        SavePipelineState();
    }

    /// <summary>
    /// 現在のステージの enter action を実行済みとしてマークし、状態を保存する。
    /// ドメインリロード後の二重実行を防止する。
    /// </summary>
    private void MarkStageEntered()
    {
        stageEntered = true;
        SavePipelineState();
    }

    // ========================================
    // タイムスタンプ復元（ドメインリロード抑制）
    // ========================================

    /// <summary>
    /// Assets 以下の全 .cs ファイルのハッシュとタイムスタンプを保存する。
    /// git checkout 後に内容が変わっていないファイルのタイムスタンプを復元することで、
    /// Unity の不要なリコンパイル（ドメインリロード）を防ぐ。
    /// </summary>
    private void SaveScriptTimestamps()
    {
        savedScriptTimestamps = new Dictionary<string, (byte[] hash, DateTime lastWrite)>();
        string assetsPath = Application.dataPath;
        try
        {
            foreach (string file in Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories))
            {
                try
                {
                    var hash = ComputeFileHash(file);
                    var lastWrite = File.GetLastWriteTimeUtc(file);
                    savedScriptTimestamps[file] = (hash, lastWrite);
                }
                catch { }
            }
        }
        catch { }
    }

    /// <summary>
    /// 保存済みハッシュと比較し、内容が変わっていないファイルのタイムスタンプを復元する。
    /// </summary>
    private void RestoreUnchangedScriptTimestamps()
    {
        if (savedScriptTimestamps == null) return;
        int restored = 0;
        foreach (var kvp in savedScriptTimestamps)
        {
            try
            {
                if (!File.Exists(kvp.Key)) continue;
                var newHash = ComputeFileHash(kvp.Key);
                if (HashesEqual(kvp.Value.hash, newHash))
                {
                    File.SetLastWriteTimeUtc(kvp.Key, kvp.Value.lastWrite);
                    restored++;
                }
            }
            catch { }
        }
        if (restored > 0)
        {
            Log("情報", $"タイムスタンプを復元しました ({restored} ファイル)");
        }
        savedScriptTimestamps = null;
    }

    private static byte[] ComputeFileHash(string filePath)
    {
        using (var md5 = MD5.Create())
        using (var stream = File.OpenRead(filePath))
        {
            return md5.ComputeHash(stream);
        }
    }

    private static bool HashesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    // ========================================
    // 自動コミット（共通処理）
    // ========================================

    /// <summary>
    /// 未コミット変更を自動コミットする（前処理ゲート）。
    /// 保護ブランチで未コミット変更がある場合は false を返し、呼び出し側に中断を要求する。
    /// </summary>
    /// <returns>true: 続行可, false: 中断すべき（保護ブランチに未コミット変更あり）</returns>
    private bool AutoCommit(string reason)
    {
        var (code, output) = RunGit("status --porcelain");
        if (code != 0)
        {
            Log("エラー", $"git status の実行に失敗しました: {output}");
            return false;
        }
        bool hasChanges = !string.IsNullOrEmpty(output);

        // 保護ブランチ: 未コミット変更があればダイアログで確認
        if (BranchPolicy.IsFullyProtected(currentBranch))
        {
            if (hasChanges)
            {
                int fileCount = output.Split('\n').Length;
                bool doStash = EditorUtility.DisplayDialog(
                    $"{currentBranch} ブランチに未コミットの変更があります",
                    $"{fileCount} ファイルの変更が検出されました。\n" +
                    $"{currentBranch} は保護ブランチのため自動コミットできません。\n\n" +
                    "変更を退避(stash)して操作を続行しますか？",
                    "退避して続行", "キャンセル");
                if (!doStash)
                {
                    Log("情報", "未コミット変更があるため操作をキャンセルしました。");
                    return false;
                }
                var (stashCode, stashOutput) = RunGit("stash push --include-untracked -m \"GitFlow auto-stash\"");
                if (stashCode != 0)
                {
                    Log("エラー", $"変更の退避に失敗しました: {stashOutput}");
                    return false;
                }
                Log("警告", $"{currentBranch} ブランチの未コミット変更を退避しました。git stash pop で復元できます。");
            }
            return true;
        }

        // feature 系: 自動コミット
        if (hasChanges)
        {
            string[] statusLines = output.Split('\n');
            int fileCount = statusLines.Length;

            // 新規追加されるファイルをログに表示（意図しないファイルの混入に気づけるように）
            var untrackedFiles = new List<string>();
            foreach (string line in statusLines)
            {
                if (line.StartsWith("??"))
                {
                    untrackedFiles.Add(line.Substring(3).Trim());
                }
            }
            if (untrackedFiles.Count > 0)
            {
                Log("情報", $"新規ファイルを追加します ({untrackedFiles.Count} 件):");
                foreach (string file in untrackedFiles)
                {
                    Log("情報", $"  + {file}");
                }
            }

            Log("情報", $"変更をステージングしています... ({fileCount} ファイル)");
            var (addCode, addOutput) = RunGit("add -A");
            if (addCode != 0)
            {
                Log("エラー", $"ステージングに失敗しました: {addOutput}");
                return false;
            }

            string message = $"Auto-commit {reason}: {DateTime.Now:yyyy/MM/dd HH:mm:ss}";
            (code, output) = RunGit($"commit -m \"{message}\"");
            if (code != 0)
            {
                Log("エラー", $"自動コミットに失敗しました: {output}");
                return false;
            }
            Log("成功", "変更を自動コミットしました。");
        }
        return true;
    }

    // ========================================
    // Fetch（非同期）
    // ========================================

    private void PerformFetch()
    {
        // 操作コンテキスト作成
        string title = "最新を取得 (Fetch)";
        var ctx = new OperationContext
        {
            Title = title,
            Stage = OperationStage.Preparing,

            ClearConsoleOnFinish = false,  // 同一ブランチのためコンソールクリア不要
        };
        currentOp = ctx;
        isOperationRunning = true;
        wasIdleLastFrame = false;
        SavePipelineState();
        Repaint();

        Log("情報", $"=== {title} ===");

        // delayCall で UI に描画フレームを挟む
        EditorApplication.delayCall += () =>
        {
            // Step 1: 前処理 (同期)
            BeginStep(title, "変更を保存", 1, 4);
            ctx.SceneSetup = EditorSceneManager.GetSceneManagerSetup();

            SaveOpenScenesWithPath();
            // NOTE: 複数選択や別Editor拡張で問題が出たら Selection.objects = Array.Empty<Object>() も追加
            Selection.activeObject = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LockAssemblyReload();
            if (!AutoCommit("before update"))
            {
                FinalizeOperation(ctx);
                return;
            }
            EndStep(title, "変更を保存", 1, 4);

            // rebase 前の HEAD を記録（変更検出用）
            var (_, headBefore) = RunGit("rev-parse HEAD");

            // Step 2: fetch (非同期)
            ctx.Stage = OperationStage.GitRunning;
    

            RunStepAsync(title, "リモート取得", 2, 4, "fetch origin", (code, output) =>
            {
                if (code != 0)
                {
                    Log("エラー", $"fetch に失敗しました: {output}");
                    FinalizeOperation(ctx);
                    return;
                }

                // Step 3: rebase
                var (checkCode, _) = RunGit($"rev-parse --verify origin/{currentBranch}");
                if (checkCode != 0)
                {
                    BeginStep(title, "リベース", 3, 4);
                    Log("情報", $"リモートにブランチ {currentBranch} が存在しません。pull をスキップします。");
                    EndStep(title, "リベース (スキップ)", 3, 4);
                    FetchLfsStep();
                    return;
                }

                RunStepAsync(title, $"リベース ({currentBranch})", 3, 4, $"rebase origin/{currentBranch}", (rebaseCode, rebaseOutput) =>
                {
                    if (rebaseCode != 0)
                    {
                        Log("警告", $"リベースでコンフリクトが発生しました。merge にフォールバックします...");
                        RunGit("rebase --abort");
                        var (mergeCode, mergeOutput) = RunGit($"merge origin/{currentBranch}");
                        if (mergeCode != 0)
                        {
                            Log("エラー", $"merge でもコンフリクトが発生しました: {mergeOutput}");
                            RunGit("merge --abort");
                            Log("エラー", "手動でコンフリクトを解決してください。クリーン状態に戻しました。");
                            FinalizeOperation(ctx);
                            return;
                        }
                        Log("成功", "merge で統合しました（rebase は失敗しました）。");
                    }
                    FetchLfsStep();
                });

                void FetchLfsStep()
                {
                    // Step 4: LFS (非同期)
                    RunStepAsync(title, "LFS ファイル取得", 4, 4, "lfs pull", (lfsCode, lfsOutput) =>
                    {
                        if (lfsCode != 0)
                        {
                            Log("警告", $"LFS pull に失敗しました（大容量ファイルが不完全な可能性があります）: {lfsOutput}");
                        }
                        Log("成功", "取得が完了しました。");

                        // HEAD が変わった場合のみフル後処理パイプラインを実行
                        var (_, headAfter) = RunGit("rev-parse HEAD");
                        bool hasChanges = headBefore != headAfter;

                        if (hasChanges)
                        {
                            TransitionTo(OperationStage.PostGitRefresh);
                        }
                        else
                        {
                            // 変更なし: シーン復元 + 後処理のみ（FinalizeOperation が全て担う）
                            FinalizeOperation(ctx);
                        }
                    });
                }
            });
        };
    }

    // ========================================
    // Push（非同期）
    // ========================================

    private void PerformPush()
    {
        string title = "プッシュ (Push)";
        Log("情報", $"=== {title} ===");

        // main への直接プッシュ禁止
        if (BranchPolicy.IsFullyProtected(currentBranch))
        {
            Log("エラー", $"{currentBranch} ブランチへの直接プッシュは禁止されています。");
            Log("情報", "feature ブランチで作業し、PR 経由でマージしてください。");
            return;
        }

        // develop への直接プッシュは警告
        if (currentBranch == "develop")
        {
            bool proceed = EditorUtility.DisplayDialog(
                "develop ブランチへの直接プッシュ",
                "develop は統合用ブランチです。\n通常は feature/ ブランチで作業し、PR 経由でマージしてください。\n\nそのままプッシュしますか？",
                "プッシュする", "キャンセル");
            if (!proceed)
            {
                Log("情報", "プッシュをキャンセルしました。");
                return;
            }
        }

        // 操作コンテキスト作成（Push はシーン退避なし）
        var ctx = new OperationContext
        {
            Title = title,
            Stage = OperationStage.Preparing,

            ClearConsoleOnFinish = false,
        };
        currentOp = ctx;
        isOperationRunning = true;
        wasIdleLastFrame = false;
        SavePipelineState();
        Repaint();

        // delayCall で UI に描画フレームを挟む
        EditorApplication.delayCall += () =>
        {
            LockAssemblyReload();

            // Step 1: 自動コミット (同期・軽量)
            BeginStep(title, "変更を保存", 1, 2);
            if (!AutoCommit("before push"))
            {
                FinalizeOperation(ctx);
                return;
            }
            EndStep(title, "変更を保存", 1, 2);

            // Step 2: push (非同期)
            ctx.Stage = OperationStage.GitRunning;
    

            RunStepAsync(title, $"プッシュ ({currentBranch})", 2, 2, $"push -u origin {currentBranch}", (code, output) =>
            {
                if (code != 0)
                {
                    Log("エラー", $"プッシュに失敗しました: {output}");
                    Log("エラー", "Fetch で最新を取り込んでから再度お試しください。");
                    FinalizeOperation(ctx);
                    return;
                }

                Log("成功", "プッシュが完了しました。");

                // feature ブランチの場合は PR 案内を表示
                if (currentBranch.StartsWith("feature/"))
                {
                    Log("情報", "GitHub で develop へのプルリクエストを作成してください。");
                }

                // Push はシーン退避なしなので直接 Finalize
                FinalizeOperation(ctx);
            });
        };
    }

    // ========================================
    // New Feature（非同期）
    // ========================================

    private void CreateNewFeature()
    {
        Log("情報", "=== 新規ブランチ作成 ===");

        // バリデーション: 空チェック
        if (string.IsNullOrEmpty(newFeatureName))
        {
            Log("エラー", "ブランチ名が入力されていません。");
            return;
        }

        // バリデーション: スペースチェック
        if (newFeatureName.Contains(" "))
        {
            Log("エラー", "ブランチ名にスペースは使えません。ハイフン（-）で単語を繋げてください。");
            return;
        }

        // バリデーション: git 形式チェック
        {
            int refCode = RunGit($"check-ref-format \"refs/heads/feature/{newFeatureName}\"").exitCode;
            if (refCode != 0)
            {
                Log("エラー", $"無効なブランチ名です: feature/{newFeatureName}");
                return;
            }
        }

        string baseBranch = currentBranch;
        string branchName = $"feature/{newFeatureName}";

        // バリデーション: 重複チェック
        var (code, _) = RunGit($"rev-parse --verify {branchName}");
        if (code == 0)
        {
            Log("エラー", $"ブランチ {branchName} は既に存在します。");
            return;
        }

        // 未プッシュのコミットがある場合は警告
        if (ahead > 0)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "未プッシュの変更があります",
                $"現在のブランチ ({currentBranch}) に {ahead} 件の未プッシュのコミットがあります。\n先にプッシュすることをおすすめします。\n\nプッシュせずに新規ブランチを作成しますか？",
                "作成する", "キャンセル");
            if (!proceed)
            {
                Log("情報", "ブランチ作成をキャンセルしました。");
                return;
            }
        }

        // 操作コンテキスト作成
        string title = "新規ブランチ作成";
        var ctx = new OperationContext
        {
            Title = title,
            Stage = OperationStage.Preparing,

            ClearConsoleOnFinish = true,
        };
        currentOp = ctx;
        isOperationRunning = true;
        wasIdleLastFrame = false;
        SavePipelineState();
        Repaint();

        LockAssemblyReload();

        // Step 1: fetch (非同期)
        ctx.Stage = OperationStage.GitRunning;


        RunStepAsync(title, "リモート情報取得", 1, 3, "fetch origin", (fetchCode, fetchOutput) =>
        {
            if (fetchCode != 0)
            {
                Log("エラー", $"fetch に失敗しました: {fetchOutput}");
                FinalizeOperation(ctx);
                return;
            }

            // ローカルブランチの存在を優先チェック
            int localBaseCode = RunGit($"rev-parse --verify {baseBranch}").exitCode;
            int remoteBaseCode = RunGit($"rev-parse --verify origin/{baseBranch}").exitCode;
            bool useLocal = localBaseCode == 0;
            if (!useLocal && remoteBaseCode != 0)
            {
                Log("エラー", $"{baseBranch} ブランチがローカルにもリモートにも存在しません。");
                FinalizeOperation(ctx);
                return;
            }
            string baseRef = useLocal ? baseBranch : $"origin/{baseBranch}";

            // delayCall で UI に描画フレームを挟む
            EditorApplication.delayCall += () =>
            {
                // プレハブモード中なら閉じる（checkout 後の "changed on disk" ダイアログ回避）
                if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                {
                    StageUtility.GoToMainStage();
                }

                // Step 2: 前処理 (同期)
                BeginStep(title, "変更を保存", 2, 3);
                ctx.SceneSetup = EditorSceneManager.GetSceneManagerSetup();

                SaveOpenScenesWithPath();
                // NOTE: 複数選択や別Editor拡張で問題が出たら Selection.objects = Array.Empty<Object>() も追加
                Selection.activeObject = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!AutoCommit("before branch creation"))
                {
                    FinalizeOperation(ctx);
                    return;
                }
                EndStep(title, "変更を保存", 2, 3);

                // Step 3: ブランチ作成 (非同期)
                RunStepAsync(title, $"ブランチ作成 ({branchName})", 3, 3, $"checkout -b {branchName} {baseRef}", (checkoutCode, checkoutOutput) =>
                {
                    if (checkoutCode != 0)
                    {
                        Log("エラー", $"ブランチの作成に失敗しました: {checkoutOutput}");
                        FinalizeOperation(ctx);
                        return;
                    }

                    Log("成功", $"ブランチ {branchName} を作成しました！（ベース: {baseBranch}）");
                    newFeatureName = "";
                    TransitionTo(OperationStage.PostGitRefresh);
                });
            };
        });
    }

    /// <summary>
    /// パスを持つシーンのみ保存する。未保存の新規シーン（パスなし）は
    /// SaveOpenScenes() が Save As ダイアログを開いてしまうためスキップする。
    /// </summary>
    private void SaveOpenScenesWithPath()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!string.IsNullOrEmpty(scene.path) && scene.isDirty)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }
    }

    /// <summary>
    /// シーンの "changed on disk" フラグをクリアする。
    /// UnlockAssemblyReload → エディタ更新完了 → RestoreSceneManagerSetup の後に呼ぶ。
    /// </summary>
    private void ClearSceneChangedOnDiskFlag()
    {
        try
        {
            var method = typeof(EditorSceneManager)
                .GetMethod("ClearOpenScenesChangedOnDisk",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, null);
            }
            else
            {
                Log("警告", "ClearOpenScenesChangedOnDisk が見つかりません（Unity バージョン非対応の可能性）");
            }
        }
        catch (Exception ex)
        {
            Log("警告", $"ClearSceneChangedOnDiskFlag 失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// 次回フォーカス復帰時に1回だけ ClearSceneChangedOnDiskFlag を実行する。
    /// Auto-Refresh がフォーカス復帰時にシーンのタイムスタンプを再チェックして
    /// changedOnDisk フラグを立て直す問題への対策。
    /// delayCall ではなく pendingFocusClearFlag + OnEditorUpdate ポーリングで実現。
    /// </summary>
    private void RegisterOneTimeFocusClear()
    {
        void handler(bool hasFocus)
        {
            if (hasFocus)
            {
                EditorApplication.focusChanged -= handler;
                pendingFocusClearFlag = true;
            }
        }
        EditorApplication.focusChanged += handler;
    }

    // ========================================
    // Switch Branch（非同期）
    // ========================================

    private void SwitchBranch()
    {
        if (selectedBranchIndex < 0 || selectedBranchIndex >= branches.Count)
        {
            Log("エラー", "ブランチが選択されていません。");
            return;
        }

        string target = branches[selectedBranchIndex];
        bool isRemoteOnly = branchIsRemoteOnly[selectedBranchIndex];

        Log("情報", $"=== ブランチ切り替え: {target} ===");

        // 現在のブランチと同じ場合
        if (target == currentBranch)
        {
            Log("情報", $"既に {target} ブランチにいます。");
            return;
        }

        // 未プッシュのコミットがある場合は警告
        if (ahead > 0)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "未プッシュの変更があります",
                $"現在のブランチ ({currentBranch}) に {ahead} 件の未プッシュのコミットがあります。\n先にプッシュすることをおすすめします。\n\nプッシュせずに切り替えますか？",
                "切り替える", "キャンセル");
            if (!proceed)
            {
                Log("情報", "ブランチ切り替えをキャンセルしました。");
                return;
            }
        }

        // 操作コンテキスト作成
        string title = $"ブランチ切り替え: {target}";
        var ctx = new OperationContext
        {
            Title = title,
            Stage = OperationStage.Preparing,

            ClearConsoleOnFinish = true,
        };
        currentOp = ctx;
        isOperationRunning = true;
        wasIdleLastFrame = false;
        SavePipelineState();
        Repaint();

        // delayCall で UI に描画フレームを挟む
        EditorApplication.delayCall += () =>
        {
            // プレハブモード中なら閉じる（checkout 後の "changed on disk" ダイアログ回避）
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                StageUtility.GoToMainStage();
            }

            // Step 1: 前処理 (同期)
            BeginStep(title, "変更を保存", 1, 3);
            ctx.SceneSetup = EditorSceneManager.GetSceneManagerSetup();

            SaveOpenScenesWithPath();
            // NOTE: 複数選択や別Editor拡張で問題が出たら Selection.objects = Array.Empty<Object>() も追加
            Selection.activeObject = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LockAssemblyReload();
            if (!AutoCommit("before branch switch"))
            {
                FinalizeOperation(ctx);
                return;
            }
            EndStep(title, "変更を保存", 1, 3);

            // Step 2: checkout (非同期)
            ctx.Stage = OperationStage.GitRunning;


            string checkoutArgs = isRemoteOnly
                ? $"checkout -b {target} origin/{target}"
                : $"checkout {target}";

            RunStepAsync(title, $"チェックアウト ({target})", 2, 3, checkoutArgs, (code, output) =>
            {
                if (code != 0)
                {
                    Log("エラー", $"ブランチの切り替えに失敗しました: {output}");
                    FinalizeOperation(ctx);
                    return;
                }

                Log("成功", $"ブランチ {target} に切り替えました！");

                // Step 3: LFS pull (非同期)
                RunStepAsync(title, "LFS ファイル取得", 3, 3, "lfs pull", (lfsCode, lfsOutput) =>
                {
                    if (lfsCode != 0)
                    {
                        Log("警告", $"LFS pull に失敗しました（大容量ファイルが不完全な可能性があります）: {lfsOutput}");
                    }
                    TransitionTo(OperationStage.PostGitRefresh);
                });
            });
        };
    }
}
