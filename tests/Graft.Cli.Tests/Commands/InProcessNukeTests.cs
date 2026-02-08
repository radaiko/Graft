using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// In-process tests for nuke commands.
/// Uses stdin override to provide confirmation ("y"/"n") to prompts.
/// </summary>
[Collection("InProcess")]
public sealed class InProcessNukeTests : IDisposable
{
    private readonly TempCliRepo _repo;

    public InProcessNukeTests()
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
    public async Task NukeStack_WithConfirmation_RemovesStacks()
    {
        var result = await InProcessCliRunner.RunWithStdinAsync(_repo.Path, "y\n", "nuke", "stack");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task NukeStack_Declined_Aborts()
    {
        var result = await InProcessCliRunner.RunWithStdinAsync(_repo.Path, "n\n", "nuke", "stack");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Aborted", result.Stdout);
    }

    [Fact]
    public async Task NukeWt_WithConfirmation_RemovesWorktrees()
    {
        // Add a worktree first
        await InProcessCliRunner.RunAsync(_repo.Path, "wt", "wt-nuke-test", "-c");

        var result = await InProcessCliRunner.RunWithStdinAsync(_repo.Path, "y\n", "nuke", "wt");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task NukeWt_Declined_Aborts()
    {
        var result = await InProcessCliRunner.RunWithStdinAsync(_repo.Path, "n\n", "nuke", "wt");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Aborted", result.Stdout);
    }

    [Fact]
    public async Task NukeBranches_WithConfirmation_NothingToRemove()
    {
        // No gone branches in a local-only repo
        var result = await InProcessCliRunner.RunWithStdinAsync(_repo.Path, "y\n", "nuke", "branches");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Nothing to remove", result.Stdout);
    }

    [Fact]
    public async Task NukeBranches_Declined_Aborts()
    {
        var result = await InProcessCliRunner.RunWithStdinAsync(_repo.Path, "n\n", "nuke", "branches");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Aborted", result.Stdout);
    }

    [Fact]
    public async Task NukeAll_WithConfirmation_Succeeds()
    {
        var result = await InProcessCliRunner.RunWithStdinAsync(_repo.Path, "y\n", "nuke");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task NukeAll_Declined_Aborts()
    {
        var result = await InProcessCliRunner.RunWithStdinAsync(_repo.Path, "n\n", "nuke");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Aborted", result.Stdout);
    }
}
