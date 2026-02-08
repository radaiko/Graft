using Graft.Core.Commit;
using Graft.Core.Stack;
using Graft.Core.Tests.Helpers;

namespace Graft.Core.Tests.Commit;

public sealed class CommitRouterTests : IDisposable
{
    private readonly TempGitRepo _repo = new();

    public void Dispose() => _repo.Dispose();

    // Requirement: CommitOptions has Amend flag (NoCascade removed)
    [Fact]
    public void CommitOptions_Defaults_AllFalse()
    {
        var opts = new CommitOptions();

        Assert.False(opts.Amend);
    }

    // Requirement: CommitResult has CommitSha, TargetBranch, OriginalBranch, BranchesAreStale
    [Fact]
    public void CommitResult_StoresCommitSha()
    {
        var result = new CommitResult
        {
            CommitSha = "abc1234",
            TargetBranch = "feature/x",
            OriginalBranch = "main",
        };

        Assert.Equal("abc1234", result.CommitSha);
        Assert.Equal("feature/x", result.TargetBranch);
        Assert.Equal("main", result.OriginalBranch);
        Assert.False(result.BranchesAreStale);
    }

    // Requirement: CommitResult tracks BranchesAreStale when committing to non-top branch
    [Fact]
    public void CommitResult_BranchesAreStale_WhenSet()
    {
        var result = new CommitResult
        {
            CommitSha = "abc1234",
            TargetBranch = "auth/base-types",
            OriginalBranch = "auth/base-types",
            BranchesAreStale = true,
        };

        Assert.True(result.BranchesAreStale);
    }

    // CommitAsync with empty stack throws "no branches"
    [Fact]
    public async Task CommitAsync_EmptyStack_Throws()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("empty-stack", _repo.Path);

        // Stage a change
        _repo.CommitFile("initial.txt", "content", "setup");
        File.WriteAllText(Path.Combine(_repo.Path, "change.txt"), "new");
        _repo.RunGit("add", "change.txt");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CommitRouter.CommitAsync(null, "commit msg", _repo.Path));
        Assert.Contains("has no branches", ex.Message);
    }

    // CommitAsync with no staged changes throws
    [Fact]
    public async Task CommitAsync_NoStagedChanges_Throws()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("no-staged", _repo.Path);
        _repo.RunGit("checkout", "-b", "feature-branch");
        await StackManager.PushAsync("feature-branch", _repo.Path);
        _repo.RunGit("checkout", "master");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CommitRouter.CommitAsync(null, "commit msg", _repo.Path));
        Assert.Contains("No staged changes", ex.Message);
    }
}
