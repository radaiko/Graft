using Graft.Core.Config;
using Graft.Core.Stack;
using Graft.Core.Tests.Helpers;

namespace Graft.Core.Tests.Config;

/// <summary>
/// Tests for loading and persisting TOML configuration files.
/// </summary>
public sealed class ConfigLoaderTests : IDisposable
{
    private readonly TempGitRepo _repo = new();

    public void Dispose() => _repo.Dispose();

    // Requirement: Per-repo config at .git/graft/config.toml
    [Fact]
    public void LoadRepoConfig_NoFile_ShouldReturnDefaults()
    {
        _repo.InitGraftDir();
        var configPath = Path.Combine(_repo.Path, ".git", "graft", "config.toml");
        Assert.False(File.Exists(configPath));

        var config = ConfigLoader.LoadRepoConfig(_repo.Path);
        Assert.Equal("main", config.Defaults.Trunk);
        Assert.Equal("chain", config.Defaults.StackPrStrategy);
    }

    // Requirement: Per-repo config can be loaded from file
    [Fact]
    public void LoadRepoConfig_ValidToml_ShouldReturnParsedConfig()
    {
        _repo.InitGraftDir();
        var configPath = Path.Combine(_repo.Path, ".git", "graft", "config.toml");
        File.WriteAllText(configPath, """
            [defaults]
            trunk = "develop"
            stack_pr_strategy = "individual"
            """);
        Assert.True(File.Exists(configPath));

        var config = ConfigLoader.LoadRepoConfig(_repo.Path);
        Assert.Equal("develop", config.Defaults.Trunk);
        Assert.Equal("individual", config.Defaults.StackPrStrategy);
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

    // Requirement: Worktree config at .git/graft/worktrees.toml
    [Fact]
    public void LoadWorktreeConfig_ValidToml_ShouldReturnConfig()
    {
        _repo.InitGraftDir();
        var wtPath = Path.Combine(_repo.Path, ".git", "graft", "worktrees.toml");
        File.WriteAllText(wtPath, """
            [layout]
            pattern = "../{name}"

            [templates]
            [[templates.files]]
            src = ".env.template"
            dst = ".env"
            mode = "copy"
            """);
        Assert.True(File.Exists(wtPath));

        var config = ConfigLoader.LoadWorktreeConfig(_repo.Path);
        Assert.Equal("../{name}", config.Layout.Pattern);
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
}
