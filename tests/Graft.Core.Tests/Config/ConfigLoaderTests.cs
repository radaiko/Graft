using Graft.Core.Config;
using Graft.Core.Scan;
using Graft.Core.Stack;
using Graft.Core.Tests.Helpers;

namespace Graft.Core.Tests.Config;

/// <summary>
/// Tests for loading and persisting TOML configuration files.
/// </summary>
public sealed class ConfigLoaderTests : IDisposable
{
    private readonly TempGitRepo _repo = new();
    private readonly string _configDir;

    public ConfigLoaderTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), $"graft-config-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        _repo.Dispose();
        if (Directory.Exists(_configDir))
            Directory.Delete(_configDir, recursive: true);
    }

    // Requirement: Stack definitions stored in .git/graft/stacks/<name>.toml
    [Fact]
    public void LoadStack_ValidToml_ShouldReturnStackDefinition()
    {
        _repo.InitGraftDir();
        var stackPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "auth-refactor.toml");
        File.WriteAllText(stackPath, """
            name = "auth-refactor"
            created_at = "2025-02-01T10:00:00Z"
            trunk = "main"

            [[branches]]
            name = "auth/base-types"

            [[branches]]
            name = "auth/session-manager"
            """);
        Assert.True(File.Exists(stackPath));

        var stack = ConfigLoader.LoadStack("auth-refactor", _repo.Path);
        Assert.Equal("auth-refactor", stack.Name);
        Assert.Equal("main", stack.Trunk);
        Assert.Equal(2, stack.Branches.Count);
        Assert.Equal("auth/base-types", stack.Branches[0].Name);
        Assert.Equal("auth/session-manager", stack.Branches[1].Name);
    }

    // Requirement: Stack with PR references
    [Fact]
    public void LoadStack_WithPrRefs_ShouldParsePrData()
    {
        _repo.InitGraftDir();
        var stackPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "with-prs.toml");
        File.WriteAllText(stackPath, """
            name = "with-prs"
            trunk = "main"

            [[branches]]
            name = "auth/base-types"
            pr_number = 123
            pr_url = "https://github.com/org/repo/pull/123"
            """);
        Assert.True(File.Exists(stackPath));

        var stack = ConfigLoader.LoadStack("with-prs", _repo.Path);
        Assert.NotNull(stack.Branches[0].Pr);
        Assert.Equal(123UL, stack.Branches[0].Pr!.Number);
        Assert.Equal("https://github.com/org/repo/pull/123", stack.Branches[0].Pr!.Url);
    }

    // Requirement: Stack definition can be saved
    [Fact]
    public void SaveStack_ShouldWriteTomlFile()
    {
        _repo.InitGraftDir();
        var expectedPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "new-stack.toml");
        Assert.False(File.Exists(expectedPath));

        var stack = new StackDefinition { Name = "new-stack", Trunk = "main" };
        ConfigLoader.SaveStack(stack, _repo.Path);

        Assert.True(File.Exists(expectedPath));
        var content = File.ReadAllText(expectedPath);
        Assert.Contains("new-stack", content);
        Assert.Contains("main", content);
    }

    // Edge case: Malformed TOML
    [Fact]
    public void LoadStack_MalformedToml_ShouldThrowError()
    {
        _repo.InitGraftDir();
        var stackPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "bad.toml");
        File.WriteAllText(stackPath, "this is not valid toml {{{}}}}");
        Assert.True(File.Exists(stackPath));

        Assert.ThrowsAny<Exception>(() => ConfigLoader.LoadStack("bad", _repo.Path));
    }

    // Edge case: Missing required fields in TOML
    [Fact]
    public void LoadStack_MissingTrunk_ShouldThrowError()
    {
        _repo.InitGraftDir();
        var stackPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "incomplete.toml");
        File.WriteAllText(stackPath, """
            name = "incomplete"
            """);
        Assert.True(File.Exists(stackPath));

        Assert.ThrowsAny<Exception>(() => ConfigLoader.LoadStack("incomplete", _repo.Path));
    }

    // Edge case: Stack file doesn't exist
    [Fact]
    public void LoadStack_FileNotFound_ShouldThrowError()
    {
        _repo.InitGraftDir();
        var stackPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "nonexistent.toml");
        Assert.False(File.Exists(stackPath));

        Assert.ThrowsAny<Exception>(() => ConfigLoader.LoadStack("nonexistent", _repo.Path));
    }

    // Requirement: Update state at ~/.config/graft/update-state.toml
    [Fact]
    public void LoadUpdateState_ValidToml_ShouldReturnState()
    {
        var configDir = Path.Combine(_repo.Path, "fake-config", "graft");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "update-state.toml"), """
            last_checked = "2026-02-05T14:30:00Z"
            current_version = "0.3.1"
            """);

        var state = ConfigLoader.LoadUpdateState(configDir);
        Assert.Equal("0.3.1", state.CurrentVersion);
        Assert.True(state.LastChecked > DateTime.MinValue);
    }

    // Requirement: Save then load should produce equivalent stack (round-trip)
    [Fact]
    public void SaveThenLoad_RoundTrip_ProducesEquivalentStack()
    {
        _repo.InitGraftDir();

        var original = new StackDefinition
        {
            Name = "roundtrip-test",
            Trunk = "develop",
            CreatedAt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc),
        };
        original.Branches.Add(new StackBranch { Name = "feature/alpha" });
        original.Branches.Add(new StackBranch
        {
            Name = "feature/beta",
            Pr = new PullRequestRef
            {
                Number = 42,
                Url = "https://github.com/org/repo/pull/42",
            },
        });

        ConfigLoader.SaveStack(original, _repo.Path);
        var loaded = ConfigLoader.LoadStack("roundtrip-test", _repo.Path);

        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.Trunk, loaded.Trunk);
        Assert.Equal(original.Branches.Count, loaded.Branches.Count);
        Assert.Equal("feature/alpha", loaded.Branches[0].Name);
        Assert.Null(loaded.Branches[0].Pr);
        Assert.Equal("feature/beta", loaded.Branches[1].Name);
        Assert.NotNull(loaded.Branches[1].Pr);
        Assert.Equal(42UL, loaded.Branches[1].Pr!.Number);
        Assert.Equal("https://github.com/org/repo/pull/42", loaded.Branches[1].Pr!.Url);
    }

    // Requirement: List all stacks in the stacks/ directory
    [Fact]
    public void ListStacks_MultipleStacks_ShouldReturnAll()
    {
        _repo.InitGraftDir();
        var stacksDir = Path.Combine(_repo.Path, ".git", "graft", "stacks");
        File.WriteAllText(Path.Combine(stacksDir, "stack-a.toml"), "name = \"stack-a\"\ntrunk = \"main\"");
        File.WriteAllText(Path.Combine(stacksDir, "stack-b.toml"), "name = \"stack-b\"\ntrunk = \"main\"");

        var files = Directory.GetFiles(stacksDir, "*.toml");
        Assert.Equal(2, files.Length);

        var stacks = ConfigLoader.ListStacks(_repo.Path);
        Assert.Contains("stack-a", stacks);
        Assert.Contains("stack-b", stacks);
    }

    // ========================
    // PR state values
    // ========================

    [Fact]
    public void LoadStack_WithPrStateMerged_ParsesCorrectly()
    {
        _repo.InitGraftDir();
        var stackPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "pr-state.toml");
        File.WriteAllText(stackPath, """
            name = "pr-state"
            trunk = "main"

            [[branches]]
            name = "feature/merged"
            pr_number = 10
            pr_url = "https://github.com/org/repo/pull/10"
            pr_state = "merged"
            """);

        var stack = ConfigLoader.LoadStack("pr-state", _repo.Path);
        Assert.Equal(PrState.Merged, stack.Branches[0].Pr!.State);
    }

    [Fact]
    public void LoadStack_WithPrStateClosed_ParsesCorrectly()
    {
        _repo.InitGraftDir();
        var stackPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "pr-closed.toml");
        File.WriteAllText(stackPath, """
            name = "pr-closed"
            trunk = "main"

            [[branches]]
            name = "feature/closed"
            pr_number = 11
            pr_url = "https://github.com/org/repo/pull/11"
            pr_state = "closed"
            """);

        var stack = ConfigLoader.LoadStack("pr-closed", _repo.Path);
        Assert.Equal(PrState.Closed, stack.Branches[0].Pr!.State);
    }

    [Fact]
    public void LoadStack_WithPrStateDefault_ParsesAsOpen()
    {
        _repo.InitGraftDir();
        var stackPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "pr-default.toml");
        File.WriteAllText(stackPath, """
            name = "pr-default"
            trunk = "main"

            [[branches]]
            name = "feature/open"
            pr_number = 12
            pr_url = "https://github.com/org/repo/pull/12"
            pr_state = "open"
            """);

        var stack = ConfigLoader.LoadStack("pr-default", _repo.Path);
        Assert.Equal(PrState.Open, stack.Branches[0].Pr!.State);
    }

    [Fact]
    public void LoadStack_WithPrStateUnknown_DefaultsToOpen()
    {
        _repo.InitGraftDir();
        var stackPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "pr-unknown.toml");
        File.WriteAllText(stackPath, """
            name = "pr-unknown"
            trunk = "main"

            [[branches]]
            name = "feature/unknown"
            pr_number = 13
            pr_url = "https://github.com/org/repo/pull/13"
            pr_state = "something-else"
            """);

        var stack = ConfigLoader.LoadStack("pr-unknown", _repo.Path);
        Assert.Equal(PrState.Open, stack.Branches[0].Pr!.State);
    }

    // ========================
    // Invalid PR number
    // ========================

    [Fact]
    public void LoadStack_InvalidPrNumber_Throws()
    {
        _repo.InitGraftDir();
        var stackPath = Path.Combine(_repo.Path, ".git", "graft", "stacks", "bad-pr.toml");
        File.WriteAllText(stackPath, """
            name = "bad-pr"
            trunk = "main"

            [[branches]]
            name = "feature/bad"
            pr_number = "not-a-number"
            pr_url = "https://github.com/org/repo/pull/1"
            """);

        var ex = Assert.ThrowsAny<Exception>(() => ConfigLoader.LoadStack("bad-pr", _repo.Path));
        Assert.Contains("invalid pr_number", ex.Message);
    }

    // ========================
    // Update state with pending_update
    // ========================

    [Fact]
    public void LoadUpdateState_WithPendingUpdate_ParsesAllFields()
    {
        File.WriteAllText(Path.Combine(_configDir, "update-state.toml"), """
            last_checked = "2026-02-05T14:30:00Z"
            current_version = "0.3.1"

            [pending_update]
            version = "0.3.2"
            binary_path = "/tmp/staging/graft-0.3.2"
            checksum = "sha256:abc123"
            downloaded_at = "2026-02-05T14:30:05Z"
            """);

        var state = ConfigLoader.LoadUpdateState(_configDir);
        Assert.Equal("0.3.1", state.CurrentVersion);
        Assert.NotNull(state.PendingUpdate);
        Assert.Equal("0.3.2", state.PendingUpdate!.Version);
        Assert.Equal("/tmp/staging/graft-0.3.2", state.PendingUpdate.BinaryPath);
        Assert.Equal("sha256:abc123", state.PendingUpdate.Checksum);
        Assert.True(state.PendingUpdate.DownloadedAt > DateTime.MinValue);
    }

    [Fact]
    public void LoadUpdateState_MissingFile_ReturnsDefaultState()
    {
        var emptyDir = Path.Combine(_configDir, "empty-sub");
        Directory.CreateDirectory(emptyDir);

        var state = ConfigLoader.LoadUpdateState(emptyDir);
        Assert.Null(state.PendingUpdate);
        Assert.Equal(default, state.LastChecked);
    }

    // ========================
    // ListStacks edge cases
    // ========================

    [Fact]
    public void ListStacks_NoStacksDir_ReturnsEmpty()
    {
        // InitGraftDir creates the stacks dir, so just use a bare .git/graft without stacks
        var graftDir = Path.Combine(_repo.Path, ".git", "graft");
        Directory.CreateDirectory(graftDir);
        // Don't create stacks dir

        var stacks = ConfigLoader.ListStacks(_repo.Path);
        Assert.Empty(stacks);
    }

    // ========================
    // Repo cache round-trip
    // ========================

    [Fact]
    public void SaveAndLoadRepoCache_RoundTrip()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo
        {
            Name = "repo-a",
            Path = "/tmp/repo-a",
            Branch = "main",
            AutoFetch = true,
            LastFetched = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
        });
        cache.Repos.Add(new CachedRepo
        {
            Name = "repo-b",
            Path = "/tmp/repo-b",
        });

        ConfigLoader.SaveRepoCache(cache, _configDir);
        var loaded = ConfigLoader.LoadRepoCache(_configDir);

        Assert.Equal(2, loaded.Repos.Count);
        Assert.Equal("repo-a", loaded.Repos[0].Name);
        Assert.Equal("main", loaded.Repos[0].Branch);
        Assert.True(loaded.Repos[0].AutoFetch);
        Assert.NotNull(loaded.Repos[0].LastFetched);
        Assert.Equal("repo-b", loaded.Repos[1].Name);
        Assert.Null(loaded.Repos[1].Branch);
        Assert.False(loaded.Repos[1].AutoFetch);
    }

    [Fact]
    public void LoadRepoCache_MissingFile_ReturnsEmpty()
    {
        var emptyDir = Path.Combine(_configDir, "no-cache");
        Directory.CreateDirectory(emptyDir);

        var cache = ConfigLoader.LoadRepoCache(emptyDir);
        Assert.Empty(cache.Repos);
    }

    // ========================
    // AddRepoToCache / RemoveRepoFromCache
    // ========================

    [Fact]
    public void AddRepoToCache_AddsNewRepo()
    {
        ConfigLoader.SaveRepoCache(new RepoCache(), _configDir);

        ConfigLoader.AddRepoToCache(
            new CachedRepo { Name = "new-repo", Path = "/tmp/new-repo" }, _configDir);

        var cache = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Single(cache.Repos);
        Assert.Equal("new-repo", cache.Repos[0].Name);
    }

    [Fact]
    public void AddRepoToCache_DuplicatePath_DoesNotAddDuplicate()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo { Name = "existing", Path = "/tmp/existing" });
        ConfigLoader.SaveRepoCache(cache, _configDir);

        ConfigLoader.AddRepoToCache(
            new CachedRepo { Name = "existing-dup", Path = "/tmp/existing" }, _configDir);

        var loaded = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Single(loaded.Repos);
    }

    [Fact]
    public void RemoveRepoFromCache_RemovesExisting()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo { Name = "to-remove", Path = "/tmp/to-remove" });
        cache.Repos.Add(new CachedRepo { Name = "to-keep", Path = "/tmp/to-keep" });
        ConfigLoader.SaveRepoCache(cache, _configDir);

        ConfigLoader.RemoveRepoFromCache("/tmp/to-remove", _configDir);

        var loaded = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Single(loaded.Repos);
        Assert.Equal("to-keep", loaded.Repos[0].Name);
    }

    [Fact]
    public void RemoveRepoFromCache_NonExistentPath_DoesNothing()
    {
        var cache = new RepoCache();
        cache.Repos.Add(new CachedRepo { Name = "stays", Path = "/tmp/stays" });
        ConfigLoader.SaveRepoCache(cache, _configDir);

        ConfigLoader.RemoveRepoFromCache("/tmp/nonexistent", _configDir);

        var loaded = ConfigLoader.LoadRepoCache(_configDir);
        Assert.Single(loaded.Repos);
    }

    // ========================
    // Scan paths round-trip
    // ========================

    [Fact]
    public void SaveAndLoadScanPaths_RoundTrip()
    {
        var paths = new List<ScanPath>
        {
            new() { Path = "/Users/dev/projects" },
            new() { Path = "/Users/dev/work" },
        };

        ConfigLoader.SaveScanPaths(paths, _configDir);
        var loaded = ConfigLoader.LoadScanPaths(_configDir);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("/Users/dev/projects", loaded[0].Path);
        Assert.Equal("/Users/dev/work", loaded[1].Path);
    }

    [Fact]
    public void LoadScanPaths_MissingFile_ReturnsEmpty()
    {
        var emptyDir = Path.Combine(_configDir, "no-scan");
        Directory.CreateDirectory(emptyDir);

        var paths = ConfigLoader.LoadScanPaths(emptyDir);
        Assert.Empty(paths);
    }

    [Fact]
    public void SaveScanPaths_EmptyList_RemovesKey()
    {
        // First save some paths
        ConfigLoader.SaveScanPaths(
            [new ScanPath { Path = "/tmp/test" }], _configDir);
        Assert.Single(ConfigLoader.LoadScanPaths(_configDir));

        // Now save empty list
        ConfigLoader.SaveScanPaths([], _configDir);
        var loaded = ConfigLoader.LoadScanPaths(_configDir);
        Assert.Empty(loaded);
    }

    // ========================
    // Active stack persistence
    // ========================

    [Fact]
    public void SaveActiveStack_Null_DeletesFile()
    {
        _repo.InitGraftDir();
        var activeStackPath = Path.Combine(_repo.Path, ".git", "graft", "active-stack");
        File.WriteAllText(activeStackPath, "some-stack");

        ConfigLoader.SaveActiveStack(null, _repo.Path);

        Assert.False(File.Exists(activeStackPath));
    }

    [Fact]
    public void LoadActiveStack_EmptyFile_ReturnsNull()
    {
        _repo.InitGraftDir();
        var activeStackPath = Path.Combine(_repo.Path, ".git", "graft", "active-stack");
        File.WriteAllText(activeStackPath, "   ");

        var name = ConfigLoader.LoadActiveStack(_repo.Path);
        Assert.Null(name);
    }

    // ========================
    // PR state round-trip via SaveStack
    // ========================

    [Fact]
    public void SaveStack_WithPrState_PersistsState()
    {
        _repo.InitGraftDir();
        var stack = new StackDefinition { Name = "pr-roundtrip", Trunk = "main" };
        stack.Branches.Add(new StackBranch
        {
            Name = "feature/x",
            Pr = new PullRequestRef
            {
                Number = 99,
                Url = "https://github.com/org/repo/pull/99",
                State = PrState.Merged,
            },
        });

        ConfigLoader.SaveStack(stack, _repo.Path);
        var loaded = ConfigLoader.LoadStack("pr-roundtrip", _repo.Path);

        Assert.Equal(PrState.Merged, loaded.Branches[0].Pr!.State);
    }
}
