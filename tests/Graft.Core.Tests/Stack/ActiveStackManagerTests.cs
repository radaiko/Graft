using Graft.Core.Config;
using Graft.Core.Stack;
using Graft.Core.Tests.Helpers;

namespace Graft.Core.Tests.Stack;

public sealed class ActiveStackManagerTests : IDisposable
{
    private readonly TempGitRepo _repo = new();

    public void Dispose() => _repo.Dispose();

    [Fact]
    public void GetActiveStackName_WithActiveStack_ReturnsName()
    {
        _repo.InitGraftDir();
        var stacksDir = Path.Combine(_repo.Path, ".git", "graft", "stacks");
        File.WriteAllText(Path.Combine(stacksDir, "my-stack.toml"), "name = \"my-stack\"\ntrunk = \"master\"");
        File.WriteAllText(Path.Combine(_repo.Path, ".git", "graft", "active-stack"), "my-stack");

        var name = ActiveStackManager.GetActiveStackName(_repo.Path);

        Assert.Equal("my-stack", name);
    }

    [Fact]
    public void GetActiveStackName_NoActiveStack_OneStack_AutoMigrates()
    {
        _repo.InitGraftDir();
        var stacksDir = Path.Combine(_repo.Path, ".git", "graft", "stacks");
        File.WriteAllText(Path.Combine(stacksDir, "only-stack.toml"), "name = \"only-stack\"\ntrunk = \"master\"");

        var name = ActiveStackManager.GetActiveStackName(_repo.Path);

        Assert.Equal("only-stack", name);
        // Verify it was persisted
        var persisted = ConfigLoader.LoadActiveStack(_repo.Path);
        Assert.Equal("only-stack", persisted);
    }

    [Fact]
    public void GetActiveStackName_NoActiveStack_NoStacks_Throws()
    {
        _repo.InitGraftDir();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ActiveStackManager.GetActiveStackName(_repo.Path));
        Assert.Contains("No stacks found", ex.Message);
    }

    [Fact]
    public void GetActiveStackName_NoActiveStack_MultipleStacks_Throws()
    {
        _repo.InitGraftDir();
        var stacksDir = Path.Combine(_repo.Path, ".git", "graft", "stacks");
        File.WriteAllText(Path.Combine(stacksDir, "stack-a.toml"), "name = \"stack-a\"\ntrunk = \"master\"");
        File.WriteAllText(Path.Combine(stacksDir, "stack-b.toml"), "name = \"stack-b\"\ntrunk = \"master\"");

        var ex = Assert.Throws<InvalidOperationException>(
            () => ActiveStackManager.GetActiveStackName(_repo.Path));
        Assert.Contains("No active stack", ex.Message);
        Assert.Contains("stack-a", ex.Message);
        Assert.Contains("stack-b", ex.Message);
    }

    [Fact]
    public void SetActiveStack_ValidStack_SetsIt()
    {
        _repo.InitGraftDir();
        var stacksDir = Path.Combine(_repo.Path, ".git", "graft", "stacks");
        File.WriteAllText(Path.Combine(stacksDir, "target.toml"), "name = \"target\"\ntrunk = \"master\"");

        ActiveStackManager.SetActiveStack("target", _repo.Path);

        var active = ConfigLoader.LoadActiveStack(_repo.Path);
        Assert.Equal("target", active);
    }

    [Fact]
    public void SetActiveStack_NonexistentStack_Throws()
    {
        _repo.InitGraftDir();

        Assert.Throws<FileNotFoundException>(
            () => ActiveStackManager.SetActiveStack("nonexistent", _repo.Path));
    }

    [Fact]
    public void ClearActiveStack_RemovesActiveStackFile()
    {
        _repo.InitGraftDir();
        var stacksDir = Path.Combine(_repo.Path, ".git", "graft", "stacks");
        File.WriteAllText(Path.Combine(stacksDir, "stack.toml"), "name = \"stack\"\ntrunk = \"master\"");
        ActiveStackManager.SetActiveStack("stack", _repo.Path);
        Assert.NotNull(ConfigLoader.LoadActiveStack(_repo.Path));

        ActiveStackManager.ClearActiveStack(_repo.Path);

        Assert.Null(ConfigLoader.LoadActiveStack(_repo.Path));
    }
}
