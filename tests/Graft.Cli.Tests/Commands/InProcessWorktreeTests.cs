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
}
