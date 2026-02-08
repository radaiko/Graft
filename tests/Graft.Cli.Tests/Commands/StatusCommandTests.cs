using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

public sealed class StatusCommandTests
{
    [Fact]
    public void Status_IsRegisteredCommand()
    {
        var root = CliTestHelper.BuildRootCommand();

        var statusCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "status");

        Assert.NotNull(statusCommand);
    }

    [Fact]
    public void St_AliasIsRegistered()
    {
        var root = CliTestHelper.BuildRootCommand();

        var stCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "st");

        Assert.NotNull(stCommand);
        Assert.True(stCommand.Hidden);
    }

    [Fact]
    public void Status_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("status");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Status_WithRepoName_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("status MyRepo");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void St_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("st");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void St_WithRepoName_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("st MyRepo");
        Assert.Empty(result.Errors);
    }
}
