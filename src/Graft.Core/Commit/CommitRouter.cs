using Graft.Core.Config;
using Graft.Core.Git;
using Graft.Core.Stack;

namespace Graft.Core.Commit;

public sealed class CommitOptions
{
    public bool Amend { get; set; }
}

public sealed class CommitResult
{
    public required string CommitSha { get; set; }
    public required string TargetBranch { get; set; }
    public required string OriginalBranch { get; set; }
    /// <summary>
    /// True when the commit target is not the top branch, meaning branches above are now stale.
    /// </summary>
    public bool BranchesAreStale { get; set; }
}

public static class CommitRouter
{
    private const string RevParse = "rev-parse";
    private const string Checkout = "checkout";

    public static async Task<CommitResult> CommitAsync(
        string? branch, string message, string repoPath, CommitOptions? options = null, CancellationToken ct = default)
    {
        options ??= new CommitOptions();
        var git = new GitRunner(repoPath, ct);

        var stackName = ActiveStackManager.GetActiveStackName(repoPath);
        var stack = ConfigLoader.LoadStack(stackName, repoPath);

        // Check staged changes
        var staged = await git.RunAsync("diff", "--cached", "--name-only");
        if (string.IsNullOrWhiteSpace(staged.Stdout))
            throw new InvalidOperationException("No staged changes to commit");

        // Determine target branch
        var targetBranch = ResolveTargetBranch(branch, stack, stackName);

        // Save original HEAD (resolve to SHA for detached HEAD)
        var originalBranch = await ResolveOriginalHeadAsync(git);

        // Stash staged changes, switch to target branch, apply and commit
        (await git.RunAsync("stash", "push", "--staged", "-m", "graft-commit-temp")).ThrowOnFailure();

        try
        {
            (await git.RunAsync(Checkout, targetBranch)).ThrowOnFailure();
            (await git.RunAsync("stash", "pop")).ThrowOnFailure();

            var commitArgs = new List<string> { "commit" };
            if (options.Amend) commitArgs.Add("--amend");
            if (!string.IsNullOrEmpty(message))
            {
                commitArgs.Add("-m");
                commitArgs.Add(message);
            }
            else if (options.Amend)
            {
                commitArgs.Add("--no-edit");
            }

            var commitResult = await git.RunAsync(commitArgs.ToArray());
            commitResult.ThrowOnFailure();

            // Get the commit sha
            var shaResult2 = await git.RunAsync(RevParse, "--short", "HEAD");
            var commitSha = shaResult2.Stdout.Trim();

            // Check if branches above are now stale
            var targetIdx = stack.Branches.FindIndex(b => b.Name == targetBranch);
            var branchesAreStale = targetIdx < stack.Branches.Count - 1;

            // Return to original branch
            var returnCheckout = await git.RunAsync(Checkout, originalBranch);
            if (!returnCheckout.Success)
                throw new InvalidOperationException(
                    $"Commit succeeded on '{targetBranch}' but failed to return to original branch '{originalBranch}': {returnCheckout.Stderr}");

            return new CommitResult
            {
                CommitSha = commitSha,
                TargetBranch = targetBranch,
                OriginalBranch = originalBranch,
                BranchesAreStale = branchesAreStale,
            };
        }
        catch (Exception ex)
        {
            // Recover stash if it wasn't popped successfully
            var stashRef = await TryRecoverStashAsync(git, originalBranch);

            if (stashRef != null)
            {
                await git.RunAsync(Checkout, originalBranch);
                throw new InvalidOperationException(
                    $"Commit failed. Your staged changes are preserved in git stash ({stashRef}). " +
                    $"Run 'git stash pop {stashRef}' to recover them.");
            }

            var returnResult = await git.RunAsync(Checkout, originalBranch);
            if (!returnResult.Success)
                throw new InvalidOperationException(
                    $"Commit failed and could not return to original branch '{originalBranch}'. " +
                    $"You are currently on '{targetBranch}'. Original error: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Determines the target branch for the commit based on explicit branch name or stack top.
    /// </summary>
    private static string ResolveTargetBranch(string? branch, StackDefinition stack, string stackName)
    {
        if (branch != null)
        {
            if (!stack.Branches.Any(b => b.Name == branch))
                throw new InvalidOperationException($"Branch '{branch}' is not in stack '{stackName}'");
            return branch;
        }

        if (stack.Branches.Count == 0)
            throw new InvalidOperationException($"Stack '{stackName}' has no branches");
        return stack.Branches[^1].Name;
    }

    /// <summary>
    /// Resolves the current HEAD to a branch name or SHA (for detached HEAD).
    /// </summary>
    private static async Task<string> ResolveOriginalHeadAsync(GitRunner git)
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
    /// Attempts to find and return the stash ref for the graft-commit-temp stash entry.
    /// Returns null if no matching stash entry was found.
    /// </summary>
    private static async Task<string?> TryRecoverStashAsync(GitRunner git, string originalBranch)
    {
        var stashList = await git.RunAsync("stash", "list");
        if (!stashList.Success)
            return null;

        var lines = stashList.Stdout.Split('\n');
        var matchLine = lines.FirstOrDefault(line => line.Contains("graft-commit-temp"));

        if (matchLine == null)
            return null;

        var colonIdx = matchLine.IndexOf(':');
        if (colonIdx <= 0)
            return null;

        var indexRef = matchLine[..colonIdx];
        var shaResult = await git.RunAsync(RevParse, indexRef);
        return shaResult.Success ? shaResult.Stdout.Trim() : indexRef;
    }
}
