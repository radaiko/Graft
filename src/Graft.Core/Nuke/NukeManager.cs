using System.Runtime.InteropServices;
using Graft.Core.Config;
using Graft.Core.Git;
using Graft.Core.Stack;
using Graft.Core.Worktree;

namespace Graft.Core.Nuke;

public sealed class NukeResult
{
    public List<string> Removed { get; set; } = [];
    public List<string> Skipped { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public static class NukeManager
{
    public static async Task<NukeResult> NukeWorktreesAsync(string repoPath, bool force = false, CancellationToken ct = default)
    {
        var result = new NukeResult();
        var worktrees = await WorktreeManager.ListAsync(repoPath, ct);

        var repoFullPath = Path.GetFullPath(repoPath);
        var pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var linkedWorktrees = worktrees
            .Where(wt => wt.Branch != null && !wt.IsBare)
            .Where(wt => !string.Equals(repoFullPath, Path.GetFullPath(wt.Path), pathComparison));

        foreach (var wt in linkedWorktrees)
        {

            try
            {
                await WorktreeManager.RemoveAsync(wt.Branch!, repoPath, force, ct);
                result.Removed.Add(wt.Branch!);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("uncommitted changes"))
            {
                result.Skipped.Add($"{wt.Branch} (dirty)");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{wt.Branch}: {ex.Message}");
            }
        }

        return result;
    }

    public static Task<NukeResult> NukeStacksAsync(string repoPath, bool force = false, CancellationToken ct = default)
    {
        var result = new NukeResult();
        var stacks = ConfigLoader.ListStacks(repoPath);

        foreach (var name in stacks)
        {
            try
            {
                StackManager.Delete(name, repoPath);
                result.Removed.Add(name);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{name}: {ex.Message}");
            }
        }

        // Active stack is cleared by Delete if it was the deleted one
        return Task.FromResult(result);
    }

    public static async Task<NukeResult> NukeBranchesAsync(string repoPath, CancellationToken ct = default)
    {
        var result = new NukeResult();
        var git = new GitRunner(repoPath, ct);

        // Prune remote tracking refs
        await git.RunAsync("fetch", "--prune");

        // Find branches whose upstream is gone
        var branchResult = await git.RunAsync("branch", "-vv");
        if (!branchResult.Success) return result;

        // git branch -vv uses a fixed 2-char prefix: "  " normal, "* " current, "+ " worktree
        var goneBranches = branchResult.Stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Length > 2 && line[0] == ' ')
            .Select(line => line[2..].TrimStart())
            .Where(line => line.Contains('[') && line.Contains(": gone]"))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);

        foreach (var branchName in goneBranches)
        {
            var deleteResult = await git.RunAsync("branch", "-D", branchName);
            if (deleteResult.Success)
                result.Removed.Add(branchName);
            else
                result.Errors.Add($"{branchName}: {deleteResult.Stderr}");
        }

        return result;
    }

    public static async Task<NukeResult> NukeAllAsync(string repoPath, bool force = false, CancellationToken ct = default)
    {
        var combined = new NukeResult();

        // Worktrees first (can't delete branches with active worktrees)
        var wtResult = await NukeWorktreesAsync(repoPath, force, ct);
        combined.Removed.AddRange(wtResult.Removed.Select(r => $"worktree:{r}"));
        combined.Skipped.AddRange(wtResult.Skipped.Select(s => $"worktree:{s}"));
        combined.Errors.AddRange(wtResult.Errors.Select(e => $"worktree:{e}"));

        // Then stacks
        var stackResult = await NukeStacksAsync(repoPath, force, ct);
        combined.Removed.AddRange(stackResult.Removed.Select(r => $"stack:{r}"));
        combined.Errors.AddRange(stackResult.Errors.Select(e => $"stack:{e}"));

        // Then branches
        var branchResult = await NukeBranchesAsync(repoPath, ct);
        combined.Removed.AddRange(branchResult.Removed.Select(r => $"branch:{r}"));
        combined.Errors.AddRange(branchResult.Errors.Select(e => $"branch:{e}"));

        return combined;
    }
}
