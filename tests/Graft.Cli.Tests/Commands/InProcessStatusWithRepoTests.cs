using System.Diagnostics;
using Graft.Cli.Tests.Helpers;
using Graft.Core.Config;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// In-process tests for the status command with a real repo in the cache.
/// Creates a temp config dir and populates it with a repo cache entry pointing
/// to a test repo. Overrides HOME env var so CliPaths.GetConfigDir() resolves
/// to our temp dir.
/// </summary>
[Collection("InProcess")]
public sealed class InProcessStatusWithRepoTests : IDisposable
{
    private readonly TempCliRepo _repo;
    private readonly string _tempHome;
    private readonly string _configDir;

    public InProcessStatusWithRepoTests()
    {
        _repo = TempCliRepo.CreateWithStack();
        _tempHome = Path.Combine(Path.GetTempPath(), $"graft-home-{Guid.NewGuid():N}");
        _configDir = Path.Combine(_tempHome, ".config", "graft");
        Directory.CreateDirectory(_configDir);

        // Write a repo cache with our test repo
        var repoName = Path.GetFileName(_repo.Path);
        var cacheToml = $"""
            [[repos]]
            name = "{repoName}"
            path = "{_repo.Path.Replace("\\", "\\\\")}"
            auto_fetch = false
            """;
        File.WriteAllText(Path.Combine(_configDir, "repo-cache.toml"), cacheToml);
    }

    public void Dispose()
    {
        _repo.Dispose();
        if (Directory.Exists(_tempHome))
        {
            try { Directory.Delete(_tempHome, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Status_WithRepoInCache_ShowsOverview()
    {
        var repoName = Path.GetFileName(_repo.Path);
        var originalHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Override HOME so CliPaths.GetConfigDir() reads our temp config
        Environment.SetEnvironmentVariable("HOME", _tempHome);
        try
        {
            var result = await InProcessCliRunner.RunAsync(null, "status");

            Assert.Equal(0, result.ExitCode);
            // Should show repo info: name, branch, status, stack, worktrees
            Assert.Contains(repoName, result.Stdout);
            Assert.Contains("branch", result.Stdout);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
        }
    }

    [Fact]
    public async Task Status_DetailedForRepo_ShowsDetail()
    {
        var repoName = Path.GetFileName(_repo.Path);
        var originalHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Environment.SetEnvironmentVariable("HOME", _tempHome);
        try
        {
            var result = await InProcessCliRunner.RunAsync(null, "status", repoName);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(repoName, result.Stdout);
            // Detailed status shows upstream, changed, untracked, stacks, worktrees
            Assert.Contains("branch", result.Stdout);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
        }
    }

    [Fact]
    public async Task Status_NonexistentRepoName_ShowsError()
    {
        var originalHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Environment.SetEnvironmentVariable("HOME", _tempHome);
        try
        {
            var result = await InProcessCliRunner.RunAsync(null, "status", "no-such-repo");

            Assert.NotEqual(0, result.ExitCode);
            var combined = result.Stdout + result.Stderr;
            Assert.Contains("Error", combined);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
        }
    }
}
