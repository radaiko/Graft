using Graft.Core.Config;
using Graft.Core.Git;
using Graft.Core.Worktree;

namespace Graft.Core.Status;

public static class StatusCollector
{
    public static async Task<List<RepoStatus>> CollectAllAsync(string configDir, CancellationToken ct = default)
    {
        var cache = ConfigLoader.LoadRepoCache(configDir);

        // Filter to repos whose path still exists
        var existingRepos = cache.Repos.Where(r => Directory.Exists(r.Path)).ToList();

        // Collect status in parallel with bounded concurrency
        var bag = new System.Collections.Concurrent.ConcurrentBag<RepoStatus>();

        await Parallel.ForEachAsync(existingRepos,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct },
            async (repo, token) =>
            {
                var s = await CollectRepoStatusAsync(repo.Name, repo.Path, detailed: false, token);
                bag.Add(s);
            });

        return [.. bag.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public static async Task<RepoStatus> CollectOneAsync(string repoPath, CancellationToken ct = default)
    {
        var name = Path.GetFileName(Path.GetFullPath(repoPath));
        return await CollectRepoStatusAsync(name, repoPath, detailed: true, ct);
    }

    private static async Task<RepoStatus> CollectRepoStatusAsync(
        string name, string repoPath, bool detailed, CancellationToken ct)
    {
        var status = new RepoStatus { Name = name, Path = repoPath };

        try
        {
            var git = new GitRunner(repoPath, ct);

            await CollectBasicGitInfoAsync(git, status);
            await CollectWorktreeInfoAsync(git, repoPath, status, ct);
            await CollectStackInfoAsync(repoPath, status, detailed, git);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            status.IsAccessible = false;
            status.Error = ex.Message;
        }

        return status;
    }

    /// <summary>
    /// Collects branch, upstream, ahead/behind, and changed/untracked file counts.
    /// </summary>
    private static async Task CollectBasicGitInfoAsync(GitRunner git, RepoStatus status)
    {
        // Branch
        var branchResult = await git.RunAsync("rev-parse", "--abbrev-ref", "HEAD");
        if (branchResult.Success)
            status.Branch = branchResult.Stdout;

        // Upstream
        var upstreamResult = await git.RunAsync("rev-parse", "--abbrev-ref", "@{upstream}");
        if (upstreamResult.Success)
            status.Upstream = upstreamResult.Stdout;

        // Ahead/behind
        var abResult = await git.RunAsync("rev-list", "--left-right", "--count", "HEAD...@{upstream}");
        if (abResult.Success)
        {
            var parts = abResult.Stdout.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2
                && int.TryParse(parts[0], out var ahead)
                && int.TryParse(parts[1], out var behind))
            {
                status.Ahead = ahead;
                status.Behind = behind;
            }
        }

        // Changed/untracked files
        var statusResult = await git.RunAsync("status", "--porcelain");
        if (statusResult.Success && !string.IsNullOrEmpty(statusResult.Stdout))
        {
            foreach (var line in statusResult.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length >= 2 && line[0] == '?' && line[1] == '?')
                    status.UntrackedFiles++;
                else
                    status.ChangedFiles++;
            }
        }
    }

    /// <summary>
    /// Collects worktree information (non-bare, non-main worktrees).
    /// </summary>
    private static async Task CollectWorktreeInfoAsync(GitRunner git, string repoPath, RepoStatus status, CancellationToken ct)
    {
        try
        {
            var worktrees = await WorktreeManager.ListAsync(repoPath, ct);

            // Use git to resolve the real toplevel path (handles macOS /var → /private/var symlink)
            var toplevelResult = await git.RunAsync("rev-parse", "--show-toplevel");
            var mainPath = toplevelResult.Success ? toplevelResult.Stdout : Path.GetFullPath(repoPath);

            var normalizedMainPath = Path.GetFullPath(mainPath);
            var pathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            status.Worktrees = worktrees
                .Where(w => !w.IsBare && !string.Equals(
                    Path.GetFullPath(w.Path), normalizedMainPath, pathComparison))
                .ToList();
        }
        catch
        {
            // Worktree listing failed — not critical
        }
    }

    /// <summary>
    /// Collects graft stack information (active stack, branch counts, and detailed per-branch stats).
    /// </summary>
    private static async Task CollectStackInfoAsync(string repoPath, RepoStatus status, bool detailed, GitRunner git)
    {
        var graftDir = Path.Combine(GitRunner.ResolveGitCommonDir(repoPath), "graft");
        if (!Directory.Exists(graftDir))
            return;

        status.ActiveStackName = ConfigLoader.LoadActiveStack(repoPath);

        var stackNames = ConfigLoader.ListStacks(repoPath);

        if (status.ActiveStackName != null)
        {
            try
            {
                var activeStack = ConfigLoader.LoadStack(status.ActiveStackName, repoPath);
                status.ActiveStackBranchCount = activeStack.Branches.Count;
            }
            catch
            {
                // Stack file corrupt or missing — clear
                status.ActiveStackName = null;
            }
        }

        if (!detailed)
            return;

        foreach (var stackName in stackNames)
        {
            try
            {
                var stack = ConfigLoader.LoadStack(stackName, repoPath);
                var summary = new StackSummary
                {
                    Name = stack.Name,
                    Trunk = stack.Trunk,
                    IsActive = string.Equals(stackName, status.ActiveStackName, StringComparison.Ordinal),
                };

                // Get ahead/behind for each stack branch
                for (int bi = 0; bi < stack.Branches.Count; bi++)
                {
                    var branch = stack.Branches[bi];
                    var branchSummary = new StackBranchSummary { Name = branch.Name };

                    var branchParent = bi == 0
                        ? stack.Trunk
                        : stack.Branches[bi - 1].Name;

                    var branchAbResult = await git.RunAsync(
                        "rev-list", "--left-right", "--count",
                        $"refs/heads/{branch.Name}...refs/heads/{branchParent}");
                    if (branchAbResult.Success)
                    {
                        var parts = branchAbResult.Stdout.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2
                            && int.TryParse(parts[0], out var ahead)
                            && int.TryParse(parts[1], out var behind))
                        {
                            branchSummary.Ahead = ahead;
                            branchSummary.Behind = behind;
                        }
                    }

                    summary.Branches.Add(branchSummary);
                }

                status.Stacks.Add(summary);
            }
            catch
            {
                // Skip corrupt stack definitions
            }
        }
    }
}
