using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Graft.Core.Install;

public static class AliasInstaller
{
    private const string Config = "config";
    private const string AliasKey = "alias.gt";

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

            // Safe to delete: it's either a symlink pointing elsewhere or a regular file
            File.Delete(gtPath);
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
            RunGitConfig(Config, "-f", gitconfigPath, AliasKey, "!graft");
        }
        else
        {
            RunGitConfig(Config, "--global", AliasKey, "!graft");
        }
    }

    private static void RemoveGitAlias(string? gitconfigPath)
    {
        if (gitconfigPath != null)
        {
            RunGitConfig(Config, "-f", gitconfigPath, "--unset", AliasKey);
        }
        else
        {
            RunGitConfig(Config, "--global", "--unset", AliasKey);
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
            try { process.Kill(entireProcessTree: true); } catch { /* Best-effort kill on timeout */ }
            throw new InvalidOperationException($"git {string.Join(' ', args)} timed out");
        }

        // Ensure stream reads complete after process exits
        Task.WhenAll(stdoutTask, stderrTask).Wait(TimeSpan.FromSeconds(5));

        // Exit code 5 means the key doesn't exist (for --unset), which is fine
        if (process.ExitCode != 0 && process.ExitCode != 5)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed (exit {process.ExitCode})");
    }
}
