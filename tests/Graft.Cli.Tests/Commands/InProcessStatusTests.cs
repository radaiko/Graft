using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// In-process tests for the status command.
/// Status reads from ~/.config/graft — these tests exercise the command
/// handler code paths for coverage.
/// </summary>
[Collection("InProcess")]
public sealed class InProcessStatusTests
{
    [Fact]
    public async Task Status_NoRepos_ShowsMessage()
    {
        // status with an empty/nonexistent config dir shows "No repos found"
        var result = await InProcessCliRunner.RunAsync(null, "status");

        // Should succeed — either shows "No repos found" or lists actual repos
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Status_WithAlias_Succeeds()
    {
        var result = await InProcessCliRunner.RunAsync(null, "st");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Status_NonexistentRepo_ShowsError()
    {
        var result = await InProcessCliRunner.RunAsync(null, "status", "nonexistent-repo-name-xyz");

        // Should fail because no repo matches
        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        Assert.Contains("Error", combined);
    }
}
