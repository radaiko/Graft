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
        string targetBranch;
        if (branch != null)
        {
            if (!stack.Branches.Any(b => b.Name == branch))
                throw new InvalidOperationException($"Branch '{branch}' is not in stack '{stackName}'");
            targetBranch = branch;
        }
        else
        {
            if (stack.Branches.Count == 0)
                throw new InvalidOperationException($"Stack '{stackName}' has no branches");
            targetBranch = stack.Branches[^1].Name;
        }

        // Save original HEAD (resolve to SHA for detached HEAD)
        var headResult = await git.RunAsync("rev-parse", "--abbrev-ref", "HEAD");
        var originalBranch = headResult.Stdout.Trim();
        if (originalBranch == "HEAD")
        {
            var shaResult = await git.RunAsync("rev-parse", "HEAD");
            if (!shaResult.Success || string.IsNullOrWhiteSpace(shaResult.Stdout))
                throw new InvalidOperationException("Cannot determine current HEAD. Is this an empty repository?");
            originalBranch = shaResult.Stdout.Trim();
        }

        // Stash staged changes, switch to target branch, apply and commit
        (await git.RunAsync("stash", "push", "--staged", "-m", "graft-commit-temp")).ThrowOnFailure();

        try
        {
            (await git.RunAsync("checkout", targetBranch)).ThrowOnFailure();
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
            var shaResult2 = await git.RunAsync("rev-parse", "--short", "HEAD");
            var commitSha = shaResult2.Stdout.Trim();

            // Check if branches above are now stale
            var targetIdx = stack.Branches.FindIndex(b => b.Name == targetBranch);
            var branchesAreStale = targetIdx < stack.Branches.Count - 1;

            // Return to original branch
            var returnCheckout = await git.RunAsync("checkout", originalBranch);
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
            var stashList = await git.RunAsync("stash", "list");
            if (stashList.Success)
            {
                var lines = stashList.Stdout.Split('\n');
                string? stashRef = null;
                foreach (var line in lines)
                {
                    if (line.Contains("graft-commit-temp"))
                    {
                        var colonIdx = line.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            var indexRef = line[..colonIdx];
                            var shaResult3 = await git.RunAsync("rev-parse", indexRef);
                            stashRef = shaResult3.Success ? shaResult3.Stdout.Trim() : indexRef;
                        }
                        break;
                    }
                }

                if (stashRef != null)
                {
                    await git.RunAsync("checkout", originalBranch);
                    throw new InvalidOperationException(
                        $"Commit failed. Your staged changes are preserved in git stash ({stashRef}). " +
                        $"Run 'git stash pop {stashRef}' to recover them.");
                }
            }

            var returnResult = await git.RunAsync("checkout", originalBranch);
            if (!returnResult.Success)
                throw new InvalidOperationException(
                    $"Commit failed and could not return to original branch '{originalBranch}'. " +
                    $"You are currently on '{targetBranch}'. Original error: {ex.Message}", ex);
            throw;
        }
    }
}
