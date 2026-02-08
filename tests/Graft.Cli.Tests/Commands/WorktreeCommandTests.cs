using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// Tests for `graft wt` command.
/// Structure: `graft wt <branch> [-c]`, `graft wt remove <branch> [-f]`,
/// `graft wt list`
/// </summary>
public sealed class WorktreeCommandTests
{
    // Requirement: `graft wt` command exists
    [Fact]
    public void Wt_IsRegisteredCommand()
    {
        var root = CliTestHelper.BuildRootCommand();

        var wtCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "wt");

        Assert.NotNull(wtCommand);
    }

    // Requirement: `graft wt <branch>` parses correctly (add existing branch)
    [Fact]
    public void Wt_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt feature-branch");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft wt <branch> -c` (create new branch + worktree)
    [Fact]
    public void Wt_WithCreate_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt feature-branch --create");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Wt_WithCreateAlias_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt feature-branch -c");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft wt remove <branch>` parses correctly
    [Fact]
    public void WtRemove_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt remove feature-branch");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void WtRemove_WithForce_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt remove feature-branch -f");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void WtRemove_WithForceLong_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt remove feature-branch --force");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft wt rm <branch>` (alias) parses correctly
    [Fact]
    public void WtRm_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt rm feature-branch");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft wt list` parses correctly
    [Fact]
    public void WtList_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt list");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft wt ls` (alias) parses correctly
    [Fact]
    public void WtLs_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt ls");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft wt goto <branch>` parses correctly (deprecated but still works)
    [Fact]
    public void WtGoto_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt goto feature-branch");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft wt del` (deprecated alias) still parses
    [Fact]
    public void WtDel_Deprecated_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt del feature-branch");
        Assert.Empty(result.Errors);
    }

    // Requirement: wt has expected subcommands
    [Fact]
    public void Wt_HasExpectedSubcommands()
    {
        var root = CliTestHelper.BuildRootCommand();
        var wtCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .First(c => c.Name == "wt");

        var subcommands = wtCommand.Children
            .OfType<System.CommandLine.Command>()
            .Select(c => c.Name)
            .ToList();

        Assert.Contains("remove", subcommands);
        Assert.Contains("rm", subcommands);
        Assert.Contains("list", subcommands);
        Assert.Contains("ls", subcommands);
        Assert.Contains("del", subcommands);
        Assert.Contains("goto", subcommands);
    }
}
