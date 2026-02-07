using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Graft.Core.Install;

public static class AliasInstaller
{
    public static void Install(string binaryPath, string? gitconfigPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binaryPath);

        // Create gt symlink/copy next to the graft binary
        var dir = Path.GetDirectoryName(binaryPath)!;
        var gtPath = Path.Combine(dir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gt.exe" : "gt");

        if (Directory.Exists(gtPath))
        {
            throw new InvalidOperationException(
                $"Cannot create alias: '{gtPath}' is a directory. Remove it manually and retry.");
        }

        if (File.Exists(gtPath))
        {
            // Check if it's already our symlink
            var fi = new FileInfo(gtPath);
            if (fi.LinkTarget != null && Path.GetFullPath(fi.LinkTarget) == Path.GetFullPath(binaryPath))
                return; // Already installed

            // Verify it's safe to delete: must be a symlink or a regular file
            var attrs = File.GetAttributes(gtPath);
            if ((attrs & FileAttributes.ReparsePoint) != 0)
            {
                // It's a symlink pointing elsewhere — safe to replace
                File.Delete(gtPath);
            }
            else
            {
                // Regular file — remove and replace
                File.Delete(gtPath);
            }
        }

        CreateLink(gtPath, binaryPath);

        // Write git alias using git config command (safe, no manual file editing)
        WriteGitAlias(gitconfigPath);
    }

    public static void Uninstall(string binaryPath, string? gitconfigPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binaryPath);

        // Remove gt symlink/copy
        var dir = Path.GetDirectoryName(binaryPath)!;
        var gtPath = Path.Combine(dir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gt.exe" : "gt");

        if (File.Exists(gtPath))
            File.Delete(gtPath);

        // Remove git alias using git config command
        RemoveGitAlias(gitconfigPath);
    }

    private static void CreateLink(string linkPath, string targetPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Symlinks on Windows require elevated privileges or developer mode.
            // Fall back to copying the binary.
            File.Copy(targetPath, linkPath, overwrite: true);
        }
        else
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
    }

    private static void WriteGitAlias(string? gitconfigPath)
    {
        if (gitconfigPath != null)
        {
            RunGitConfig("config", "-f", gitconfigPath, "alias.gt", "!graft");
        }
        else
        {
            RunGitConfig("config", "--global", "alias.gt", "!graft");
        }
    }

    private static void RemoveGitAlias(string? gitconfigPath)
    {
        if (gitconfigPath != null)
        {
            RunGitConfig("config", "-f", gitconfigPath, "--unset", "alias.gt");
        }
        else
        {
            RunGitConfig("config", "--global", "--unset", "alias.gt");
        }
    }

    private static void RunGitConfig(params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        process.Start();

        // Read both streams concurrently to prevent pipe buffer deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(10_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException($"git {string.Join(' ', args)} timed out");
        }

        // Ensure stream reads complete after process exits
        Task.WhenAll(stdoutTask, stderrTask).Wait(TimeSpan.FromSeconds(5));

        // Exit code 5 means the key doesn't exist (for --unset), which is fine
        if (process.ExitCode != 0 && process.ExitCode != 5)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed (exit {process.ExitCode})");
    }
}
