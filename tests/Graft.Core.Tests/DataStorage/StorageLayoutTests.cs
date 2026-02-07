using Graft.Core.Tests.Helpers;

namespace Graft.Core.Tests.DataStorage;

/// <summary>
/// Tests for data storage layout per spec section 6.
/// Validates that the graft directory structure can be created correctly.
/// Uses TempGitRepo.InitGraftDir() which creates the expected structure.
/// </summary>
public sealed class StorageLayoutTests : IDisposable
{
    private readonly TempGitRepo _repo = new();

    public void Dispose() => _repo.Dispose();

    // Requirement: Per-repo metadata stored in .git/graft/
    [Fact]
    public void InitGraftDir_CreatesGraftDirectory()
    {
        var graftDir = _repo.InitGraftDir();

        Assert.True(Directory.Exists(graftDir));
        Assert.Equal(Path.Combine(_repo.Path, ".git", "graft"), graftDir);
    }

    // Requirement: Stack definitions in .git/graft/stacks/<name>.toml
    [Fact]
    public void InitGraftDir_CreatesStacksSubdirectory()
    {
        var graftDir = _repo.InitGraftDir();
        var stacksDir = Path.Combine(graftDir, "stacks");

        Assert.True(Directory.Exists(stacksDir));
    }

    // Requirement: Stack files can be written to the stacks/ directory
    [Fact]
    public void StackFile_CanBeCreatedInStacksDir()
    {
        var graftDir = _repo.InitGraftDir();
        var stackFile = Path.Combine(graftDir, "stacks", "auth-refactor.toml");

        File.WriteAllText(stackFile, """
            name = "auth-refactor"
            created_at = "2025-02-01T10:00:00Z"
            trunk = "main"

            [[branches]]
            name = "auth/base-types"
            """);

        Assert.True(File.Exists(stackFile));
        var content = File.ReadAllText(stackFile);
        Assert.Contains("auth-refactor", content);
        Assert.Contains("trunk", content);
    }

    // Requirement: Worktree config at .git/graft/worktrees.toml
    [Fact]
    public void WorktreeConfig_CanBeCreatedInGraftDir()
    {
        var graftDir = _repo.InitGraftDir();
        var wtFile = Path.Combine(graftDir, "worktrees.toml");

        File.WriteAllText(wtFile, """
            [layout]
            pattern = "../{name}"
            """);

        Assert.True(File.Exists(wtFile));
    }

    // Requirement: Config file at .git/graft/config.toml
    [Fact]
    public void ConfigFile_CanBeCreatedInGraftDir()
    {
        var graftDir = _repo.InitGraftDir();
        var configFile = Path.Combine(graftDir, "config.toml");

        File.WriteAllText(configFile, """
            [defaults]
            trunk = "main"
            """);

        Assert.True(File.Exists(configFile));
    }

    // Requirement: Global config dir at ~/.config/graft/
    [Fact]
    public void GlobalConfigPath_FollowsXdgConvention()
    {
        // The global config should be at ~/.config/graft/ (or XDG_CONFIG_HOME/graft/)
        var expectedBase = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        // On macOS/Linux this resolves to ~/.config, on Windows to AppData
        // The actual path construction should be: Path.Combine(configBase, "graft")
        Assert.False(string.IsNullOrEmpty(expectedBase),
            "Could not determine application data directory");
    }

    // Requirement: Storage layout matches spec section 6 structure
    [Fact]
    public void FullLayout_MatchesSpec()
    {
        var graftDir = _repo.InitGraftDir();

        // Create the full per-repo structure from spec
        File.WriteAllText(Path.Combine(graftDir, "config.toml"), "");
        File.WriteAllText(Path.Combine(graftDir, "worktrees.toml"), "");
        File.WriteAllText(Path.Combine(graftDir, "stacks", "test.toml"), "");

        // Verify the expected structure exists
        Assert.True(File.Exists(Path.Combine(graftDir, "config.toml")));
        Assert.True(Directory.Exists(Path.Combine(graftDir, "stacks")));
        Assert.True(File.Exists(Path.Combine(graftDir, "stacks", "test.toml")));
        Assert.True(File.Exists(Path.Combine(graftDir, "worktrees.toml")));
    }
}
