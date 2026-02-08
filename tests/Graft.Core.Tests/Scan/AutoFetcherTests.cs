using Graft.Core.Config;
using Graft.Core.Scan;

namespace Graft.Core.Tests.Scan;

public sealed class AutoFetcherTests : IDisposable
{
    private readonly string _configDir;

    public AutoFetcherTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), $"graft-af-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDir))
            Directory.Delete(_configDir, recursive: true);
    }

    // ========================
    // GetDueRepos
    // ========================

    [Fact]
    public void GetDueRepos_NoAutoFetchRepos_ReturnsEmpty()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo { Name = "repo-a", Path = "/tmp/a", AutoFetch = false });

        var due = AutoFetcher.GetDueRepos(cache, TimeSpan.FromMinutes(15));
        Assert.Empty(due);
    }

    [Fact]
    public void GetDueRepos_NeverFetched_ReturnsDue()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo { Name = "repo-a", Path = "/tmp/a", AutoFetch = true });

        var due = AutoFetcher.GetDueRepos(cache, TimeSpan.FromMinutes(15));
        Assert.Single(due);
        Assert.Equal("repo-a", due[0].Name);
    }

    [Fact]
    public void GetDueRepos_RecentlyFetched_ReturnsEmpty()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo
        {
            Name = "repo-a",
            Path = "/tmp/a",
            AutoFetch = true,
            LastFetched = DateTime.UtcNow.AddMinutes(-5),
        });

        var due = AutoFetcher.GetDueRepos(cache, TimeSpan.FromMinutes(15));
        Assert.Empty(due);
    }

    [Fact]
    public void GetDueRepos_FetchedLongAgo_ReturnsDue()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo
        {
            Name = "repo-a",
            Path = "/tmp/a",
            AutoFetch = true,
            LastFetched = DateTime.UtcNow.AddMinutes(-20),
        });

        var due = AutoFetcher.GetDueRepos(cache, TimeSpan.FromMinutes(15));
        Assert.Single(due);
    }

    [Fact]
    public void GetDueRepos_MixedRepos_ReturnsOnlyDue()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo
        {
            Name = "fresh",
            Path = "/tmp/fresh",
            AutoFetch = true,
            LastFetched = DateTime.UtcNow.AddMinutes(-2),
        });
        cache.Repos.Add(new CachedRepo
        {
            Name = "stale",
            Path = "/tmp/stale",
            AutoFetch = true,
            LastFetched = DateTime.UtcNow.AddMinutes(-30),
        });
        cache.Repos.Add(new CachedRepo
        {
            Name = "disabled",
            Path = "/tmp/disabled",
            AutoFetch = false,
        });
        cache.Repos.Add(new CachedRepo
        {
            Name = "never-fetched",
            Path = "/tmp/never",
            AutoFetch = true,
        });

        var due = AutoFetcher.GetDueRepos(cache, TimeSpan.FromMinutes(15));
        Assert.Equal(2, due.Count);
        Assert.Contains(due, r => r.Name == "stale");
        Assert.Contains(due, r => r.Name == "never-fetched");
    }

    // ========================
    // Enable / Disable by name
    // ========================

    [Fact]
    public void EnableByName_ExistingRepo_SetsAutoFetch()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo { Name = "my-repo", Path = "/tmp/my-repo" });
        ConfigLoader.SaveRepoCache(cache, _configDir);

        AutoFetcher.EnableByName("my-repo", _configDir);

        var updated = ConfigLoader.LoadRepoCache(_configDir);
        Assert.True(updated.Repos[0].AutoFetch);
    }

    [Fact]
    public void DisableByName_EnabledRepo_ClearsAutoFetchAndLastFetched()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo
        {
            Name = "my-repo",
            Path = "/tmp/my-repo",
            AutoFetch = true,
            LastFetched = DateTime.UtcNow,
        });
        ConfigLoader.SaveRepoCache(cache, _configDir);

        AutoFetcher.DisableByName("my-repo", _configDir);

        var updated = ConfigLoader.LoadRepoCache(_configDir);
        Assert.False(updated.Repos[0].AutoFetch);
        Assert.Null(updated.Repos[0].LastFetched);
    }

    [Fact]
    public void EnableByName_NonexistentRepo_Throws()
    {
        var cache = new RepoCache();
        ConfigLoader.SaveRepoCache(cache, _configDir);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AutoFetcher.EnableByName("no-such-repo", _configDir));
        Assert.Contains("not found in cache", ex.Message);
    }

    [Fact]
    public void EnableByName_CaseInsensitive_Matches()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo { Name = "MyRepo", Path = "/tmp/MyRepo" });
        ConfigLoader.SaveRepoCache(cache, _configDir);

        AutoFetcher.EnableByName("myrepo", _configDir);

        var updated = ConfigLoader.LoadRepoCache(_configDir);
        Assert.True(updated.Repos[0].AutoFetch);
    }

    // ========================
    // Enable / Disable by path
    // ========================

    [Fact]
    public void Enable_ExistingRepoByPath_SetsAutoFetch()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo { Name = "my-repo", Path = "/tmp/my-repo" });
        ConfigLoader.SaveRepoCache(cache, _configDir);

        AutoFetcher.Enable("/tmp/my-repo", _configDir);

        var updated = ConfigLoader.LoadRepoCache(_configDir);
        Assert.True(updated.Repos[0].AutoFetch);
    }

    [Fact]
    public void Disable_ExistingRepoByPath_ClearsAutoFetch()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo
        {
            Name = "my-repo",
            Path = "/tmp/my-repo",
            AutoFetch = true,
            LastFetched = DateTime.UtcNow,
        });
        ConfigLoader.SaveRepoCache(cache, _configDir);

        AutoFetcher.Disable("/tmp/my-repo", _configDir);

        var updated = ConfigLoader.LoadRepoCache(_configDir);
        Assert.False(updated.Repos[0].AutoFetch);
        Assert.Null(updated.Repos[0].LastFetched);
    }

    [Fact]
    public void Enable_NonexistentPath_Throws()
    {
        var cache = new RepoCache();
        ConfigLoader.SaveRepoCache(cache, _configDir);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AutoFetcher.Enable("/tmp/nonexistent", _configDir));
        Assert.Contains("not found in cache", ex.Message);
    }

    // ========================
    // LastFetched persistence round-trip
    // ========================

    [Fact]
    public void LastFetched_PersistsAcrossSaveLoad()
    {
        var now = DateTime.UtcNow;
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo
        {
            Name = "repo",
            Path = "/tmp/repo",
            AutoFetch = true,
            LastFetched = now,
        });

        ConfigLoader.SaveRepoCache(cache, _configDir);
        var loaded = ConfigLoader.LoadRepoCache(_configDir);

        Assert.NotNull(loaded.Repos[0].LastFetched);
        // Allow 1 second tolerance for round-trip serialization
        Assert.True(Math.Abs((loaded.Repos[0].LastFetched!.Value - now).TotalSeconds) < 1);
    }

    [Fact]
    public void LastFetched_Null_NotPersistedInToml()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo
        {
            Name = "repo",
            Path = "/tmp/repo",
        });

        ConfigLoader.SaveRepoCache(cache, _configDir);
        var loaded = ConfigLoader.LoadRepoCache(_configDir);

        Assert.Null(loaded.Repos[0].LastFetched);
    }
}
