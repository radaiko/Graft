using Graft.Core.Config;
using Graft.Core.Scan;

namespace Graft.Core.Tests.Scan;

public sealed class RepoScannerTests : IDisposable
{
    private readonly string _scanDir;
    private readonly string _configDir;

    public RepoScannerTests()
    {
        _scanDir = Path.Combine(Path.GetTempPath(), $"graft-scan-test-{Guid.NewGuid():N}");
        _configDir = Path.Combine(Path.GetTempPath(), $"graft-config-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_scanDir);
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_scanDir))
            Directory.Delete(_scanDir, recursive: true);
        if (Directory.Exists(_configDir))
            Directory.Delete(_configDir, recursive: true);
    }

    private string CreateFakeGitRepo(string name)
    {
        var repoDir = Path.Combine(_scanDir, name);
        Directory.CreateDirectory(repoDir);
        Directory.CreateDirectory(Path.Combine(repoDir, ".git"));
        return repoDir;
    }

    [Fact]
    public void ScanDirectory_FindsGitRepos()
    {
        CreateFakeGitRepo("repo-a");
        CreateFakeGitRepo("repo-b");

        var repos = RepoScanner.ScanDirectory(_scanDir);

        Assert.Equal(2, repos.Count);
        Assert.Contains(repos, r => r.Name == "repo-a");
        Assert.Contains(repos, r => r.Name == "repo-b");
    }

    [Fact]
    public void ScanDirectory_RespectsMaxDepth()
    {
        // Create a repo at depth 2
        var nestedDir = Path.Combine(_scanDir, "level1", "level2");
        Directory.CreateDirectory(nestedDir);
        Directory.CreateDirectory(Path.Combine(nestedDir, ".git"));

        // maxDepth 1: should NOT find it (need to go 2 levels deep)
        var shallow = RepoScanner.ScanDirectory(_scanDir, maxDepth: 1);
        Assert.Empty(shallow);

        // maxDepth 2: should find it
        var deep = RepoScanner.ScanDirectory(_scanDir, maxDepth: 2);
        Assert.Single(deep);
    }

    [Fact]
    public void ScanDirectory_SkipsHiddenDirs()
    {
        var hiddenDir = Path.Combine(_scanDir, ".hidden-project");
        Directory.CreateDirectory(hiddenDir);
        Directory.CreateDirectory(Path.Combine(hiddenDir, ".git"));

        var repos = RepoScanner.ScanDirectory(_scanDir);
        Assert.Empty(repos);
    }

    [Fact]
    public void ScanDirectory_SkipsNodeModules()
    {
        var nmDir = Path.Combine(_scanDir, "node_modules", "some-package");
        Directory.CreateDirectory(nmDir);
        Directory.CreateDirectory(Path.Combine(nmDir, ".git"));

        var repos = RepoScanner.ScanDirectory(_scanDir);
        Assert.Empty(repos);
    }

    [Fact]
    public void ScanDirectory_DoesNotRecurseIntoGitRepos()
    {
        // Create a repo with a nested "repo" inside (like submodule dir with .git)
        var outerDir = Path.Combine(_scanDir, "outer");
        Directory.CreateDirectory(outerDir);
        Directory.CreateDirectory(Path.Combine(outerDir, ".git"));

        var innerDir = Path.Combine(outerDir, "inner");
        Directory.CreateDirectory(innerDir);
        Directory.CreateDirectory(Path.Combine(innerDir, ".git"));

        var repos = RepoScanner.ScanDirectory(_scanDir);
        // Should only find outer, not inner
        Assert.Single(repos);
        Assert.Equal("outer", repos[0].Name);
    }

    [Fact]
    public void ScanAndUpdateCache_PrunesStaleEntries()
    {
        // Add a repo that doesn't exist to the cache
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo { Name = "gone-repo", Path = "/nonexistent/path" });
        ConfigLoader.SaveRepoCache(cache, _configDir);

        // Register scan dir with a real repo
        CreateFakeGitRepo("real-repo");
        ConfigLoader.SaveScanPaths([new ScanPath { Path = _scanDir }], _configDir);

        RepoScanner.ScanAndUpdateCache(_configDir);

        var updated = ConfigLoader.LoadRepoCache(_configDir);
        Assert.DoesNotContain(updated.Repos, r => r.Name == "gone-repo");
        Assert.Contains(updated.Repos, r => r.Name == "real-repo");
    }

    [Fact]
    public void ScanAndUpdateCache_MergesWithoutDuplicates()
    {
        CreateFakeGitRepo("existing-repo");
        var repoPath = Path.Combine(_scanDir, "existing-repo");

        // Pre-populate cache with this repo
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo { Name = "existing-repo", Path = repoPath });
        ConfigLoader.SaveRepoCache(cache, _configDir);
        ConfigLoader.SaveScanPaths([new ScanPath { Path = _scanDir }], _configDir);

        RepoScanner.ScanAndUpdateCache(_configDir);

        var updated = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Single(updated.Repos);
    }

    [Fact]
    public void ScanAndUpdateCache_NoScanPaths_DoesNothing()
    {
        // No scan paths registered — should not error
        RepoScanner.ScanAndUpdateCache(_configDir);

        var cache = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Empty(cache.Repos);
    }
}
