using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// Tests for `graft wt` command.
/// Structure: `graft wt <branch> [-c]`, `graft wt del <branch> [-f]`,
/// `graft wt list`, `graft wt goto <branch>`
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

    // Requirement: `graft wt del <branch>` parses correctly
    [Fact]
    public void WtDel_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt del feature-branch");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void WtDel_WithForce_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt del feature-branch -f");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft wt list` parses correctly
    [Fact]
    public void WtList_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt list");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft wt goto <branch>` parses correctly
    [Fact]
    public void WtGoto_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("wt goto feature-branch");
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

        Assert.Contains("list", subcommands);
        Assert.Contains("del", subcommands);
        Assert.Contains("goto", subcommands);
    }
}
