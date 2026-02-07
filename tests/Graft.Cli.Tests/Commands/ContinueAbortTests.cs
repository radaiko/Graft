using System.CommandLine;
using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// Tests for `graft --continue` and `graft --abort` global options.
/// Per spec these are root-level options, not subcommands.
/// </summary>
public sealed class ContinueAbortTests
{
    // Requirement: `graft --continue` is a root-level option
    [Fact]
    public void Continue_IsGlobalOption()
    {
        var root = CliTestHelper.BuildRootCommand();

        var continueOption = root.Options
            .FirstOrDefault(o => o.Name == "--continue" || o.Name == "continue");

        Assert.NotNull(continueOption);
    }

    // Requirement: `graft --abort` is a root-level option
    [Fact]
    public void Abort_IsGlobalOption()
    {
        var root = CliTestHelper.BuildRootCommand();

        var abortOption = root.Options
            .FirstOrDefault(o => o.Name == "--abort" || o.Name == "abort");

        Assert.NotNull(abortOption);
    }

    // Requirement: `graft --continue` parses without error
    [Fact]
    public void Continue_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("--continue");

        Assert.Empty(result.Errors);
    }

    // Requirement: `graft --abort` parses without error
    [Fact]
    public void Abort_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("--abort");

        Assert.Empty(result.Errors);
    }

    // Requirement: `graft --continue --abort` are mutually exclusive
    [Fact]
    public async Task ContinueAndAbort_Together_ReturnsError()
    {
        var cliResult = await CliTestHelper.RunAsync(null, "--continue", "--abort");

        Assert.NotEqual(0, cliResult.ExitCode);
        Assert.Contains("cannot be used together", cliResult.Stderr);
    }
}
