using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Git user.name / user.email が未設定の場合、起動時に GitFlowWindow を自動表示する。
/// UI/検証/RunGit は GitFlowWindow に統合済みのため、このクラスは起動チェックのみ担当。
/// </summary>
[InitializeOnLoad]
public static class GitUserSetup
{
    static GitUserSetup()
    {
        EditorApplication.delayCall += CheckOnStartup;
    }

    static void CheckOnStartup()
    {
        string projectDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string repoRoot = FindGitRoot(projectDir);
        if (string.IsNullOrEmpty(repoRoot)) return;

        string gitPath = Path.Combine(repoRoot, ".git");
        if (!Directory.Exists(gitPath) && !File.Exists(gitPath)) return;

        bool nameOk = IsGitConfigSet(repoRoot, "user.name");
        bool emailOk = IsGitConfigSet(repoRoot, "user.email");

        if (!nameOk || !emailOk)
        {
            GitFlowWindow.ShowWindow();
        }
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
                StandardOutputEncoding = System.Text.Encoding.UTF8
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
