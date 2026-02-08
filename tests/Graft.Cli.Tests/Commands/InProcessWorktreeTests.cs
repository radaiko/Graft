using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// In-process tests for worktree commands.
/// </summary>
[Collection("InProcess")]
public sealed class InProcessWorktreeTests : IDisposable
{
    private readonly TempCliRepo _repo;

    public InProcessWorktreeTests()
    {
        _repo = TempCliRepo.CreateWithStack();
    }

    public void Dispose()
    {
        // Clean up worktree siblings
        var parentDir = Path.GetDirectoryName(_repo.Path);
        var repoName = Path.GetFileName(_repo.Path);
        if (parentDir != null)
        {
            foreach (var dir in Directory.GetDirectories(parentDir, $"{repoName}.wt.*"))
            {
                try
                {
                    SetAttributesNormal(new DirectoryInfo(dir));
                    Directory.Delete(dir, recursive: true);
                }
                catch { }
            }
        }
        _repo.Dispose();
    }

    private static void SetAttributesNormal(DirectoryInfo dir)
    {
        foreach (var sub in dir.GetDirectories())
            SetAttributesNormal(sub);
        foreach (var file in dir.GetFiles())
            file.Attributes = FileAttributes.Normal;
    }

    [Fact]
    public async Task WtList_ShowsWorktrees()
    {
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "wt", "list");

        Assert.Equal(0, result.ExitCode);
        // Should show at least the main worktree
        Assert.False(string.IsNullOrWhiteSpace(result.Stdout));
    }

    [Fact]
    public async Task WtAdd_ExistingBranch_CreatesWorktree()
    {
        // auth/base-types already exists from CreateWithStack
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "wt", "auth/session-manager");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task WtAdd_CreateBranch_CreatesWorktree()
    {
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "wt", "new-wt-branch", "-c");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task WtRemove_ExistingWorktree_Succeeds()
    {
        // Add a worktree, then remove it with --force (stdin redirected in tests)
        await InProcessCliRunner.RunAsync(_repo.Path, "wt", "rm-branch", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "wt", "remove", "rm-branch", "-f");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task WtRemove_WithForce_Succeeds()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "wt", "force-rm-branch", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "wt", "remove", "force-rm-branch", "-f");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task WtAdd_NonExistentBranch_WithoutCreate_ShowsError()
    {
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "wt", "ghost-branch-xyz");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task WtRemove_NonExistent_ShowsError()
    {
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "wt", "remove", "nonexistent-wt-branch");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task Wt_NoArgs_ShowsUsage()
    {
        var result = await InProcessCliRunner.RunAsync(_repo.Path, "wt");

        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        Assert.Contains("Usage", combined);
    }

    [Fact]
    public async Task WtRemove_WithRmAlias_Succeeds()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "wt", "rm-alias-branch", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "wt", "rm", "rm-alias-branch", "-f");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task WtRemove_WithoutForce_InputRedirected_ShowsError()
    {
        // Without --force and with redirected input, should fail
        await InProcessCliRunner.RunAsync(_repo.Path, "wt", "no-force-branch", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "wt", "remove", "no-force-branch");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Cannot prompt", result.Stderr);
    }
}
