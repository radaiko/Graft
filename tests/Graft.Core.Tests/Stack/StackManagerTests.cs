using Graft.Core.Commit;
using Graft.Core.Config;
using Graft.Core.Git;
using Graft.Core.Stack;
using Graft.Core.Tests.Helpers;
using Graft.Core.Worktree;

namespace Graft.Core.Tests.Stack;

/// <summary>
/// Tests for stack management operations.
/// Uses real git repos via TempGitRepo and real GitRunner.
/// </summary>
public sealed class StackManagerTests : IDisposable
{
    private readonly TempGitRepo _repo = new();
    private readonly GitRunner _git;

    public StackManagerTests()
    {
        _git = new GitRunner(_repo.Path);
    }

    public void Dispose() => _repo.Dispose();

    // ========================
    // graft stack init <name>
    // ========================

    [Fact]
    public async Task Init_ValidName_CreatesStackFile()
    {
        _repo.InitGraftDir();
        var expectedFile = Path.Combine(_repo.Path, ".git", "graft", "stacks", "auth-refactor.toml");

        var status = await _git.RunAsync("status");
        Assert.True(status.Success);

        var stack = await StackManager.InitAsync("auth-refactor", _repo.Path);

        Assert.True(File.Exists(expectedFile));
        Assert.Equal("auth-refactor", stack.Name);
    }

    [Fact]
    public async Task Init_UsesCurrentBranchAsTrunk()
    {
        _repo.InitGraftDir();
        var result = await _git.RunAsync("rev-parse", "--abbrev-ref", "HEAD");
        var currentBranch = result.Stdout.Trim();
        Assert.False(string.IsNullOrEmpty(currentBranch));

        var stack = await StackManager.InitAsync("test-stack", _repo.Path);
        Assert.Equal(currentBranch, stack.Trunk);
    }

