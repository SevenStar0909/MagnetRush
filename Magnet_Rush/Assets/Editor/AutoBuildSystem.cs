using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// ビルドの自動実行、圧縮、アップロードを行うエディタ拡張クラス
/// </summary>
public class AutoBuildSystem : EditorWindow
{
    // 設定
    private string googleDriveFolder = "";
    private string productName = "";
    private string configFilePath = "Assets/Editor/BuildConfig.json";

    // ビルド設定
    private bool buildDebug = true;
    private bool buildRelease = true;

    [MenuItem("Tools/Auto Build System")]
    public static void ShowWindow()
    {
        GetWindow<AutoBuildSystem>("Auto Build System");
    }

    private void OnGUI()
    {
        GUILayout.Label("ビルド自動化システム", EditorStyles.boldLabel);

        googleDriveFolder = EditorGUILayout.TextField("Googleドライブフォルダパス", googleDriveFolder);

        EditorGUILayout.Space();

        productName = EditorGUILayout.TextField("プロジェクト名", productName);

        EditorGUILayout.Space();

        buildDebug = EditorGUILayout.Toggle("デバッグビルド", buildDebug);
        buildRelease = EditorGUILayout.Toggle("リリースビルド", buildRelease);

        EditorGUILayout.Space();

        if (GUILayout.Button("設定保存"))
        {
            SaveConfig();
        }

        if (GUILayout.Button("ビルド実行"))
        {
            PerformBuild();
        }

    }

    private void OnEnable()
    {
        LoadConfig();
    }

    /// <summary>
    /// 設定をJSONファイルから読み込む
    /// </summary>
    private void LoadConfig()
    {
        try
        {
            // テンプレートから設定ファイルを自動生成
            string templatePath = configFilePath + ".template";
            if (!File.Exists(configFilePath) && File.Exists(templatePath))
            {
                File.Copy(templatePath, configFilePath);
                Debug.LogWarning("[AutoBuild] テンプレートから設定ファイルを作成しました。Tools → Auto Build System で共有フォルダ等を設定してください。");
            }
            else if (!File.Exists(configFilePath) && !File.Exists(templatePath))
            {
                Debug.LogWarning("[AutoBuild] 設定ファイルもテンプレートも見つかりません。デフォルト設定で動作します。");
            }

            if (File.Exists(configFilePath))
            {
                string json = File.ReadAllText(configFilePath);
                BuildConfig config = JsonUtility.FromJson<BuildConfig>(json);

                googleDriveFolder = config.googleDriveFolder;
                productName = !string.IsNullOrEmpty(config.productName) ? config.productName : PlayerSettings.productName;
                buildDebug = config.buildDebug;
                buildRelease = config.buildRelease;

                Debug.Log("[AutoBuild] 設定を読み込みました");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoBuild] 設定読み込みエラー: {e.Message}");
        }
    }

    /// <summary>
    /// 設定をJSONファイルに保存する
    /// </summary>
    private void SaveConfig()
    {
        try
        {
            // PlayerSettings にも反映
            if (!string.IsNullOrEmpty(productName))
            {
                PlayerSettings.productName = productName;
            }

            BuildConfig config = new BuildConfig
            {
                googleDriveFolder = googleDriveFolder,
                productName = productName,
                buildDebug = buildDebug,
                buildRelease = buildRelease
            };

            string json = JsonUtility.ToJson(config, true);

            string directory = Path.GetDirectoryName(configFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(configFilePath, json);
            Debug.Log("[AutoBuild] 設定を保存しました");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoBuild] 設定保存エラー: {e.Message}");
        }
    }

    /// <summary>
    /// ビルドを実行する
    /// </summary>
    private void PerformBuild()
    {
        string branch = GetCurrentBranch();
        if (branch != null && !IsDeployableBranch(branch))
        {
            bool proceed = EditorUtility.DisplayDialog(
                "ブランチ制限",
                $"現在のブランチ「{branch}」からはデプロイできません。\nデプロイは main または develop ブランチからのみ実行可能です。",
                "OK");
            return;
        }

        if (!buildDebug && !buildRelease)
        {
            Debug.LogWarning("[AutoBuild] デバッグビルド・リリースビルドの両方が無効です。ビルド対象がありません。");
            return;
        }

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogWarning("[AutoBuild] Build Settings にシーンが登録されていません。File → Build Settings からシーンを追加してください。");
            return;
        }

        if (string.IsNullOrEmpty(googleDriveFolder))
        {
            Debug.LogWarning("[AutoBuild] 共有フォルダパスが未設定です。ビルドはローカル (Builds/) のみに保存されます。");
        }

        int commitCount = GetCommitCount();
        string fullVersion = $"{commitCount / 100}.{commitCount % 100 / 10}.{commitCount % 10}";
        Debug.Log($"[AutoBuild] バージョン: v{fullVersion} (コミット数: {commitCount})");

        // アップロード先を事前にクリーンアップ（1回だけ）
        if (!string.IsNullOrEmpty(googleDriveFolder) && Directory.Exists(googleDriveFolder))
        {
            CleanupGoogleDrive();
        }

