using System.Runtime.InteropServices;
using Graft.Core.Git;

namespace Graft.Core.Worktree;

public static class WorktreeManager
{
    public static async Task AddAsync(string branch, string repoPath, bool createBranch = false, CancellationToken ct = default)
    {
        Validation.ValidateName(branch, "Branch name");

        var git = new GitRunner(repoPath, ct);

        if (createBranch)
        {
            // Verify branch does NOT exist
            var branchCheck = await git.RunAsync("rev-parse", "--verify", $"refs/heads/{branch}");
            if (branchCheck.Success)
                throw new InvalidOperationException($"Branch '{branch}' already exists. Use 'graft wt <branch>' without -c.");

            // Create branch first, then add worktree
            (await git.RunAsync("branch", branch)).ThrowOnFailure();
        }
        else
        {
            // Verify branch exists
            var branchCheck = await git.RunAsync("branch", "--list", branch);
            if (string.IsNullOrWhiteSpace(branchCheck.Stdout))
                throw new InvalidOperationException($"Branch '{branch}' does not exist. Use 'graft wt <branch> -c' to create it.");
        }

        // Check if worktree already exists for this branch
        var wtList = await git.RunAsync("worktree", "list", "--porcelain");
        if (wtList.Success && wtList.Stdout.Split('\n').Any(line => line.Trim() == $"branch refs/heads/{branch}"))
            throw new InvalidOperationException($"Worktree already exists for branch '{branch}'");

        var wtPath = GetWorktreePath(branch, repoPath);

        // Safety check: ensure resulting path doesn't escape repo parent
        var repoParent = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(repoPath))!);
        var resolvedWtPath = Path.GetFullPath(wtPath);
        var pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        if (!resolvedWtPath.StartsWith(repoParent + Path.DirectorySeparatorChar, pathComparison)
            && !resolvedWtPath.Equals(repoParent, pathComparison))
            throw new InvalidOperationException($"Worktree path '{resolvedWtPath}' would be outside the repository parent directory");

        var result = await git.RunAsync("worktree", "add", wtPath, branch);
        result.ThrowOnFailure();
    }

    public static async Task RemoveAsync(string branch, string repoPath, bool force = false, CancellationToken ct = default)
    {
        var git = new GitRunner(repoPath, ct);

        // Find the worktree path for this branch
        var wtList = await git.RunAsync("worktree", "list", "--porcelain");
        if (!wtList.Success)
            throw new InvalidOperationException("Failed to list worktrees");

        var wtPath = FindWorktreePath(wtList.Stdout, branch);
        if (wtPath == null)
            throw new InvalidOperationException($"No worktree found for branch '{branch}'");

        if (!force)
        {
            // Check for uncommitted changes
            var statusResult = await git.RunAsync("-C", wtPath, "status", "--porcelain");
            if (statusResult.Success && !string.IsNullOrWhiteSpace(statusResult.Stdout))
                throw new InvalidOperationException(
                    $"Worktree for '{branch}' has uncommitted changes. Use -f to force removal.");
        }

        var args = force
            ? new[] { "worktree", "remove", "--force", wtPath }
            : new[] { "worktree", "remove", wtPath };
        var result = await git.RunAsync(args);
        result.ThrowOnFailure();
    }

    public static async Task<List<WorktreeInfo>> ListAsync(string repoPath, CancellationToken ct = default)
    {
        var git = new GitRunner(repoPath, ct);
        var result = await git.RunAsync("worktree", "list", "--porcelain");
        result.ThrowOnFailure();

        return ParseWorktreeList(result.Stdout);
    }

    /// <summary>
    /// Computes the worktree path: ../{repoName}.wt.{safeBranch}/
    /// Slashes in branch names are replaced with hyphens.
    /// </summary>
    public static string GetWorktreePath(string branch, string repoPath)
    {
        var repoName = Path.GetFileName(Path.GetFullPath(repoPath));
        var parentDir = Path.GetDirectoryName(Path.GetFullPath(repoPath))!;
        var safeBranch = branch.Replace('/', '-');
        return Path.Combine(parentDir, $"{repoName}.wt.{safeBranch}");
    }

    private static string? FindWorktreePath(string porcelainOutput, string branch)
    {
        string? currentPath = null;
        foreach (var line in porcelainOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("worktree "))
                currentPath = trimmed["worktree ".Length..];
            else if (trimmed == $"branch refs/heads/{branch}")
                return currentPath;
        }
        return null;
    }

    private static List<WorktreeInfo> ParseWorktreeList(string porcelainOutput)
    {
        var worktrees = new List<WorktreeInfo>();
        WorktreeInfo? current = null;

        foreach (var line in porcelainOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("worktree "))
            {
                current = new WorktreeInfo { Path = trimmed["worktree ".Length..] };
                worktrees.Add(current);
            }
            else if (current != null && trimmed.StartsWith("branch refs/heads/"))
            {
                current.Branch = trimmed["branch refs/heads/".Length..];
            }
            else if (current != null && trimmed == "bare")
            {
                current.IsBare = true;
            }
            else if (current != null && trimmed.StartsWith("HEAD "))
            {
                current.HeadSha = trimmed["HEAD ".Length..];
            }
        }

        return worktrees;
    }
}

public sealed class WorktreeInfo
{
    public string Path { get; set; } = "";
    public string? Branch { get; set; }
    public string? HeadSha { get; set; }
    public bool IsBare { get; set; }
}
