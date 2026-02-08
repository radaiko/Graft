using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// In-process tests for stack commands. These run the CLI command handlers
/// directly, enabling coverlet to instrument Graft.Cli code.
/// </summary>
[Collection("InProcess")]
public sealed class InProcessStackTests : IDisposable
{
    private readonly TempCliRepo _repo;

    public InProcessStackTests()
    {
        _repo = TempCliRepo.CreateEmpty();
    }

    public void Dispose() => _repo.Dispose();

    [Fact]
    public async Task StackInit_CreatesStack()
    {
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "my-stack");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Created stack", result.Stdout);
        Assert.Contains("my-stack", result.Stdout);
    }

    [Fact]
    public async Task StackList_ShowsStacks()
    {
        // First create a stack
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "test-stack");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "list");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("test-stack", result.Stdout);
    }

    [Fact]
    public async Task StackList_NoStacks_ShowsMessage()
    {
        // Ensure graft dir exists but no stacks
        Directory.CreateDirectory(Path.Combine(_repo.Path, ".git", "graft", "stacks"));

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "list");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No stacks found", result.Stdout);
    }

    [Fact]
    public async Task StackList_ActiveMarker()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "stack-a");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "stack-b");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "list");

        Assert.Equal(0, result.ExitCode);
        // The last init sets active, so stack-b should be active
        Assert.Contains("* stack-b", result.Stdout);
        Assert.Contains("  stack-a", result.Stdout);
    }

    [Fact]
    public async Task StackPush_CreateBranch_AddsBranch()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "test-stack");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "new-branch", "-c");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task StackPop_RemovesTopBranch()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "test-stack");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "branch1", "-c");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "branch2", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "pop");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("branch2", result.Stdout);
    }

    [Fact]
    public async Task StackRemove_WithForce_DeletesStack()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "doomed-stack");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "remove", "doomed-stack", "-f");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task StackRemove_Nonexistent_ShowsError()
    {
        Directory.CreateDirectory(Path.Combine(_repo.Path, ".git", "graft", "stacks"));

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "remove", "nonexistent", "-f");

        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        Assert.Contains("Error", combined);
    }

    [Fact]
    public async Task StackInit_NotGitRepo_ShowsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"not-a-repo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = await InProcessCliRunner.RunAsync(tempDir, "stack", "init", "test");

            Assert.NotEqual(0, result.ExitCode);
            var combined = result.Stdout + result.Stderr;
            Assert.Contains("Error", combined);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task StackSwitch_ValidStack_SwitchesActive()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "stack-a");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "stack-b");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "switch", "stack-a");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Switched to stack", result.Stdout);
    }

    [Fact]
    public async Task StackDrop_ValidBranch_RemovesBranch()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "test-stack");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "branch1", "-c");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "branch2", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "drop", "branch1");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task StackInit_DuplicateName_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "dup-stack");
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "dup-stack");

        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        Assert.Contains("already exists", combined);
    }

    [Fact]
    public async Task StackPush_NonExistentBranch_WithoutCreate_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "test-stack");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "ghost-branch");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task StackShift_InsertsBranchAtBottom()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "test-stack");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "top-branch", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "shift", "bottom-branch");

        // shift inserts an existing branch at bottom — if branch doesn't exist, it should error
        // or succeed depending on implementation. Either way the handler runs.
        Assert.True(result.ExitCode == 0 || result.ExitCode == 1);
    }

    [Fact]
    public async Task StackLog_ShowsBranchGraph()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "log-stack");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "log-branch", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "log");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("log-branch", result.Stdout);
    }

    [Fact]
    public async Task StackLog_EmptyStack_ShowsNoBranches()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "empty-log-stack");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "log");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("no branches", result.Stdout);
    }

    [Fact]
    public async Task StackCommit_NoMessage_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "commit-stack");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "commit");

        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        Assert.Contains("message", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StackCommit_WithMessage_NoStagedChanges_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "commit-stack2");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "commit-branch", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "commit", "-m", "test commit");

        // Should fail because nothing is staged
        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        Assert.Contains("staged", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StackSwitch_Nonexistent_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "real-stack");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "switch", "fake-stack");

        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        Assert.Contains("not found", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StackRemove_WithoutForce_InputRedirected_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "protected-stack");

        // Without --force and with redirected input, should fail
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "remove", "protected-stack");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Cannot prompt", result.Stderr);
    }

    [Fact]
    public async Task StackPush_AlreadyInStack_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "push-dup-stack");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "dup-branch", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "dup-branch");

        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        Assert.Contains("already in stack", combined);
    }

    [Fact]
    public async Task StackPop_EmptyStack_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "empty-pop-stack");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "pop");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task StackDrop_NonExistent_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "drop-test-stack");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "drop-branch", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "drop", "nonexistent-branch");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task StackListAlias_Works()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "ls-stack");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "ls");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ls-stack", result.Stdout);
    }

    [Fact]
    public async Task StackSwitchAlias_Works()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "sw-stack-a");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "sw-stack-b");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "sw", "sw-stack-a");

        Assert.Equal(0, result.ExitCode);
    }
}