        if (buildDebug)
        {
            BuildPlayerWithSettings(fullVersion, true);
        }

        if (buildRelease)
        {
            BuildPlayerWithSettings(fullVersion, false);
        }
    }

    /// <summary>
    /// 指定された設定でビルドを実行する
    /// </summary>
    private void BuildPlayerWithSettings(string version, bool isDebug)
    {
        string buildType = isDebug ? "Debug" : "Release";

        // 絶対パスを使用
        string buildsRoot = Path.Combine(Application.dataPath, "..", "Builds");
        buildsRoot = Path.GetFullPath(buildsRoot);
        Directory.CreateDirectory(buildsRoot);

        string name = !string.IsNullOrEmpty(productName) ? productName : PlayerSettings.productName;
        string buildFolderName = $"{name}_{buildType}_v{version}";
        string buildFolderPath = Path.Combine(buildsRoot, buildFolderName);

        string exeName = $"{name}.exe";
        string buildPathWithExe = Path.Combine(buildFolderPath, exeName);

        Debug.Log($"[AutoBuild] ビルド出力先: {buildPathWithExe}");

        // ビルドシーンを取得（有効なシーンのみ）
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[AutoBuild] ビルド設定に有効なシーンがありません。File → Build Settings からシーンを追加してください。");
            return;
        }

        if (scenes.Length == 1)
        {
            Debug.LogWarning($"[AutoBuild] シーンが1つだけです: {scenes[0]}");
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPathWithExe,
            target = BuildTarget.StandaloneWindows64,
            options = isDebug
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.None
        };

        Debug.Log($"[AutoBuild] {buildType}ビルドを開始します... ({scenes.Length}シーン)");

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[AutoBuild] {buildType}ビルド成功: {summary.totalSize / 1048576} MB, {summary.totalTime}");

            if (summary.totalWarnings > 0)
            {
                Debug.LogWarning($"[AutoBuild] ビルド中に {summary.totalWarnings} 件の警告が発生しました。");
            }

            if (Directory.Exists(buildFolderPath))
            {
                // ZIP圧縮
                string zipPath = $"{buildFolderPath}.zip";
                CompressBuild(buildFolderPath, zipPath);

                // Googleドライブにアップロード
                UploadToGoogleDrive(zipPath);
            }
            else
            {
                Debug.LogError($"[AutoBuild] ビルドフォルダが見つかりません: {buildFolderPath}");
            }
        }
        else
        {
            Debug.LogError($"[AutoBuild] {buildType}ビルド失敗: {summary.result} ({summary.totalErrors} エラー)");
        }
    }

    /// <summary>
    /// ビルドを圧縮する
    /// </summary>
    private void CompressBuild(string buildPath, string zipPath)
    {
        try
        {
            Debug.Log($"[AutoBuild] ビルドを圧縮しています: {buildPath}");

            if (!Directory.Exists(buildPath))
            {
                Debug.LogError($"[AutoBuild] 圧縮対象のフォルダが存在しません: {buildPath}");
                return;
            }

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                AddDirectoryToZip(zipArchive, buildPath, "");
            }

            if (File.Exists(zipPath))
            {
                FileInfo zipInfo = new FileInfo(zipPath);
                Debug.Log($"[AutoBuild] 圧縮完了: {zipInfo.Length / 1048576} MB");

                // 元のビルドフォルダを削除
                Directory.Delete(buildPath, true);
                Debug.Log($"[AutoBuild] 元のビルドフォルダを削除しました");
            }
            else
            {
                Debug.LogError($"[AutoBuild] ZIPファイルが作成されませんでした: {zipPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoBuild] 圧縮エラー: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// ディレクトリをZIPに追加する補助メソッド
    /// </summary>
    private void AddDirectoryToZip(ZipArchive archive, string sourceDirPath, string entryPrefix)
    {
        foreach (string filePath in Directory.GetFiles(sourceDirPath))
        {
            string fileName = Path.GetFileName(filePath);
            string entryName = string.IsNullOrEmpty(entryPrefix)
                ? fileName
                : $"{entryPrefix}/{fileName}";

            try
            {
                archive.CreateEntryFromFile(filePath, entryName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AutoBuild] ファイル追加エラー ({entryName}): {e.Message}");
            }
        }

        foreach (string subDirPath in Directory.GetDirectories(sourceDirPath))
        {
            string subDirName = Path.GetFileName(subDirPath);
            string newEntryPrefix = string.IsNullOrEmpty(entryPrefix)
                ? subDirName
                : $"{entryPrefix}/{subDirName}";
            AddDirectoryToZip(archive, subDirPath, newEntryPrefix);
        }
    }

    /// <summary>
    /// 共有フォルダ（Googleドライブ等）にアップロードする
    /// </summary>
    private void UploadToGoogleDrive(string zipPath)
    {
        try
        {
            if (string.IsNullOrEmpty(googleDriveFolder))
            {
                Debug.LogWarning("[AutoBuild] 共有フォルダのパスが設定されていません。ローカルのみに保存します。");
                return;
            }

            if (!Directory.Exists(googleDriveFolder))
            {
                Debug.LogError($"[AutoBuild] 共有フォルダが存在しません: {googleDriveFolder}");
                return;
            }

            if (!File.Exists(zipPath))
            {
                Debug.LogError($"[AutoBuild] アップロード対象のZIPファイルが存在しません: {zipPath}");
                return;
            }

            string fileName = Path.GetFileName(zipPath);
            string destination = Path.Combine(googleDriveFolder, fileName);

            FileInfo sourceInfo = new FileInfo(zipPath);
            long fileSize = sourceInfo.Length;

            File.Copy(zipPath, destination, true);

            if (File.Exists(destination))
            {
                FileInfo destInfo = new FileInfo(destination);
                Debug.Log($"[AutoBuild] コピー完了: {fileName} ({fileSize / 1048576} MB)");

                if (destInfo.Length != fileSize)
                {
                    Debug.LogWarning($"[AutoBuild] コピーされたファイルのサイズが一致しません: 元={fileSize}, コピー先={destInfo.Length}");
                }
            }
            else
            {
                Debug.LogError($"[AutoBuild] コピー先にファイルが作成されませんでした: {destination}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoBuild] アップロードエラー: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Googleドライブ内の既存ビルドを全削除する
    /// </summary>
    private void CleanupGoogleDrive()
    {
        try
        {
            int deletedCount = 0;

            foreach (string file in Directory.GetFiles(googleDriveFolder))
            {
                File.Delete(file);
                deletedCount++;
            }

            foreach (string dir in Directory.GetDirectories(googleDriveFolder))
            {
                Directory.Delete(dir, true);
                deletedCount++;
            }

            if (deletedCount > 0)
                Debug.Log($"[AutoBuild] 既存ビルドを削除しました ({deletedCount}件)");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoBuild] クリーンアップエラー: {e.Message}");
        }
    }

    /// <summary>
    /// git のコミット数をビルド番号として取得する
    /// </summary>
    private static int GetCommitCount()
    {
        try
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-list --count HEAD",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode == 0 && int.TryParse(output, out int count))
                {
                    return count;
                }
            }
        }
        catch
        {
            // git が使えない環境では 0
        }
        return 0;
    }

    /// <summary>
    /// 現在の git ブランチ名を取得する
    /// </summary>
    private static string GetCurrentBranch()
    {
        try
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "symbolic-ref --short HEAD",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : null;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// デプロイ可能なブランチかチェックする（main, develop のみ許可）
    /// </summary>
    private static bool IsDeployableBranch(string branch)
    {
        return branch == "main" || branch == "develop";
    }

    /// <summary>
    /// 自動ビルドを実行するためのコマンドライン関数
    /// Deploy.bat から -executeMethod AutoBuildSystem.PerformAutoBuild で呼び出される
    /// </summary>
    public static void PerformAutoBuild()
    {
        Debug.Log("[AutoBuild] === 自動ビルド開始 ===");

        string branch = GetCurrentBranch();
        if (branch != null && !IsDeployableBranch(branch))
        {
            Debug.LogError($"[AutoBuild] ブランチ「{branch}」からはデプロイできません。main または develop からのみ実行可能です。");
            EditorApplication.Exit(1);
            return;
        }

        AutoBuildSystem buildSystem = new AutoBuildSystem();
        buildSystem.LoadConfig();
        buildSystem.PerformBuild();

        Debug.Log("[AutoBuild] === 自動ビルド完了 ===");
        EditorApplication.Exit(0);
    }
}

/// <summary>
/// ビルド設定を保存するためのクラス
/// </summary>
[Serializable]
public class BuildConfig
{
    public string googleDriveFolder = "";
    public string productName = "";
    public bool buildDebug = true;
    public bool buildRelease = true;
}

/// <summary>
/// Unity 起動時にブランチを検出し、main / develop にいる場合は Console に警告を出す
/// GitFlowWindow からも呼び出される共通警告クラス
/// </summary>
[InitializeOnLoad]
public static class BranchWarning
{
    private static string lastWarnedBranch = "";

    static BranchWarning()
    {
        EditorApplication.delayCall += () => CheckBranch();
    }

    /// <summary>
    /// 指定ブランチ（または自動検出）で警告を出す
    /// force=true で重複抑制をリセット
    /// </summary>
    public static void CheckBranch(string branch = null, bool force = false)
    {
        if (branch == null)
        {
            branch = DetectBranch();
            if (branch == null) return;
        }

        if (force) lastWarnedBranch = "";
        if (branch == lastWarnedBranch) return;
        lastWarnedBranch = branch;

        if (branch == "main")
            Debug.LogError("[Git] 現在 main ブランチ（リリース用）にいます。直接作業しないでください。");
        else if (branch == "develop")
            Debug.LogWarning("[Git] 現在 develop ブランチ（統合用）にいます。feature/ ブランチで作業してください。");
    }

    private static string DetectBranch()
    {
        try
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "symbolic-ref --short HEAD",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : null;
            }
        }
        catch
        {
            return null;
        }
    }
}
