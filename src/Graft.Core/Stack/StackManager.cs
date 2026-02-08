using Graft.Core.Config;
using Graft.Core.Git;
using Graft.Core.Worktree;

namespace Graft.Core.Stack;

public static class StackManager
{
    private const string RevParse = "rev-parse";
    private const string Verify = "--verify";
    private const string Checkout = "checkout";
    private const string Merge = "merge";
    private const string BranchNameLabel = "Branch name";

    public static async Task<StackDefinition> InitAsync(string name, string repoPath, string? baseBranch = null, CancellationToken ct = default)
    {
        Validation.ValidateStackName(name);

        // Verify it's a git repo (works for both regular repos and worktrees)
        var dotGit = Path.Combine(repoPath, ".git");
        if (!Directory.Exists(dotGit) && !File.Exists(dotGit))
            throw new InvalidOperationException($"'{repoPath}' is not a git repository");

        var commonDir = GitRunner.ResolveGitCommonDir(repoPath);
        var graftDir = Path.Combine(commonDir, "graft");
        var stacksDir = Path.Combine(graftDir, "stacks");
        Directory.CreateDirectory(stacksDir);

        var stackPath = Path.Combine(stacksDir, $"{name}.toml");
        if (File.Exists(stackPath))
            throw new InvalidOperationException($"Stack '{name}' already exists");

        var git = new GitRunner(repoPath, ct);
        string trunk;

        if (baseBranch != null)
        {
            Validation.ValidateName(baseBranch, "Base branch");
            // Verify branch exists
            var branchCheck = await git.RunAsync(RevParse, Verify, $"refs/heads/{baseBranch}");
            if (!branchCheck.Success)
                throw new InvalidOperationException($"Branch '{baseBranch}' does not exist");
            trunk = baseBranch;
        }
        else
        {
            // Get current branch as trunk
            var result = await git.RunAsync(RevParse, "--abbrev-ref", "HEAD");
            result.ThrowOnFailure();
            trunk = result.Stdout.Trim();
        }

        var stack = new StackDefinition
        {
            Name = name,
            Trunk = trunk,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        ConfigLoader.SaveStack(stack, repoPath);

        // Set as active stack
        ActiveStackManager.SetActiveStack(name, repoPath);

        return stack;
    }

    public static async Task PushAsync(string branchName, string repoPath, bool createBranch = false, CancellationToken ct = default)
    {
        Validation.ValidateName(branchName, BranchNameLabel);

        var stackName = ActiveStackManager.GetActiveStackName(repoPath);
        var stack = ConfigLoader.LoadStack(stackName, repoPath);

        // Check for duplicate
        if (stack.Branches.Any(b => b.Name == branchName))
            throw new InvalidOperationException($"Branch '{branchName}' is already in stack '{stackName}'");

        var git = new GitRunner(repoPath, ct);
        var branchCheck = await git.RunAsync(RevParse, Verify, $"refs/heads/{branchName}");

        if (createBranch)
        {
            if (branchCheck.Success)
                throw new InvalidOperationException($"Branch '{branchName}' already exists. Use push without -c to add an existing branch.");
            (await git.RunAsync(Checkout, "-b", branchName)).ThrowOnFailure();
        }
        else
        {
            if (!branchCheck.Success)
                throw new InvalidOperationException($"Branch '{branchName}' does not exist. Use push with -c to create it.");
            (await git.RunAsync(Checkout, branchName)).ThrowOnFailure();
        }

        stack.Branches.Add(new StackBranch { Name = branchName });
        stack.UpdatedAt = DateTime.UtcNow;
        ConfigLoader.SaveStack(stack, repoPath);
    }

    /// <summary>
    /// Removes and returns the last (top) branch from the active stack.
    /// Does not delete the git branch.
    /// </summary>
    public static Task<string> PopAsync(string repoPath, CancellationToken ct = default)
    {
        var stackName = ActiveStackManager.GetActiveStackName(repoPath);
        var stack = ConfigLoader.LoadStack(stackName, repoPath);

        if (stack.Branches.Count == 0)
            throw new InvalidOperationException($"Stack '{stackName}' has no branches to pop");

        var removed = stack.Branches[^1];
        stack.Branches.RemoveAt(stack.Branches.Count - 1);
        stack.UpdatedAt = DateTime.UtcNow;
        ConfigLoader.SaveStack(stack, repoPath);

        return Task.FromResult(removed.Name);
    }

    /// <summary>
    /// Removes a named branch from the active stack (any position).
    /// Does not delete the git branch.
    /// </summary>
    public static Task DropAsync(string branchName, string repoPath, CancellationToken ct = default)
    {
        Validation.ValidateName(branchName, BranchNameLabel);

        var stackName = ActiveStackManager.GetActiveStackName(repoPath);
        var stack = ConfigLoader.LoadStack(stackName, repoPath);

        var idx = stack.Branches.FindIndex(b => b.Name == branchName);
        if (idx < 0)
            throw new InvalidOperationException($"Branch '{branchName}' is not in stack '{stackName}'");

        stack.Branches.RemoveAt(idx);
        stack.UpdatedAt = DateTime.UtcNow;
        ConfigLoader.SaveStack(stack, repoPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Inserts an existing git branch at the bottom (index 0) of the active stack.
    /// </summary>
    public static async Task ShiftAsync(string branchName, string repoPath, CancellationToken ct = default)
    {
        Validation.ValidateName(branchName, BranchNameLabel);

        var stackName = ActiveStackManager.GetActiveStackName(repoPath);
        var stack = ConfigLoader.LoadStack(stackName, repoPath);

        if (stack.Branches.Any(b => b.Name == branchName))
            throw new InvalidOperationException($"Branch '{branchName}' is already in stack '{stackName}'");

        // Verify branch exists in git
        var git = new GitRunner(repoPath, ct);
        var branchCheck = await git.RunAsync(RevParse, Verify, $"refs/heads/{branchName}");
        if (!branchCheck.Success)
            throw new InvalidOperationException($"Branch '{branchName}' does not exist in git");

        stack.Branches.Insert(0, new StackBranch { Name = branchName });
        stack.UpdatedAt = DateTime.UtcNow;
        ConfigLoader.SaveStack(stack, repoPath);
    }

    public static async Task<SyncResult> SyncAsync(string repoPath, string? branchName = null, CancellationToken ct = default)
    {
        var stackName = ActiveStackManager.GetActiveStackName(repoPath);
        var stack = ConfigLoader.LoadStack(stackName, repoPath);
        var git = new GitRunner(repoPath, ct);
        var result = new SyncResult { Trunk = stack.Trunk };

        // Save original branch (or SHA if detached HEAD)
        var originalBranch = await ResolveOriginalBranchAsync(git);

        // Fetch latest (if remote exists, ignore errors)
        await git.RunAsync("fetch", "--quiet");

        // Determine which branches to sync
        List<StackBranch> branchesToSync;
        int startIdx;
        if (branchName != null)
        {
            Validation.ValidateName(branchName, BranchNameLabel);
            var idx = stack.Branches.FindIndex(b => b.Name == branchName);
            if (idx < 0)
                throw new InvalidOperationException($"Branch '{branchName}' is not in stack '{stackName}'");
            branchesToSync = [stack.Branches[idx]];
            startIdx = idx;
        }
        else
        {
            branchesToSync = stack.Branches;
            startIdx = 0;
        }

        // Track branches that were merged (for pushing)
        var mergedBranches = new List<string>();

        // Detect branches checked out in worktrees (merge there instead of checking out)
        var worktreeByBranch = await GetWorktreeBranchMapAsync(repoPath, ct);

        string parentBranch = startIdx > 0 ? stack.Branches[startIdx - 1].Name : stack.Trunk;

        foreach (var branch in branchesToSync)
        {
            var branchResult = new BranchSyncResult { Name = branch.Name };

            // Verify branch still exists
            var branchExists = await git.RunAsync(RevParse, Verify, $"refs/heads/{branch.Name}");
            if (!branchExists.Success)
                throw new InvalidOperationException(
                    $"Branch '{branch.Name}' in stack '{stackName}' no longer exists.\n" +
                    $"Restore it with 'git branch {branch.Name} <commit>', or remove it with 'graft stack drop {branch.Name}'.");

            var syncBranchResult = await TryMergeBranchAsync(git, parentBranch, branch, worktreeByBranch, ct);

            if (syncBranchResult == null)
            {
                // Up to date
                branchResult.Status = SyncStatus.UpToDate;
                var countResult = await git.RunAsync("rev-list", "--count", $"{parentBranch}..{branch.Name}");
                if (countResult.Success && int.TryParse(countResult.Stdout.Trim(), out var count))
                    branchResult.CommitCount = count;
            }
            else if (syncBranchResult.Value.Success)
            {
                branchResult.Status = SyncStatus.Merged;
                mergedBranches.Add(branch.Name);
                var countResult = await git.RunAsync("rev-list", "--count", $"{parentBranch}..{branch.Name}");
                if (countResult.Success && int.TryParse(countResult.Stdout.Trim(), out var count))
                    branchResult.CommitCount = count;
            }
            else
            {
                branchResult.Status = SyncStatus.Conflict;
                branchResult.ConflictingFiles = syncBranchResult.Value.ConflictingFiles;

                var branchIdx = stack.Branches.FindIndex(b => b.Name == branch.Name);
                int? syncUpTo = branchName != null ? branchIdx : null;
                SaveOperationState(repoPath, stackName, branchIdx, originalBranch, syncUpTo, syncBranchResult.Value.WorktreePath);

                result.BranchResults.Add(branchResult);
                result.HasConflict = true;
                break;
            }

            result.BranchResults.Add(branchResult);
            parentBranch = branch.Name;
        }

        // After all merges succeed, push each merged branch
        if (!result.HasConflict)
            await PushMergedBranchesAsync(git, mergedBranches, result);

        // Return to original branch if no conflict
        if (!result.HasConflict)
        {
            ClearOperationState(repoPath);
            var checkoutResult = await git.RunAsync(Checkout, originalBranch);
            if (!checkoutResult.Success)
                throw new InvalidOperationException(
                    $"Sync completed but failed to return to original branch '{originalBranch}': {checkoutResult.Stderr}");
        }

        stack.UpdatedAt = DateTime.UtcNow;
        ConfigLoader.SaveStack(stack, repoPath);

        return result;
    }

    /// <summary>
    /// Continues a sync operation after conflict resolution.
    /// Finishes the current merge, then continues cascading to remaining branches.
    /// </summary>
    public static async Task<SyncResult> ContinueSyncAsync(string repoPath, CancellationToken ct = default)
    {
        var opState = LoadOperationState(repoPath);
        if (opState == null)
            throw new InvalidOperationException("No sync operation in progress to continue.");

        var git = new GitRunner(repoPath, ct);
        var stack = ConfigLoader.LoadStack(opState.StackName, repoPath);
        var result = new SyncResult { Trunk = stack.Trunk };

        // Finish the current merge (may be in a worktree)
        GitRunner continueGit;
        if (opState.WorktreePath != null)
        {
            if (!Directory.Exists(opState.WorktreePath))
                throw new InvalidOperationException(
                    $"The worktree at '{opState.WorktreePath}' no longer exists.\n" +
                    "Run 'graft --abort' to clean up, then try the sync again.");
            continueGit = new GitRunner(opState.WorktreePath, ct);
        }
        else
        {
            continueGit = git;
        }

        var mergeResult = await continueGit.RunAsync(Merge, "--continue");
        if (!mergeResult.Success)
        {
            // Still has conflicts — get the conflicting files
            var conflictBranch = new BranchSyncResult
            {
                Name = stack.Branches[opState.BranchIndex].Name,
                Status = SyncStatus.Conflict,
            };

            var diffResult = await continueGit.RunAsync("diff", "--name-only", "--diff-filter=U");
            if (diffResult.Success && !string.IsNullOrWhiteSpace(diffResult.Stdout))
            {
                conflictBranch.ConflictingFiles = diffResult.Stdout
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .ToList();
            }

            return new SyncResult
            {
                Trunk = stack.Trunk,
                HasConflict = true,
                BranchResults = [conflictBranch],
            };
        }

        var mergedBranches = new List<string>();

        result.BranchResults.Add(new BranchSyncResult
        {
            Name = stack.Branches[opState.BranchIndex].Name,
            Status = SyncStatus.Merged,
        });
        mergedBranches.Add(stack.Branches[opState.BranchIndex].Name);

        // Continue cascade from the next branch, respecting sync scope
        int endIdx = opState.SyncUpToIndex.HasValue
            ? opState.SyncUpToIndex.Value + 1
            : stack.Branches.Count;

        // Detect worktrees for cascade
        var worktreeByBranch = await GetWorktreeBranchMapAsync(repoPath, ct);

        var cascadeResult = await CascadeMergeAsync(
            git, stack, opState, endIdx, worktreeByBranch, mergedBranches, result, repoPath, ct);
        if (cascadeResult)
            return result;

        // Push merged branches
        foreach (var rb in mergedBranches)
        {
            var pushResult = await git.RunAsync("push", "origin", rb);
            if (!pushResult.Success)
                result.PushWarnings.Add($"Failed to push '{rb}': {pushResult.Stderr}");
        }

        // All done — return to original branch and clean up
        ClearOperationState(repoPath);
        var checkoutResult = await git.RunAsync(Checkout, opState.OriginalBranch);
        if (!checkoutResult.Success)
            throw new InvalidOperationException(
                $"Sync completed but failed to return to original branch '{opState.OriginalBranch}': {checkoutResult.Stderr}");

        return result;
    }

    /// <summary>
    /// Aborts the current sync operation. Aborts the in-progress merge and cleans up state.
    /// </summary>
    public static async Task AbortSyncAsync(string repoPath, CancellationToken ct = default)
    {
        var git = new GitRunner(repoPath, ct);
        var opState = LoadOperationState(repoPath);

        // Abort any in-progress merge (may be in a worktree)
        if (opState?.WorktreePath != null && Directory.Exists(opState.WorktreePath))
        {
            var wtGit = new GitRunner(opState.WorktreePath, ct);
            var wtGitDir = GitRunner.ResolveGitDir(opState.WorktreePath);
            if (File.Exists(Path.Combine(wtGitDir, "MERGE_HEAD")))
                await wtGit.RunAsync(Merge, "--abort");
        }

        // Also check the main repo (fallback when worktree is gone, or no worktree involved)
        if (opState?.WorktreePath == null || !Directory.Exists(opState.WorktreePath))
        {
            var gitDir = GitRunner.ResolveGitDir(repoPath);
            if (File.Exists(Path.Combine(gitDir, "MERGE_HEAD")))
                await git.RunAsync(Merge, "--abort");
        }

        if (opState != null)
        {
            var checkoutResult = await git.RunAsync(Checkout, opState.OriginalBranch);
            ClearOperationState(repoPath);
            if (!checkoutResult.Success)
                throw new InvalidOperationException(
                    $"Abort completed but failed to return to original branch '{opState.OriginalBranch}': {checkoutResult.Stderr}");
        }
    }

    /// <summary>
    /// Resolves the current branch name, or SHA if HEAD is detached.
    /// </summary>
    private static async Task<string> ResolveOriginalBranchAsync(GitRunner git)
    {
        var headResult = await git.RunAsync(RevParse, "--abbrev-ref", "HEAD");
        var originalBranch = headResult.Stdout.Trim();
        if (originalBranch == "HEAD")
        {
            var shaResult = await git.RunAsync(RevParse, "HEAD");
            if (!shaResult.Success || string.IsNullOrWhiteSpace(shaResult.Stdout))
                throw new InvalidOperationException("Cannot determine current HEAD. Is this an empty repository?");
            originalBranch = shaResult.Stdout.Trim();
        }
        return originalBranch;
    }

    /// <summary>
    /// Attempts to merge a parent branch into a stack branch. Returns null if already up-to-date,
    /// or a result indicating success/conflict.
    /// </summary>
    private static async Task<MergeBranchAttempt?> TryMergeBranchAsync(
        GitRunner git, string parentBranch, StackBranch branch,
        Dictionary<string, string> worktreeByBranch, CancellationToken ct)
    {
        // Check if merge needed
        var mergeBase = await git.RunAsync("merge-base", parentBranch, branch.Name);
        var parentHead = await git.RunAsync(RevParse, parentBranch);

        if (mergeBase.Success && parentHead.Success &&
            mergeBase.Stdout.Trim() == parentHead.Stdout.Trim())
        {
            return null; // Up to date
        }

        // If the branch is checked out in a worktree, merge there
        // instead of checking out in the current working tree
        GitRunner mergeGit;
        string? branchWtPath = null;
        if (worktreeByBranch.TryGetValue(branch.Name, out var wtPath))
        {
            mergeGit = new GitRunner(wtPath, ct);
            branchWtPath = wtPath;
        }
        else
        {
            (await git.RunAsync(Checkout, branch.Name)).ThrowOnFailure();
            mergeGit = git;
        }

        var mergeResult = await mergeGit.RunAsync(Merge, parentBranch, "--no-edit");

        if (mergeResult.Success)
        {
            return new MergeBranchAttempt { Success = true, WorktreePath = branchWtPath };
        }

        var diffResult = await mergeGit.RunAsync("diff", "--name-only", "--diff-filter=U");
        var conflictingFiles = new List<string>();
        if (diffResult.Success && !string.IsNullOrWhiteSpace(diffResult.Stdout))
        {
            conflictingFiles = diffResult.Stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .ToList();
        }

        return new MergeBranchAttempt { Success = false, ConflictingFiles = conflictingFiles, WorktreePath = branchWtPath };
    }

    /// <summary>
    /// Pushes all merged branches to origin and records warnings for failures.
    /// </summary>
    private static async Task PushMergedBranchesAsync(GitRunner git, List<string> mergedBranches, SyncResult result)
    {
        foreach (var rb in mergedBranches)
        {
            var pushResult = await git.RunAsync("push", "origin", rb);
            if (!pushResult.Success)
                result.PushWarnings.Add($"Failed to push '{rb}': {pushResult.Stderr}");
        }
    }

    /// <summary>
    /// Performs the cascade merge loop for ContinueSyncAsync.
    /// Returns true if a conflict was encountered (caller should return early).
    /// </summary>
    private static async Task<bool> CascadeMergeAsync(
        GitRunner git, StackDefinition stack, OperationState opState, int endIdx,
        Dictionary<string, string> worktreeByBranch, List<string> mergedBranches,
        SyncResult result, string repoPath, CancellationToken ct)
    {
        string parentBranch = stack.Branches[opState.BranchIndex].Name;
        for (int i = opState.BranchIndex + 1; i < endIdx; i++)
        {
            var branch = stack.Branches[i];
            var branchResult = new BranchSyncResult { Name = branch.Name };

            GitRunner branchGit;
            string? branchWtPath = null;
            if (worktreeByBranch.TryGetValue(branch.Name, out var cascadeWtPath))
            {
                branchGit = new GitRunner(cascadeWtPath, ct);
                branchWtPath = cascadeWtPath;
            }
            else
            {
                (await git.RunAsync(Checkout, branch.Name)).ThrowOnFailure();
                branchGit = git;
            }

            var cascadeMerge = await branchGit.RunAsync(Merge, parentBranch, "--no-edit");

            if (cascadeMerge.Success)
            {
                branchResult.Status = SyncStatus.Merged;
                mergedBranches.Add(branch.Name);
                var countResult = await git.RunAsync("rev-list", "--count", $"{parentBranch}..{branch.Name}");
                if (countResult.Success && int.TryParse(countResult.Stdout.Trim(), out var count))
                    branchResult.CommitCount = count;
            }
            else
            {
                branchResult.Status = SyncStatus.Conflict;
                var diffResult = await branchGit.RunAsync("diff", "--name-only", "--diff-filter=U");
                if (diffResult.Success && !string.IsNullOrWhiteSpace(diffResult.Stdout))
                {
                    branchResult.ConflictingFiles = diffResult.Stdout
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .ToList();
                }

                SaveOperationState(repoPath, opState.StackName, i, opState.OriginalBranch, opState.SyncUpToIndex, branchWtPath);
                result.BranchResults.Add(branchResult);
                result.HasConflict = true;
                return true;
            }

            result.BranchResults.Add(branchResult);
            parentBranch = branch.Name;
        }

        return false;
    }

    private static async Task<Dictionary<string, string>> GetWorktreeBranchMapAsync(string repoPath, CancellationToken ct)
    {
        var worktrees = await WorktreeManager.ListAsync(repoPath, ct);
        var normalizedRepoPath = Path.GetFullPath(repoPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var map = new Dictionary<string, string>();
        foreach (var wt in worktrees)
        {
            if (wt.Branch == null || wt.IsBare)
                continue;

            // Skip the main repo itself — only include separate worktrees
            var normalizedWtPath = Path.GetFullPath(wt.Path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(normalizedWtPath, normalizedRepoPath, StringComparison.OrdinalIgnoreCase))
                continue;

            if (Directory.Exists(wt.Path))
                map[wt.Branch] = wt.Path;
        }
        return map;
    }

    private static string GetOperationStatePath(string repoPath)
    {
        var commonDir = GitRunner.ResolveGitCommonDir(repoPath);
        return Path.Combine(commonDir, "graft", "operation.toml");
    }

    private static void SaveOperationState(string repoPath, string stackName, int branchIndex, string originalBranch, int? syncUpToIndex = null, string? worktreePath = null)
    {
        var path = GetOperationStatePath(repoPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var table = new Tomlyn.Model.TomlTable
        {
            ["stack"] = stackName,
            ["branch_index"] = (long)branchIndex,
            ["original_branch"] = originalBranch,
            ["operation"] = "sync",
        };
        if (syncUpToIndex.HasValue)
            table["sync_up_to_index"] = (long)syncUpToIndex.Value;
        if (worktreePath != null)
            table["worktree_path"] = worktreePath;
        var tempPath = $"{path}.tmp.{Guid.NewGuid():N}";
        File.WriteAllText(tempPath, Tomlyn.Toml.FromModel(table), System.Text.Encoding.UTF8);
        File.Move(tempPath, path, overwrite: true);
    }

    public static OperationState? LoadOperationState(string repoPath)
    {
        var path = GetOperationStatePath(repoPath);
        if (!File.Exists(path))
            return null;

        OperationState state;
        try
        {
            var table = Tomlyn.Toml.ToModel(File.ReadAllText(path, System.Text.Encoding.UTF8));
            state = new OperationState
            {
                StackName = (string)table["stack"],
                BranchIndex = (int)(long)table["branch_index"],
                OriginalBranch = (string)table["original_branch"],
                Operation = (string)table["operation"],
                SyncUpToIndex = table.TryGetValue("sync_up_to_index", out var syncUpTo) ? (int)(long)syncUpTo : null,
                WorktreePath = table.TryGetValue("worktree_path", out var wtPath) ? (string)wtPath : null,
            };
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidCastException or Tomlyn.TomlException)
        {
            throw new InvalidOperationException(
                $"Operation state file is corrupt: {path}\n" +
                $"Delete it and run 'graft --abort' to clean up.", ex);
        }

        // Validate persisted values to prevent injection from tampered files
        Validation.ValidateStackName(state.StackName);
        try
        {
            Validation.ValidateName(state.OriginalBranch, "Original branch");
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                $"Operation state file has invalid branch value: {path}\n" +
                $"Delete it and run 'graft --abort' to clean up.");
        }

        // WorktreePath is not path-validated here because symlink resolution
        // (e.g. /var vs /private/var on macOS) makes path comparison unreliable.
        // Safety is ensured at usage sites via Directory.Exists checks and git
        // command failures on non-repo directories.

        return state;
    }

    public static void ClearOperationState(string repoPath)
    {
        var path = GetOperationStatePath(repoPath);
        if (File.Exists(path))
            File.Delete(path);
    }

    public static void Delete(string name, string repoPath)
    {
        Validation.ValidateStackName(name);

        var commonDir = GitRunner.ResolveGitCommonDir(repoPath);
        var stackPath = Path.Combine(commonDir, "graft", "stacks", $"{name}.toml");
        if (!File.Exists(stackPath))
            throw new FileNotFoundException($"Stack '{name}' not found", stackPath);

        File.Delete(stackPath);

        // Clear active stack if it was the deleted one
        var active = ConfigLoader.LoadActiveStack(repoPath);
        if (active == name)
            ActiveStackManager.ClearActiveStack(repoPath);
    }

    /// <summary>
    /// Internal result type for TryMergeBranchAsync.
    /// </summary>
    private struct MergeBranchAttempt
    {
        public bool Success;
        public List<string> ConflictingFiles;
        public string? WorktreePath;
    }
}

public sealed class SyncResult
{
    public string Trunk { get; set; } = "";
    public List<BranchSyncResult> BranchResults { get; set; } = [];
    public bool HasConflict { get; set; }
    public List<string> PushWarnings { get; set; } = [];
}

public sealed class BranchSyncResult
{
    public string Name { get; set; } = "";
    public SyncStatus Status { get; set; }
    public int CommitCount { get; set; }
    public List<string> ConflictingFiles { get; set; } = [];
}

public enum SyncStatus
{
    UpToDate,
    Merged,
    Conflict,
}

public sealed class OperationState
{
    public string StackName { get; set; } = "";
    public int BranchIndex { get; set; }
    public string OriginalBranch { get; set; } = "";
    public string Operation { get; set; } = "";
    /// <summary>
    /// If set, sync should stop after this branch index (for single-branch sync).
    /// If null, sync continues to the end of the stack.
    /// </summary>
    public int? SyncUpToIndex { get; set; }
    /// <summary>
    /// If set, the conflicted branch is checked out in this worktree path.
    /// Merge continue/abort should be performed there instead of the main repo.
    /// </summary>
    public string? WorktreePath { get; set; }
}
