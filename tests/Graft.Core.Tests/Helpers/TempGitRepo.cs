using System.Diagnostics;

namespace Graft.Core.Tests.Helpers;

/// <summary>
/// Creates a temporary git repository for integration tests.
/// Disposes by deleting the temp directory.
/// </summary>
public sealed class TempGitRepo : IDisposable
{
    public string Path { get; }

    private readonly string _gitConfigPath;

    public TempGitRepo()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);

        // Isolate git config to prevent test pollution from/to user's global config
        _gitConfigPath = System.IO.Path.Combine(Path, ".test-gitconfig");
        File.WriteAllText(_gitConfigPath, "");

        RunGit("init");
        RunGit("config", "user.email", "test@graft.dev");
        RunGit("config", "user.name", "Graft Test");
        RunGit("commit", "--allow-empty", "-m", "initial");
        // Normalize default branch to "master" for test consistency
        RunGit("branch", "-M", "master");
    }

    public string RunGit(params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        // Isolate git from user's global config
        process.StartInfo.Environment["GIT_CONFIG_GLOBAL"] = _gitConfigPath;

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed (exit {process.ExitCode}): {stderr}");

        return stdout.TrimEnd();
    }

    /// <summary>
    /// Creates the .git/graft/ directory structure for testing.
    /// </summary>
    public string InitGraftDir()
    {
        var graftDir = System.IO.Path.Combine(Path, ".git", "graft");
        Directory.CreateDirectory(graftDir);
        Directory.CreateDirectory(System.IO.Path.Combine(graftDir, "stacks"));
        return graftDir;
    }

    /// <summary>
    /// Creates a file and commits it.
    /// </summary>
    public void CommitFile(string relativePath, string content, string message)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
        RunGit("add", relativePath);
        RunGit("commit", "-m", message);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            // On some systems .git files may be read-only
            SetAttributesNormal(new DirectoryInfo(Path));
            Directory.Delete(Path, recursive: true);
        }
    }

    private static void SetAttributesNormal(DirectoryInfo dir)
    {
        foreach (var sub in dir.GetDirectories())
            SetAttributesNormal(sub);
        foreach (var file in dir.GetFiles())
            file.Attributes = FileAttributes.Normal;
    }
}
