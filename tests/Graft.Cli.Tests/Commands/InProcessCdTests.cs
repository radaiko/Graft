using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// In-process tests for the cd command.
/// cd reads from ~/.config/graft repo cache.
/// With stdin redirected (as in tests), interactive mode is disabled.
/// </summary>
[Collection("InProcess")]
public sealed class InProcessCdTests
{
    [Fact]
    public async Task Cd_WithName_ExercisesHandler()
    {
        // Uses real repo cache — the handler code runs regardless of match result
        var result = await InProcessCliRunner.RunAsync(null, "cd", "nonexistent-repo-xyz-12345");

        // Either no match (exit 1 + error) or a match is found (exit 0 + path)
        // Both paths exercise the handler code for coverage
        if (result.ExitCode == 0)
            Assert.False(string.IsNullOrWhiteSpace(result.Stdout));
        else
            Assert.Contains("Error", result.Stdout + result.Stderr);
    }

    [Fact]
    public async Task Cd_NoArgs_InputRedirected_ShowsError()
    {
        // When stdin is redirected (as in test), cd without args should error
        var result = await InProcessCliRunner.RunWithStdinAsync(null, "\n", "cd");

        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        // Should mention terminal requirement or no repos
        Assert.True(
            combined.Contains("Error") || combined.Contains("No repos"),
            $"Expected error or no repos message, got: {combined}");
    }
}
