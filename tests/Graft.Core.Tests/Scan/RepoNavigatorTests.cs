using Graft.Core.Config;
using Graft.Core.Scan;

namespace Graft.Core.Tests.Scan;

public sealed class RepoNavigatorTests : IDisposable
{
    private readonly string _configDir;

    public RepoNavigatorTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), $"graft-config-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDir))
            Directory.Delete(_configDir, recursive: true);
    }

    private void SeedCache(params CachedRepo[] repos)
    {
        var cache = new RepoCache();
        cache.Repos.AddRange(repos);
        ConfigLoader.SaveRepoCache(cache, _configDir);
    }

    [Fact]
    public void FindByName_ExactRepoName_ReturnsSingleResult()
    {
        SeedCache(
            new CachedRepo { Name = "Graft", Path = "/home/dev/Graft" },
            new CachedRepo { Name = "Other", Path = "/home/dev/Other" });

        var results = RepoNavigator.FindByName("Graft", _configDir);

        Assert.Single(results);
        Assert.Equal("/home/dev/Graft", results[0].Path);
    }

    [Fact]
    public void FindByName_BranchName_ReturnsWorktreeResult()
    {
        SeedCache(
            new CachedRepo { Name = "Graft", Path = "/home/dev/Graft" },
            new CachedRepo { Name = "Graft.wt.feature-api", Path = "/home/dev/Graft.wt.feature-api", Branch = "feature/api" });

        var results = RepoNavigator.FindByName("feature/api", _configDir);

        Assert.Single(results);
        Assert.Equal("Graft.wt.feature-api", results[0].Name);
        Assert.Equal("feature/api", results[0].Branch);
    }

    [Fact]
    public void FindByName_NoMatch_ReturnsEmpty()
    {
        SeedCache(new CachedRepo { Name = "Graft", Path = "/home/dev/Graft" });

        var results = RepoNavigator.FindByName("nonexistent", _configDir);
        Assert.Empty(results);
    }

    [Fact]
    public void FindByName_MultipleMatches_ReturnsAll()
    {
        SeedCache(
            new CachedRepo { Name = "Graft", Path = "/home/dev/Graft" },
            new CachedRepo { Name = "Graft", Path = "/home/work/Graft" });

        var results = RepoNavigator.FindByName("Graft", _configDir);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FindByName_CaseInsensitive_Matches()
    {
        SeedCache(new CachedRepo { Name = "Graft", Path = "/home/dev/Graft" });

        var results = RepoNavigator.FindByName("graft", _configDir);
        Assert.Single(results);
    }

    [Fact]
    public void FindByName_PrefersRepoNameOverBranch()
    {
        SeedCache(
            new CachedRepo { Name = "dev", Path = "/home/dev" },
            new CachedRepo { Name = "Graft.wt.dev", Path = "/home/Graft.wt.dev", Branch = "dev" });

        // Should match on repo name first
        var results = RepoNavigator.FindByName("dev", _configDir);
        Assert.Single(results);
        Assert.Equal("/home/dev", results[0].Path);
    }

    [Fact]
    public void GetAllAsPickerItems_ReturnsAllCachedRepos()
    {
        SeedCache(
            new CachedRepo { Name = "Graft", Path = "/home/dev/Graft" },
            new CachedRepo { Name = "Graft.wt.feature", Path = "/home/dev/Graft.wt.feature", Branch = "feature" });

        var items = RepoNavigator.GetAllAsPickerItems(_configDir);

        Assert.Equal(2, items.Count);
        Assert.Equal("Graft", items[0].Label);
        Assert.Equal("/home/dev/Graft", items[0].Description);
        Assert.Equal("Graft.wt.feature", items[1].Label);
        Assert.Equal("[feature]", items[1].Description);
    }

    [Fact]
    public void GetAllAsPickerItems_EmptyCache_ReturnsEmpty()
    {
        var items = RepoNavigator.GetAllAsPickerItems(_configDir);
        Assert.Empty(items);
    }
}
