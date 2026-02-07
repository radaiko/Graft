using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// Tests for `graft nuke` command and subcommands.
/// </summary>
public sealed class NukeCommandTests
{
    // Requirement: `graft nuke` command exists
    [Fact]
    public void Nuke_IsRegisteredCommand()
    {
        var root = CliTestHelper.BuildRootCommand();

        var nukeCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "nuke");

        Assert.NotNull(nukeCommand);
    }

    // Requirement: `graft nuke` parses correctly (with force)
    [Fact]
    public void Nuke_WithForce_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("nuke --force");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Nuke_WithForceAlias_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("nuke -f");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft nuke wt` parses correctly
    [Fact]
    public void NukeWt_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("nuke wt");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void NukeWt_WithForce_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("nuke wt -f");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft nuke stack` parses correctly
    [Fact]
    public void NukeStack_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("nuke stack");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft nuke branches` parses correctly
    [Fact]
    public void NukeBranches_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("nuke branches");
        Assert.Empty(result.Errors);
    }

    // Requirement: nuke has expected subcommands
    [Fact]
    public void Nuke_HasExpectedSubcommands()
    {
        var root = CliTestHelper.BuildRootCommand();
        var nukeCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .First(c => c.Name == "nuke");

        var subcommands = nukeCommand.Children
            .OfType<System.CommandLine.Command>()
            .Select(c => c.Name)
            .ToList();

        Assert.Contains("wt", subcommands);
        Assert.Contains("stack", subcommands);
        Assert.Contains("branches", subcommands);
    }
}
