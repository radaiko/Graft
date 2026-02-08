using System.Diagnostics;

namespace Graft.Core.Git;

/// <summary>
/// Executes git CLI commands. AOT-safe alternative to LibGit2Sharp.
/// </summary>
public sealed class GitRunner
{
    private readonly string _workingDirectory;
    private readonly CancellationToken _ct;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private const string GitdirPrefix = "gitdir: ";

    public GitRunner(string workingDirectory, CancellationToken ct = default)
    {
        _workingDirectory = workingDirectory;
        _ct = ct;
    }

    public async Task<GitResult> RunAsync(params string[] args)
    {
        return await RunAsync(DefaultTimeout, args);
    }

    public async Task<GitResult> RunAsync(TimeSpan timeout, params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        // Prevent git from ever opening an editor (e.g. during merge --continue).
        process.StartInfo.Environment["GIT_EDITOR"] = "true";

        process.Start();

        // Read both streams concurrently to prevent pipe buffer deadlock.
        // Link the caller's cancellation token with the timeout so either can cancel.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        linkedCts.CancelAfter(timeout);
        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(linkedCts.Token);

            return new GitResult(process.ExitCode, stdoutTask.Result.TrimEnd(), stderrTask.Result.TrimEnd());
        }
        catch (OperationCanceledException) when (_ct.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* Best-effort kill on cancellation */ }
            throw; // Propagate caller cancellation
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);
            }
            catch { /* Best-effort kill/wait on timeout */ }
            return new GitResult(-1, "", $"git command timed out after {timeout.TotalSeconds}s: git {string.Join(' ', args)}");
        }
    }

    /// <summary>
    /// Resolves the git directory for the given working directory.
    /// Works for regular repos (.git is a directory), worktrees (.git is a file),
    /// and returns the common dir for shared state.
    /// </summary>
    public static string ResolveGitCommonDir(string workingDir)
    {
        var dotGit = Path.Combine(workingDir, ".git");

        // Regular repo: .git is a directory
        if (Directory.Exists(dotGit))
            return dotGit;

        // Worktree or submodule: .git is a file containing "gitdir: <path>"
        if (File.Exists(dotGit))
        {
            var content = File.ReadAllText(dotGit).Trim();
            if (content.StartsWith(GitdirPrefix, StringComparison.Ordinal))
            {
                var gitDir = content[GitdirPrefix.Length..];
                if (!Path.IsPathRooted(gitDir))
                    gitDir = Path.GetFullPath(Path.Combine(workingDir, gitDir));

                // Check for commondir file (worktree-specific git dir points to shared dir)
                var commonDirFile = Path.Combine(gitDir, "commondir");
                if (File.Exists(commonDirFile))
                {
                    var commonDir = File.ReadAllText(commonDirFile).Trim();
                    if (!Path.IsPathRooted(commonDir))
                        commonDir = Path.GetFullPath(Path.Combine(gitDir, commonDir));
                    return commonDir;
                }
                return gitDir;
            }
        }

        throw new InvalidOperationException($"'{workingDir}' is not a git repository");
    }

    /// <summary>
    /// Resolves the worktree-specific git directory (for rebase state, etc.).
    /// Unlike ResolveGitCommonDir, this returns the per-worktree dir.
    /// </summary>
    public static string ResolveGitDir(string workingDir)
    {
        var dotGit = Path.Combine(workingDir, ".git");

        if (Directory.Exists(dotGit))
            return dotGit;

        if (File.Exists(dotGit))
        {
            var content = File.ReadAllText(dotGit).Trim();
            if (content.StartsWith(GitdirPrefix, StringComparison.Ordinal))
            {
                var gitDir = content[GitdirPrefix.Length..];
                if (!Path.IsPathRooted(gitDir))
                    gitDir = Path.GetFullPath(Path.Combine(workingDir, gitDir));
                return gitDir;
            }
        }

        throw new InvalidOperationException($"'{workingDir}' is not a git repository");
    }
}

public readonly record struct GitResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Success => ExitCode == 0;

    public bool HasConflict => !Success && Stderr.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase);

    public GitResult ThrowOnFailure()
    {
        if (!Success)
            throw new GitException($"git failed (exit {ExitCode}): {Stderr}");
        return this;
    }
}

public sealed class GitException(string message) : Exception(message);
