using Graft.Core.Config;
using Graft.Core.Scan;
using Graft.Core.Stack;
using Graft.Core.Status;
using Graft.Core.Tests.Helpers;

namespace Graft.Core.Tests.Status;

public sealed class StatusCollectorTests : IDisposable
{
    private readonly TempGitRepo _repo;

    public StatusCollectorTests()
    {
        _repo = new TempGitRepo();
        // Track the .test-gitconfig file so it doesn't show as untracked
        _repo.RunGit("add", ".test-gitconfig");
        _repo.RunGit("commit", "-m", "track test-gitconfig");
    }

    public void Dispose()
    {
        _repo.Dispose();
    }

    [Fact]
    public async Task CollectOne_CleanRepo_ShowsBranchAndZeroChanges()
    {
        var status = await StatusCollector.CollectOneAsync(_repo.Path);

        Assert.True(status.IsAccessible);
        Assert.Equal("master", status.Branch);
        Assert.Equal(0, status.ChangedFiles);
        Assert.Equal(0, status.UntrackedFiles);
        Assert.Equal(0, status.Ahead);
        Assert.Equal(0, status.Behind);
        Assert.Null(status.ActiveStackName);
    }

    [Fact]
    public async Task CollectOne_ModifiedFiles_ShowsCorrectChangedCount()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "file1.txt"), "hello");
        _repo.RunGit("add", "file1.txt");

        var status = await StatusCollector.CollectOneAsync(_repo.Path);

        Assert.True(status.IsAccessible);
        Assert.Equal(1, status.ChangedFiles);
        Assert.Equal(0, status.UntrackedFiles);
    }

    [Fact]
    public async Task CollectOne_UntrackedFiles_ShowsCorrectCount()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "untracked.txt"), "hello");

        var status = await StatusCollector.CollectOneAsync(_repo.Path);

        Assert.True(status.IsAccessible);
        Assert.Equal(0, status.ChangedFiles);
        Assert.Equal(1, status.UntrackedFiles);
    }

    [Fact]
    public async Task CollectOne_WithActiveStack_ShowsStackNameAndBranchCount()
    {
        _repo.InitGraftDir();

        var stack = new StackDefinition
        {
            Name = "my-stack",
            Trunk = "master",
            Branches =
            [
                new StackBranch { Name = "feature-a" },
                new StackBranch { Name = "feature-b" },
            ],
        };
        ConfigLoader.SaveStack(stack, _repo.Path);
        ConfigLoader.SaveActiveStack("my-stack", _repo.Path);

        // Create the branches so git operations work
        _repo.RunGit("branch", "feature-a");
        _repo.RunGit("branch", "feature-b");

        var status = await StatusCollector.CollectOneAsync(_repo.Path);

        Assert.True(status.IsAccessible);
        Assert.Equal("my-stack", status.ActiveStackName);
        Assert.Equal(2, status.ActiveStackBranchCount);
    }

    [Fact]
    public async Task CollectOne_NoGraftDir_ShowsNoStack()
    {
        var status = await StatusCollector.CollectOneAsync(_repo.Path);

        Assert.True(status.IsAccessible);
        Assert.Null(status.ActiveStackName);
        Assert.Equal(0, status.ActiveStackBranchCount);
        Assert.Empty(status.Stacks);
    }

    [Fact]
    public async Task CollectOne_DetailedWithStacks_ShowsStackBranches()
    {
        _repo.InitGraftDir();

        var stack = new StackDefinition
        {
            Name = "test-stack",
            Trunk = "master",
            Branches =
            [
                new StackBranch { Name = "feature-a" },
            ],
        };
        ConfigLoader.SaveStack(stack, _repo.Path);
        ConfigLoader.SaveActiveStack("test-stack", _repo.Path);

        _repo.RunGit("branch", "feature-a");

        var status = await StatusCollector.CollectOneAsync(_repo.Path);

        Assert.Single(status.Stacks);
        Assert.Equal("test-stack", status.Stacks[0].Name);
        Assert.True(status.Stacks[0].IsActive);
        Assert.Single(status.Stacks[0].Branches);
        Assert.Equal("feature-a", status.Stacks[0].Branches[0].Name);
    }

    [Fact]
    public async Task CollectOne_NonexistentPath_ReturnsInaccessible()
    {
        var badPath = Path.Combine(Path.GetTempPath(), $"graft-nonexistent-{Guid.NewGuid():N}");

        var status = await StatusCollector.CollectOneAsync(badPath);

        Assert.False(status.IsAccessible);
        Assert.NotNull(status.Error);
    }

    [Fact]
    public async Task CollectAll_EmptyCache_ReturnsEmptyList()
    {
        var configDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(configDir);

        try
        {
            var statuses = await StatusCollector.CollectAllAsync(configDir);
            Assert.Empty(statuses);
        }
        finally
        {
            Directory.Delete(configDir, recursive: true);
        }
    }

    [Fact]
    public async Task CollectAll_WithCachedRepo_ReturnsStatus()
    {
        var configDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(configDir);

        try
        {
            var cache = new RepoCache();
            cache.Repos.Add(new CachedRepo { Name = "test-repo", Path = _repo.Path });
            ConfigLoader.SaveRepoCache(cache, configDir);

            var statuses = await StatusCollector.CollectAllAsync(configDir);

            Assert.Single(statuses);
            Assert.Equal("test-repo", statuses[0].Name);
            Assert.True(statuses[0].IsAccessible);
            Assert.Equal("master", statuses[0].Branch);
        }
        finally
        {
            Directory.Delete(configDir, recursive: true);
        }
    }

    [Fact]
    public async Task CollectAll_NonexistentRepoPath_IsFilteredOut()
    {
        var configDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(configDir);

        try
        {
            var cache = new RepoCache();
            cache.Repos.Add(new CachedRepo
            {
                Name = "gone-repo",
                Path = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}"),
            });
            ConfigLoader.SaveRepoCache(cache, configDir);

            var statuses = await StatusCollector.CollectAllAsync(configDir);

            Assert.Empty(statuses);
        }
        finally
        {
            Directory.Delete(configDir, recursive: true);
        }
    }

    [Fact]
    public async Task CollectOne_WithWorktree_ShowsWorktreeCount()
    {
        _repo.RunGit("branch", "wt-branch");
        var wtPath = Path.Combine(
            Path.GetDirectoryName(_repo.Path)!,
            $"{Path.GetFileName(_repo.Path)}.wt.wt-branch");
        _repo.RunGit("worktree", "add", wtPath, "wt-branch");

        var status = await StatusCollector.CollectOneAsync(_repo.Path);

        Assert.True(status.IsAccessible);
        Assert.Single(status.Worktrees);
        Assert.Equal("wt-branch", status.Worktrees[0].Branch);
    }
}