    [Fact]
    public async Task Init_SetsActiveStack()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);

        var active = ConfigLoader.LoadActiveStack(_repo.Path);
        Assert.Equal("test-stack", active);
    }

    [Fact]
    public async Task Init_WithBaseBranch_UsesThatAsTrunk()
    {
        _repo.InitGraftDir();
        await _git.RunAsync("checkout", "-b", "develop");
        await _git.RunAsync("checkout", "master");

        var stack = await StackManager.InitAsync("test-stack", _repo.Path, baseBranch: "develop");
        Assert.Equal("develop", stack.Trunk);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Init_InvalidName_ThrowsArgumentException(string? name)
    {
        _ = name; // suppress xUnit1026
        _repo.InitGraftDir();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            StackManager.InitAsync(name!, _repo.Path));
    }

    [Fact]
    public async Task Init_DuplicateName_ThrowsError()
    {
        var graftDir = _repo.InitGraftDir();
        File.WriteAllText(
            Path.Combine(graftDir, "stacks", "existing.toml"),
            "name = \"existing\"\ntrunk = \"main\"");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            StackManager.InitAsync("existing", _repo.Path));
    }

    [Fact]
    public async Task Init_NotGitRepo_ThrowsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            Assert.False(Directory.Exists(Path.Combine(tempDir, ".git")));

            await Assert.ThrowsAnyAsync<Exception>(() =>
                StackManager.InitAsync("test", tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========================
    // graft stack push <branch>
    // ========================

    [Fact]
    public async Task Push_ExistingBranch_AddsBranchToTopOfStack()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);

        var branchResult = await _git.RunAsync("branch", "feature/new-branch");
        Assert.True(branchResult.Success);

        await StackManager.PushAsync("feature/new-branch", _repo.Path);

        var updatedStack = ConfigLoader.LoadStack("test-stack", _repo.Path);
        Assert.Equal("feature/new-branch", updatedStack.Branches[^1].Name);
    }

    [Fact]
    public async Task Push_CreateBranch_CreatesAndAdds()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);

        await StackManager.PushAsync("new-branch", _repo.Path, createBranch: true);

        var updatedStack = ConfigLoader.LoadStack("test-stack", _repo.Path);
        Assert.Single(updatedStack.Branches);
        Assert.Equal("new-branch", updatedStack.Branches[0].Name);
    }

    [Fact]
    public async Task Push_CreateBranch_ExistingBranch_ThrowsError()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);
        await _git.RunAsync("branch", "existing-branch");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            StackManager.PushAsync("existing-branch", _repo.Path, createBranch: true));
    }

    [Fact]
    public async Task Push_NoBranch_ThrowsError()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            StackManager.PushAsync("nonexistent-branch", _repo.Path));
    }

    [Fact]
    public async Task Push_DuplicateBranch_ThrowsError()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);
        await StackManager.PushAsync("dup-branch", _repo.Path, createBranch: true);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            StackManager.PushAsync("dup-branch", _repo.Path));
    }

    // ========================
    // graft stack pop
    // ========================

    [Fact]
    public async Task Pop_RemovesLastBranch()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);
        await StackManager.PushAsync("branch1", _repo.Path, createBranch: true);
        await StackManager.PushAsync("branch2", _repo.Path, createBranch: true);

        var removed = await StackManager.PopAsync(_repo.Path);

        Assert.Equal("branch2", removed);
        var stack = ConfigLoader.LoadStack("test-stack", _repo.Path);
        Assert.Single(stack.Branches);
        Assert.Equal("branch1", stack.Branches[0].Name);
    }

    [Fact]
    public async Task Pop_EmptyStack_ThrowsError()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            StackManager.PopAsync(_repo.Path));
    }

    // ========================
    // graft stack drop <branch>
    // ========================

    [Fact]
    public async Task Drop_RemovesNamedBranch()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);
        await StackManager.PushAsync("branch1", _repo.Path, createBranch: true);
        await StackManager.PushAsync("branch2", _repo.Path, createBranch: true);

        await StackManager.DropAsync("branch1", _repo.Path);

        var stack = ConfigLoader.LoadStack("test-stack", _repo.Path);
        Assert.Single(stack.Branches);
        Assert.Equal("branch2", stack.Branches[0].Name);
    }

    [Fact]
    public async Task Drop_BranchNotInStack_ThrowsError()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            StackManager.DropAsync("nonexistent", _repo.Path));
    }

    // ========================
    // graft stack shift <branch>
    // ========================

    [Fact]
    public async Task Shift_InsertsAtBottom()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);
        await StackManager.PushAsync("branch1", _repo.Path, createBranch: true);
        await _git.RunAsync("checkout", "master");
        await _git.RunAsync("branch", "bottom-branch");

        await StackManager.ShiftAsync("bottom-branch", _repo.Path);

        var stack = ConfigLoader.LoadStack("test-stack", _repo.Path);
        Assert.Equal(2, stack.Branches.Count);
        Assert.Equal("bottom-branch", stack.Branches[0].Name);
        Assert.Equal("branch1", stack.Branches[1].Name);
    }

    [Fact]
    public async Task Shift_DuplicateBranch_ThrowsError()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);
        await StackManager.PushAsync("branch1", _repo.Path, createBranch: true);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            StackManager.ShiftAsync("branch1", _repo.Path));
    }

    // ========================
    // graft stack sync
    // ========================

    [Fact]
    public async Task Sync_RebasesStackBottomToTop()
    {
        _repo.InitGraftDir();
        var stack = await StackManager.InitAsync("test-stack", _repo.Path);

        // Create branches: trunk -> branch1 -> branch2
        await _git.RunAsync("checkout", "-b", "branch1");
        _repo.CommitFile("file1.txt", "content1", "Add file1");
        await _git.RunAsync("checkout", "-b", "branch2");
        _repo.CommitFile("file2.txt", "content2", "Add file2");

        // Register branches in stack
        await _git.RunAsync("checkout", stack.Trunk);
        await StackManager.PushAsync("branch1", _repo.Path);
        await StackManager.PushAsync("branch2", _repo.Path);

        // Return to trunk and add a new commit so branches are behind
        await _git.RunAsync("checkout", stack.Trunk);
        _repo.CommitFile("trunk-file.txt", "trunk content", "Trunk commit");

        var result = await StackManager.SyncAsync(_repo.Path);

        Assert.False(result.HasConflict);
        Assert.True(result.BranchResults.Count >= 2);
        Assert.True(result.BranchResults.All(b =>
            b.Status == SyncStatus.Merged || b.Status == SyncStatus.UpToDate));
    }

    [Fact]
    public async Task Sync_AlreadyUpToDate_ReturnsNoChanges()
    {
        _repo.InitGraftDir();
        var stack = await StackManager.InitAsync("test-stack", _repo.Path);

        await _git.RunAsync("checkout", "-b", "uptodate-branch");
        _repo.CommitFile("file.txt", "content", "Add file");
        await _git.RunAsync("checkout", stack.Trunk);
        await StackManager.PushAsync("uptodate-branch", _repo.Path);
        await _git.RunAsync("checkout", stack.Trunk);

        var result = await StackManager.SyncAsync(_repo.Path);

        Assert.False(result.HasConflict);
        Assert.True(result.BranchResults.All(b =>
            b.Status == SyncStatus.UpToDate || b.Status == SyncStatus.Merged));
    }

    [Fact]
    public async Task Sync_Conflict_StopsAndReportsConflict()
    {
        _repo.InitGraftDir();
        var stack = await StackManager.InitAsync("test-stack", _repo.Path);

        await _git.RunAsync("checkout", "-b", "conflict-branch");
        _repo.CommitFile("shared.txt", "branch version", "Branch change");

        await _git.RunAsync("checkout", stack.Trunk);
        await StackManager.PushAsync("conflict-branch", _repo.Path);

        await _git.RunAsync("checkout", stack.Trunk);
        _repo.CommitFile("shared.txt", "trunk version", "Trunk change");

        var result = await StackManager.SyncAsync(_repo.Path);

        Assert.True(result.HasConflict);
        Assert.Contains(result.BranchResults, b => b.Status == SyncStatus.Conflict);

        // Clean up the merge state
        await _git.RunAsync("merge", "--abort");
        StackManager.ClearOperationState(_repo.Path);
    }

    [Fact]
    public async Task Sync_BranchWithWorktree_SyncsSuccessfully()
    {
        _repo.InitGraftDir();
        var stack = await StackManager.InitAsync("test-stack", _repo.Path);

        // Create branches: trunk -> branch1 -> branch2
        await _git.RunAsync("checkout", "-b", "branch1");
        _repo.CommitFile("file1.txt", "content1", "Add file1");
        await _git.RunAsync("checkout", "-b", "branch2");
        _repo.CommitFile("file2.txt", "content2", "Add file2");

        // Register branches in stack
        await _git.RunAsync("checkout", stack.Trunk);
        await StackManager.PushAsync("branch1", _repo.Path);
        await StackManager.PushAsync("branch2", _repo.Path);

        // Create a worktree for branch1 (the branch that would fail on checkout)
        await _git.RunAsync("checkout", stack.Trunk);
        await WorktreeManager.AddAsync("branch1", _repo.Path);

        // Add a trunk commit so branches need syncing
        _repo.CommitFile("trunk-file.txt", "trunk content", "Trunk commit");

        var result = await StackManager.SyncAsync(_repo.Path);

        Assert.False(result.HasConflict);
        Assert.Equal(2, result.BranchResults.Count);
        Assert.True(result.BranchResults.All(b =>
            b.Status == SyncStatus.Merged || b.Status == SyncStatus.UpToDate));
    }

    [Fact]
    public async Task Sync_MultipleBranchesWithWorktrees_SyncsSuccessfully()
    {
        _repo.InitGraftDir();
        var stack = await StackManager.InitAsync("test-stack", _repo.Path);

        // Create branches: trunk -> branch1 -> branch2 -> branch3
        await _git.RunAsync("checkout", "-b", "branch1");
        _repo.CommitFile("file1.txt", "content1", "Add file1");
        await _git.RunAsync("checkout", "-b", "branch2");
        _repo.CommitFile("file2.txt", "content2", "Add file2");
        await _git.RunAsync("checkout", "-b", "branch3");
        _repo.CommitFile("file3.txt", "content3", "Add file3");

        // Register branches in stack
        await _git.RunAsync("checkout", stack.Trunk);
        await StackManager.PushAsync("branch1", _repo.Path);
        await StackManager.PushAsync("branch2", _repo.Path);
        await StackManager.PushAsync("branch3", _repo.Path);

        // Create worktrees for branch1 and branch3
        await _git.RunAsync("checkout", stack.Trunk);
        await WorktreeManager.AddAsync("branch1", _repo.Path);
        await WorktreeManager.AddAsync("branch3", _repo.Path);

        // Add a trunk commit so branches need syncing
        _repo.CommitFile("trunk-file.txt", "trunk content", "Trunk commit");

        var result = await StackManager.SyncAsync(_repo.Path);

        Assert.False(result.HasConflict);
        Assert.Equal(3, result.BranchResults.Count);
        Assert.True(result.BranchResults.All(b =>
            b.Status == SyncStatus.Merged || b.Status == SyncStatus.UpToDate));
    }

    [Fact]
    public async Task Sync_ConflictInWorktreeBranch_SavesWorktreePath()
    {
        _repo.InitGraftDir();
        var stack = await StackManager.InitAsync("test-stack", _repo.Path);

        // Create a branch with a conflicting file
        await _git.RunAsync("checkout", "-b", "conflict-branch");
        _repo.CommitFile("shared.txt", "branch version", "Branch change");

        // Register branch and return to trunk
        await _git.RunAsync("checkout", stack.Trunk);
        await StackManager.PushAsync("conflict-branch", _repo.Path);

        // Create a worktree for the conflict branch
        await _git.RunAsync("checkout", stack.Trunk);
        await WorktreeManager.AddAsync("conflict-branch", _repo.Path);

        // Create a conflicting trunk commit
        _repo.CommitFile("shared.txt", "trunk version", "Trunk change");

        var result = await StackManager.SyncAsync(_repo.Path);

        Assert.True(result.HasConflict);
        Assert.Contains(result.BranchResults, b => b.Status == SyncStatus.Conflict);

        // Verify operation state saved the correct worktree path
        var opState = StackManager.LoadOperationState(_repo.Path);
        Assert.NotNull(opState);
        Assert.NotNull(opState.WorktreePath);
        // Compare by directory name (symlink resolution can differ, e.g. /var vs /private/var on macOS)
        var expectedDirName = Path.GetFileName(WorktreeManager.GetWorktreePath("conflict-branch", _repo.Path));
        Assert.Equal(expectedDirName, Path.GetFileName(opState.WorktreePath));

        // Clean up: abort the merge in the worktree
        await StackManager.AbortSyncAsync(_repo.Path);
    }

    // ========================
    // graft stack commit -m <msg>
    // ========================

    [Fact]
    public async Task Commit_SpecificBranch_CommitsToBranch()
    {
        _repo.InitGraftDir();
        var stack = await StackManager.InitAsync("test-stack", _repo.Path);

        // Create a multi-branch stack
        await _git.RunAsync("checkout", "-b", "base-branch");
        _repo.CommitFile("base.txt", "base", "Base commit");
        await _git.RunAsync("checkout", "-b", "top-branch");
        _repo.CommitFile("top.txt", "top", "Top commit");

        await _git.RunAsync("checkout", stack.Trunk);
        await StackManager.PushAsync("base-branch", _repo.Path);
        await StackManager.PushAsync("top-branch", _repo.Path);

        // Stage a change for the base branch
        await _git.RunAsync("checkout", "base-branch");
        File.WriteAllText(Path.Combine(_repo.Path, "fix.txt"), "fix content");
        await _git.RunAsync("add", "fix.txt");

        var result = await CommitRouter.CommitAsync(
            "base-branch", "Fix something", _repo.Path);

        Assert.NotNull(result.CommitSha);
        Assert.True(result.BranchesAreStale);
    }

    [Fact]
    public async Task Commit_NoBranch_CommitsToTopBranch()
    {
        _repo.InitGraftDir();
        var stack = await StackManager.InitAsync("test-stack", _repo.Path);

        await _git.RunAsync("checkout", "-b", "top-branch");
        _repo.CommitFile("file.txt", "content", "Initial");

        await _git.RunAsync("checkout", stack.Trunk);
        await StackManager.PushAsync("top-branch", _repo.Path);

        // Stage a change
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "new content");
        await _git.RunAsync("add", "new.txt");

        var result = await CommitRouter.CommitAsync(
            null, "New commit", _repo.Path);

        Assert.NotNull(result.CommitSha);
        Assert.Equal("top-branch", result.OriginalBranch);
    }

    [Fact]
    public async Task Commit_NoStagedChanges_ThrowsError()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);
        await _git.RunAsync("checkout", "-b", "test-branch");
        _repo.CommitFile("file.txt", "content", "Initial");
        await _git.RunAsync("checkout", "master");
        await StackManager.PushAsync("test-branch", _repo.Path);
        await _git.RunAsync("checkout", "master");

        var status = await _git.RunAsync("diff", "--cached", "--name-only");
        Assert.True(string.IsNullOrEmpty(status.Stdout));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CommitRouter.CommitAsync(null, "Empty commit", _repo.Path));
    }

    [Fact]
    public async Task Commit_BranchNotInStack_ThrowsError()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);

        // Stage something so we pass the "no staged changes" check
        File.WriteAllText(Path.Combine(_repo.Path, "dummy.txt"), "dummy");
        await _git.RunAsync("add", "dummy.txt");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CommitRouter.CommitAsync("nonexistent-branch", "msg", _repo.Path));
    }

    // ========================
    // graft stack del <name>
    // ========================

    [Fact]
    public async Task Delete_ExistingStack_RemovesStackFileKeepsBranches()
    {
        var graftDir = _repo.InitGraftDir();

        await _git.RunAsync("checkout", "-b", "feature-branch");
        File.WriteAllText(
            Path.Combine(graftDir, "stacks", "my-stack.toml"),
            "name = \"my-stack\"\ntrunk = \"master\"\n\n[[branches]]\nname = \"feature-branch\"");

        Assert.True(File.Exists(Path.Combine(graftDir, "stacks", "my-stack.toml")));
        var branchList = await _git.RunAsync("branch", "--list", "feature-branch");
        Assert.Contains("feature-branch", branchList.Stdout);

        StackManager.Delete("my-stack", _repo.Path);

        Assert.False(File.Exists(Path.Combine(graftDir, "stacks", "my-stack.toml")));
        // Branch should still exist
        branchList = await _git.RunAsync("branch", "--list", "feature-branch");
        Assert.Contains("feature-branch", branchList.Stdout);
    }

    [Fact]
    public async Task Delete_ActiveStack_ClearsActiveStack()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("test-stack", _repo.Path);

        Assert.Equal("test-stack", ConfigLoader.LoadActiveStack(_repo.Path));

        StackManager.Delete("test-stack", _repo.Path);

        Assert.Null(ConfigLoader.LoadActiveStack(_repo.Path));
    }

    [Fact]
    public void Delete_NonExistentStack_ThrowsError()
    {
        _repo.InitGraftDir();

        Assert.ThrowsAny<Exception>(() =>
            StackManager.Delete("nonexistent", _repo.Path));
    }

    // ========================
    // graft --continue / --abort
    // ========================

    [Fact]
    public async Task Continue_AfterConflictResolution_ResumesOperation()
    {
        _repo.InitGraftDir();
        var stack = await StackManager.InitAsync("test-stack", _repo.Path);

        // Set up a conflict
        await _git.RunAsync("checkout", "-b", "conflict-branch");
        _repo.CommitFile("shared.txt", "branch version", "Branch change");
        await _git.RunAsync("checkout", stack.Trunk);
        await StackManager.PushAsync("conflict-branch", _repo.Path);

        await _git.RunAsync("checkout", stack.Trunk);
        _repo.CommitFile("shared.txt", "trunk version", "Trunk change");

        var result = await StackManager.SyncAsync(_repo.Path);
        Assert.True(result.HasConflict);

        // Resolve the conflict and continue via StackManager
        File.WriteAllText(Path.Combine(_repo.Path, "shared.txt"), "resolved version");
        await _git.RunAsync("add", "shared.txt");
        var continueResult = await StackManager.ContinueSyncAsync(_repo.Path);

        // Should complete without conflict
        Assert.False(continueResult.HasConflict);
    }

    [Fact]
    public async Task Abort_InProgressOperation_CleansUp()
    {
        _repo.InitGraftDir();
        var stack = await StackManager.InitAsync("test-stack", _repo.Path);

        await _git.RunAsync("checkout", "-b", "conflict-branch2");
        _repo.CommitFile("shared2.txt", "branch version", "Branch change");
        await _git.RunAsync("checkout", stack.Trunk);
        await StackManager.PushAsync("conflict-branch2", _repo.Path);

        await _git.RunAsync("checkout", stack.Trunk);
        _repo.CommitFile("shared2.txt", "trunk version", "Trunk change");

        var result = await StackManager.SyncAsync(_repo.Path);
        Assert.True(result.HasConflict);

        // Use StackManager.AbortSyncAsync which also cleans up operation state
        await StackManager.AbortSyncAsync(_repo.Path);

        // Verify operation state was cleaned up
        Assert.Null(StackManager.LoadOperationState(_repo.Path));
    }

    [Fact]
    public async Task Continue_NoInProgressOp_ThrowsError()
    {
        _repo.InitGraftDir();

        // No operation in progress — merge --continue should fail
        var result = await _git.RunAsync("merge", "--continue");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Abort_NoInProgressOp_ThrowsError()
    {
        _repo.InitGraftDir();

        var result = await _git.RunAsync("merge", "--abort");
        Assert.False(result.Success);
    }
}
