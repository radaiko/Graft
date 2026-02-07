using System.Diagnostics;

namespace Graft.Cli.Tests.Helpers;

/// <summary>
/// Creates temporary git repos with pre-configured stacks for E2E CLI tests.
/// Each factory method returns a self-contained fixture ready for testing.
/// </summary>
public sealed class TempCliRepo : IDisposable
{
    public string Path { get; }

    private TempCliRepo(string path) => Path = path;

    /// <summary>
    /// Creates a bare git repo with no stacks. For testing "no stacks found" scenarios.
    /// </summary>
    public static TempCliRepo CreateEmpty()
    {
        var path = InitRepo();
        return new TempCliRepo(path);
    }

    /// <summary>
    /// Creates a repo with a 2-branch stack. HEAD on auth/base-types.
    /// Branches are up-to-date with trunk (no merge needed).
    /// </summary>
    public static TempCliRepo CreateWithStack()
    {
        var path = InitRepo();

        RunGit(path, "checkout", "-b", "auth/base-types");
        File.WriteAllText(System.IO.Path.Combine(path, "base.cs"), "// base types v1");
        RunGit(path, "add", ".");
        RunGit(path, "commit", "-m", "Add auth base types");
        File.WriteAllText(System.IO.Path.Combine(path, "base.cs"), "// base types v2");
        RunGit(path, "add", ".");
        RunGit(path, "commit", "-m", "Update base types");

        RunGit(path, "checkout", "-b", "auth/session-manager");
        File.WriteAllText(System.IO.Path.Combine(path, "session.cs"), "// session manager");
        RunGit(path, "add", ".");
        RunGit(path, "commit", "-m", "Add session manager");

        RunGit(path, "checkout", "auth/base-types");

        WriteStackToml(path);
        return new TempCliRepo(path);
    }

    /// <summary>
    /// Creates a repo where trunk has moved ahead, so stack branches need merging.
    /// HEAD on auth/base-types.
    /// </summary>
    public static TempCliRepo CreateWithNeedsRebase()
    {
        var path = InitRepo();

        RunGit(path, "checkout", "-b", "auth/base-types");
        File.WriteAllText(System.IO.Path.Combine(path, "base.cs"), "// base types");
        RunGit(path, "add", ".");
        RunGit(path, "commit", "-m", "Add auth base types");

        RunGit(path, "checkout", "-b", "auth/session-manager");
        File.WriteAllText(System.IO.Path.Combine(path, "session.cs"), "// session manager");
        RunGit(path, "add", ".");
        RunGit(path, "commit", "-m", "Add session manager");

        // Move trunk ahead so branches need merging
        RunGit(path, "checkout", "master");
        File.WriteAllText(System.IO.Path.Combine(path, "trunk-update.cs"), "// trunk update");
        RunGit(path, "add", ".");
        RunGit(path, "commit", "-m", "Trunk update");

        RunGit(path, "checkout", "auth/base-types");

        WriteStackToml(path);
        return new TempCliRepo(path);
    }

    /// <summary>
    /// Creates a repo where syncing will produce a merge conflict.
    /// Trunk and branch both modify the same file differently.
    /// </summary>
    public static TempCliRepo CreateWithConflict()
    {
        var path = InitRepo();

        // Create branch that modifies a file
        RunGit(path, "checkout", "-b", "feature/conflict");
        File.WriteAllText(System.IO.Path.Combine(path, "shared.cs"), "// branch version\nline2\nline3\n");
        RunGit(path, "add", ".");
        RunGit(path, "commit", "-m", "Branch changes to shared.cs");

        // Go back to trunk and modify the same file
        RunGit(path, "checkout", "master");
        File.WriteAllText(System.IO.Path.Combine(path, "shared.cs"), "// master version\nline2\nline3\n");
        RunGit(path, "add", ".");
        RunGit(path, "commit", "-m", "Master changes to shared.cs");

        RunGit(path, "checkout", "feature/conflict");

        // Create stack TOML and active-stack file
        var graftDir = System.IO.Path.Combine(path, ".git", "graft");
        var stacksDir = System.IO.Path.Combine(graftDir, "stacks");
        Directory.CreateDirectory(stacksDir);
        File.WriteAllText(System.IO.Path.Combine(stacksDir, "test-stack.toml"),
            "name = \"test-stack\"\ntrunk = \"master\"\n\n[[branches]]\nname = \"feature/conflict\"\n");
        File.WriteAllText(System.IO.Path.Combine(graftDir, "active-stack"), "test-stack");

        return new TempCliRepo(path);
    }

    private static string InitRepo()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"graft-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        // Isolate git config to prevent test pollution
        var gitConfigPath = System.IO.Path.Combine(path, ".test-gitconfig");
        File.WriteAllText(gitConfigPath, "");

        RunGit(path, "init");
        RunGit(path, "config", "user.email", "test@graft.dev");
        RunGit(path, "config", "user.name", "Graft Test");
        File.WriteAllText(System.IO.Path.Combine(path, "README.md"), "# Test\n");
        RunGit(path, "add", ".");
        RunGit(path, "commit", "-m", "initial");
        RunGit(path, "branch", "-M", "master");

        return path;
    }

    private static void WriteStackToml(string path)
    {
        var graftDir = System.IO.Path.Combine(path, ".git", "graft");
        var stacksDir = System.IO.Path.Combine(graftDir, "stacks");
        Directory.CreateDirectory(stacksDir);
        File.WriteAllText(System.IO.Path.Combine(stacksDir, "test-stack.toml"),
            "name = \"test-stack\"\ntrunk = \"master\"\n\n[[branches]]\nname = \"auth/base-types\"\n\n[[branches]]\nname = \"auth/session-manager\"\n");
        // Write active-stack file
        File.WriteAllText(System.IO.Path.Combine(graftDir, "active-stack"), "test-stack");
    }

    private static void RunGit(string workDir, params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        process.Start();
        process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {stderr}");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                // Abort any in-progress merge before cleanup
                try { RunGit(Path, "merge", "--abort"); } catch { }
                SetAttributesNormal(new DirectoryInfo(Path));
                Directory.Delete(Path, recursive: true);
            }
        }
        catch { /* best effort cleanup */ }
    }

    private static void SetAttributesNormal(DirectoryInfo dir)
    {
        foreach (var sub in dir.GetDirectories())
            SetAttributesNormal(sub);
        foreach (var file in dir.GetFiles())
            file.Attributes = FileAttributes.Normal;
    }
}
