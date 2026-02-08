using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

public sealed class ScanCommandTests
{
    [Fact]
    public void Scan_IsRegisteredCommand()
    {
        var root = CliTestHelper.BuildRootCommand();

        var scanCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "scan");

        Assert.NotNull(scanCommand);
    }

    [Fact]
    public void ScanAdd_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("scan add /tmp/test");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ScanRemove_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("scan remove /tmp/test");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ScanRm_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("scan rm /tmp/test");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ScanList_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("scan list");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ScanLs_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("scan ls");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Scan_HasExpectedSubcommands()
    {
        var root = CliTestHelper.BuildRootCommand();
        var scanCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .First(c => c.Name == "scan");

        var subcommands = scanCommand.Children
            .OfType<System.CommandLine.Command>()
            .Select(c => c.Name)
            .ToList();

        Assert.Contains("add", subcommands);
        Assert.Contains("remove", subcommands);
        Assert.Contains("rm", subcommands);
        Assert.Contains("list", subcommands);
        Assert.Contains("ls", subcommands);
    }

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
