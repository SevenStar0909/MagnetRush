using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 起動時に Git ユーザー設定を確認し、未設定なら設定ウィンドウを表示する。
/// 設定済みなら GitFlowWindow を自動表示する。
/// </summary>
[InitializeOnLoad]
public static class GitUserSetup
{
    private const string SessionKey = "GitUserSetup_Shown";

    static GitUserSetup()
    {
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(SessionKey, false)) return;
                SessionState.SetBool(SessionKey, true);

                string projectDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string repoRoot = FindGitRoot(projectDir);

                bool nameOk = IsGitConfigSet(repoRoot, "user.name");
                bool emailOk = IsGitConfigSet(repoRoot, "user.email");

                if (!nameOk || !emailOk)
                {
                    GitUserSetupWindow.ShowWindow();
                }
                else
                {
                    GitFlowWindow.ShowWindow();
                }
            };
        };
    }

    static bool IsGitConfigSet(string workDir, string key)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"config {key}",
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                return p.ExitCode == 0 && !string.IsNullOrEmpty(output);
            }
        }
        catch
        {
            return false;
        }
    }

    static string FindGitRoot(string startDir)
    {
        string dir = startDir;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git")))
                return dir;
            string parent = Directory.GetParent(dir)?.FullName;
            if (parent == dir) break;
            dir = parent;
        }
        return startDir;
    }
}

/// <summary>
/// Git ユーザー設定ウィンドウ（user.name / user.email）
/// </summary>
public class GitUserSetupWindow : EditorWindow
{
    private string inputUserName = "";
    private string inputUserEmail = "";
    private string repoRoot;

    public static void ShowWindow()
    {
        var window = GetWindow<GitUserSetupWindow>("Git ユーザー設定");
        window.minSize = new Vector2(350, 200);
    }

    private void OnEnable()
    {
        repoRoot = FindGitRoot(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Git のユーザー情報が設定されていません。\nコミットするには名前とメールアドレスが必要です。\nGitHub に登録しているメールアドレスを入力してください。",
            MessageType.Warning);
        EditorGUILayout.Space();

        inputUserName = EditorGUILayout.TextField("名前", inputUserName);
        inputUserEmail = EditorGUILayout.TextField("メールアドレス", inputUserEmail);

        EditorGUILayout.Space();

        if (GUILayout.Button("保存"))
        {
            string trimName = inputUserName?.Trim();
            string trimEmail = inputUserEmail?.Trim();

            if (string.IsNullOrEmpty(trimName) || string.IsNullOrEmpty(trimEmail))
            {
                EditorUtility.DisplayDialog("エラー", "名前とメールアドレスの両方を入力してください。", "OK");
            }
            else if (trimName.Contains("\"") || trimEmail.Contains("\"") ||
                     trimName.Contains(";") || trimEmail.Contains(";"))
            {
                EditorUtility.DisplayDialog("エラー", "無効な文字が含まれています。", "OK");
            }
            else
            {
                var (nameCode, _) = RunGit($"config user.name \"{trimName}\"");
                var (emailCode, _) = RunGit($"config user.email \"{trimEmail}\"");
                if (nameCode != 0 || emailCode != 0)
                {
                    EditorUtility.DisplayDialog("エラー", "Git の設定に失敗しました。\ngit がインストールされているか確認してください。", "OK");
                    return;
                }
                EditorUtility.DisplayDialog("完了", $"Git ユーザーを設定しました:\n{trimName} <{trimEmail}>", "OK");
                Close();
                GitFlowWindow.ShowWindow();
            }
        }
    }

    private (int exitCode, string output) RunGit(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                return (p.ExitCode, output);
            }
        }
        catch (System.Exception e)
        {
            return (-1, e.Message);
        }
    }

    private string FindGitRoot(string startDir)
    {
        string dir = startDir;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git")))
                return dir;
            string parent = Directory.GetParent(dir)?.FullName;
            if (parent == dir) break;
            dir = parent;
        }
        return startDir;
    }
}
