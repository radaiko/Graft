using Graft.Core.Commit;

namespace Graft.Core.Tests.Commit;

public sealed class CommitRouterTests
{
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
}
