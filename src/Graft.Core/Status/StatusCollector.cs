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

        // Collect status in parallel
        var tasks = existingRepos.Select(repo =>
            CollectRepoStatusAsync(repo.Name, repo.Path, detailed: false, ct));

        var results = await Task.WhenAll(tasks);
        return [.. results];
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
                if (parts.Length == 2)
                {
                    int.TryParse(parts[0], out var ahead);
                    int.TryParse(parts[1], out var behind);
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

            // Worktrees (non-bare, non-main)
            try
            {
                var worktrees = await WorktreeManager.ListAsync(repoPath, ct);

                // Use git to resolve the real toplevel path (handles macOS /var → /private/var symlink)
                var toplevelResult = await git.RunAsync("rev-parse", "--show-toplevel");
                var mainPath = toplevelResult.Success ? toplevelResult.Stdout : Path.GetFullPath(repoPath);

                status.Worktrees = worktrees
                    .Where(w => !w.IsBare && !string.Equals(w.Path, mainPath, StringComparison.Ordinal))
                    .ToList();
            }
            catch
            {
                // Worktree listing failed — not critical
            }

            // Graft stacks (only if .git/graft exists)
            var graftDir = Path.Combine(GitRunner.ResolveGitCommonDir(repoPath), "graft");
            if (Directory.Exists(graftDir))
            {
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

                if (detailed)
                {
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
                            foreach (var branch in stack.Branches)
                            {
                                var branchSummary = new StackBranchSummary { Name = branch.Name };

                                var branchParent = stack.Branches.IndexOf(branch) == 0
                                    ? stack.Trunk
                                    : stack.Branches[stack.Branches.IndexOf(branch) - 1].Name;

                                var branchAbResult = await git.RunAsync(
                                    "rev-list", "--left-right", "--count",
                                    $"refs/heads/{branch.Name}...refs/heads/{branchParent}");
                                if (branchAbResult.Success)
                                {
                                    var parts = branchAbResult.Stdout.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length == 2)
                                    {
                                        int.TryParse(parts[0], out var ahead);
                                        int.TryParse(parts[1], out var behind);
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
        }
        catch (Exception ex)
        {
            status.IsAccessible = false;
            status.Error = ex.Message;
        }

        return status;
    }
}
