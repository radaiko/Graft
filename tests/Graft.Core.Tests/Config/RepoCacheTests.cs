using Graft.Core.Config;
using Graft.Core.Scan;

namespace Graft.Core.Tests.Config;

public sealed class RepoCacheTests : IDisposable
{
    private readonly string _configDir;

    public RepoCacheTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDir))
            Directory.Delete(_configDir, recursive: true);
    }

    // ========================
    // Scan Paths
    // ========================

    [Fact]
    public void LoadScanPaths_NoFile_ReturnsEmpty()
    {
        var paths = ConfigLoader.LoadScanPaths(_configDir);
        Assert.Empty(paths);
    }

    [Fact]
    public void SaveThenLoad_ScanPaths_RoundTrip()
    {
        var paths = new List<ScanPath>
        {
            new() { Path = "/home/dev/projects" },
            new() { Path = "/home/dev/work" },
        };

        ConfigLoader.SaveScanPaths(paths, _configDir);
        var loaded = ConfigLoader.LoadScanPaths(_configDir);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("/home/dev/projects", loaded[0].Path);
        Assert.Equal("/home/dev/work", loaded[1].Path);
    }

    [Fact]
    public void SaveScanPaths_PreservesOtherKeys()
    {
        var configPath = Path.Combine(_configDir, "config.toml");
        File.WriteAllText(configPath, "some_other_key = \"value\"\n");

        var paths = new List<ScanPath> { new() { Path = "/tmp/test" } };
        ConfigLoader.SaveScanPaths(paths, _configDir);

        var content = File.ReadAllText(configPath);
        Assert.Contains("some_other_key", content);
        Assert.Contains("/tmp/test", content);
    }

    [Fact]
    public void SaveScanPaths_EmptyList_RemovesScanPathsKey()
    {
        // Save some paths first
        ConfigLoader.SaveScanPaths([new ScanPath { Path = "/tmp" }], _configDir);

        // Save empty list
        ConfigLoader.SaveScanPaths([], _configDir);

        var loaded = ConfigLoader.LoadScanPaths(_configDir);
        Assert.Empty(loaded);
    }

    // ========================
    // Repo Cache
    // ========================

    [Fact]
    public void LoadRepoCache_NoFile_ReturnsEmptyCache()
    {
        var cache = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Empty(cache.Repos);
    }

    [Fact]
    public void SaveThenLoad_RepoCache_RoundTrip()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo
        {
            Name = "Graft",
            Path = "/home/dev/Graft",
        });
        cache.Repos.Add(new CachedRepo
        {
            Name = "Graft.wt.feature-api",
            Path = "/home/dev/Graft.wt.feature-api",
            Branch = "feature/api",
        });

        ConfigLoader.SaveRepoCache(cache, _configDir);
        var loaded = ConfigLoader.LoadRepoCache(_configDir);

        Assert.Equal(2, loaded.Repos.Count);
        Assert.Equal("Graft", loaded.Repos[0].Name);
        Assert.Equal("/home/dev/Graft", loaded.Repos[0].Path);
        Assert.Null(loaded.Repos[0].Branch);
        Assert.Equal("Graft.wt.feature-api", loaded.Repos[1].Name);
        Assert.Equal("feature/api", loaded.Repos[1].Branch);
    }

    [Fact]
    public void LoadRepoCache_ValidToml_ParsesAllFields()
    {
        var cachePath = Path.Combine(_configDir, "repo-cache.toml");
        File.WriteAllText(cachePath, """
            [[repos]]
            name = "MyRepo"
            path = "/home/dev/MyRepo"
            auto_fetch = true

            [[repos]]
            name = "MyRepo.wt.dev"
            path = "/home/dev/MyRepo.wt.dev"
            branch = "dev"
            auto_fetch = false
            """);

        var cache = ConfigLoader.LoadRepoCache(_configDir);

        Assert.Equal(2, cache.Repos.Count);
        Assert.True(cache.Repos[0].AutoFetch);
        Assert.False(cache.Repos[1].AutoFetch);
        Assert.Equal("dev", cache.Repos[1].Branch);
    }

    // ========================
    // Add / Remove from cache
    // ========================

    [Fact]
    public void AddRepoToCache_NewRepo_AddsSuccessfully()
    {
        var repo = new CachedRepo { Name = "test-repo", Path = "/tmp/test-repo" };
        ConfigLoader.AddRepoToCache(repo, _configDir);

        var cache = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Single(cache.Repos);
        Assert.Equal("test-repo", cache.Repos[0].Name);
    }

    [Fact]
    public void AddRepoToCache_DuplicatePath_DoesNotAdd()
    {
        var repo = new CachedRepo { Name = "test-repo", Path = "/tmp/test-repo" };
        ConfigLoader.AddRepoToCache(repo, _configDir);
        ConfigLoader.AddRepoToCache(repo, _configDir);

        var cache = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Single(cache.Repos);
    }

    [Fact]
    public void RemoveRepoFromCache_ExistingRepo_RemovesSuccessfully()
    {
        var repo = new CachedRepo { Name = "test-repo", Path = "/tmp/test-repo" };
        ConfigLoader.AddRepoToCache(repo, _configDir);

        ConfigLoader.RemoveRepoFromCache("/tmp/test-repo", _configDir);

        var cache = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Empty(cache.Repos);
    }

    [Fact]
    public void RemoveRepoFromCache_NonexistentPath_DoesNothing()
    {
        var repo = new CachedRepo { Name = "test-repo", Path = "/tmp/test-repo" };
        ConfigLoader.AddRepoToCache(repo, _configDir);

        ConfigLoader.RemoveRepoFromCache("/tmp/nonexistent", _configDir);

        var cache = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Single(cache.Repos);
    }
}
