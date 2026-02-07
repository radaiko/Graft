using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// Tests for setup commands: install, uninstall, update, version.
/// Per spec: these are all top-level commands.
/// </summary>
public sealed class SetupCommandTests
{
    // ========================
    // graft install
    // ========================

    // Requirement: `graft install` exists as top-level command
    [Fact]
    public void Install_IsTopLevelCommand()
    {
        var root = CliTestHelper.BuildRootCommand();

        var installCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "install");

        Assert.NotNull(installCommand);
    }

    // Requirement: `graft install` parses without error
    [Fact]
    public void Install_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("install");

        Assert.Empty(result.Errors);
    }

    // ========================
    // graft uninstall
    // ========================

    // Requirement: `graft uninstall` exists as top-level command
    [Fact]
    public void Uninstall_IsTopLevelCommand()
    {
        var root = CliTestHelper.BuildRootCommand();

        var uninstallCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "uninstall");

        Assert.NotNull(uninstallCommand);
    }

    // Requirement: `graft uninstall` parses without error
    [Fact]
    public void Uninstall_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("uninstall");

        Assert.Empty(result.Errors);
    }

    // ========================
    // graft version
    // ========================

    // Requirement: `graft version` exists as top-level command
    [Fact]
    public void Version_IsTopLevelCommand()
    {
        var root = CliTestHelper.BuildRootCommand();

        var versionCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "version");

        Assert.NotNull(versionCommand);
    }

    // Requirement: `graft version` prints a version string
    [Fact]
    public async Task Version_PrintsVersionString()
    {
        var cliResult = await CliTestHelper.RunAsync(null, "version");

        Assert.Equal(0, cliResult.ExitCode);
        // Per spec: should print a version like "0.1.0"
        Assert.Matches(@"\d+\.\d+\.\d+", cliResult.Stdout);
    }

    // ========================
    // graft update
    // ========================

    // Requirement: `graft update` exists as top-level command
    [Fact]
    public void Update_IsTopLevelCommand()
    {
        var root = CliTestHelper.BuildRootCommand();

        var updateCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "update");

        Assert.NotNull(updateCommand);
    }

    // Requirement: `graft update` parses without error
    [Fact]
    public void Update_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("update");

        Assert.Empty(result.Errors);
    }
}
