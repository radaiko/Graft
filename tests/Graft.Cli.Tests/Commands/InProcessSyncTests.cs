using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// In-process tests for stack sync command.
/// Uses a repo where trunk has moved ahead, so stack branches need merging.
/// </summary>
[Collection("InProcess")]
public sealed class InProcessSyncTests : IDisposable
{
    private readonly TempCliRepo _repo;

    public InProcessSyncTests()
    {
        _repo = TempCliRepo.CreateWithNeedsRebase();
    }

    public void Dispose() => _repo.Dispose();

    [Fact]
    public async Task StackSync_MergesBranches()
    {
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "sync");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Syncing", result.Stdout);
        Assert.Contains("Done", result.Stdout);
    }

    [Fact]
    public async Task StackSync_WithBranch_SyncsSpecificBranch()
    {
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "sync", "auth/base-types");

        Assert.Equal(0, result.ExitCode);
    }
}

/// <summary>
/// In-process tests for sync conflict handling.
/// Uses a repo designed to produce merge conflicts.
/// </summary>
[Collection("InProcess")]
public sealed class InProcessConflictTests : IDisposable
{
    private readonly TempCliRepo _repo;

    public InProcessConflictTests()
    {
        _repo = TempCliRepo.CreateWithConflict();
    }

    public void Dispose() => _repo.Dispose();

    [Fact]
    public async Task StackSync_WithConflict_ShowsConflictInfo()
    {
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "sync");

        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        Assert.Contains("conflict", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Continue_NoOp_ShowsInfo()
    {
        // --continue with no in-progress sync
        var emptyRepo = TempCliRepo.CreateEmpty();
        try
        {
            var result = await InProcessCliRunner.RunAsync(emptyRepo.Path, "--continue");

            // Should print "Continuing..." from the root handler
            Assert.Equal(0, result.ExitCode);
        }
        finally
        {
            emptyRepo.Dispose();
        }
    }

    [Fact]
    public async Task Abort_NoOp_ShowsInfo()
    {
        var emptyRepo = TempCliRepo.CreateEmpty();
        try
        {
            var result = await InProcessCliRunner.RunAsync(emptyRepo.Path, "--abort");

            Assert.Equal(0, result.ExitCode);
        }
        finally
        {
            emptyRepo.Dispose();
        }
    }
}
