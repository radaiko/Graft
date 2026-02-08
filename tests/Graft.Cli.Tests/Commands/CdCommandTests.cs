using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

public sealed class CdCommandTests
{
    [Fact]
    public void Cd_IsRegisteredCommand()
    {
        var root = CliTestHelper.BuildRootCommand();

        var cdCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "cd");

        Assert.NotNull(cdCommand);
    }

    [Fact]
    public void CdWithName_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("cd my-repo");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void CdWithoutName_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("cd");
        Assert.Empty(result.Errors);
    }
}
