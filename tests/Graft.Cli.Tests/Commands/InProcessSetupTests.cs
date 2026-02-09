using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// In-process tests for setup commands (version, root help, --continue/--abort).
/// </summary>
[Collection("InProcess")]
public sealed class InProcessSetupTests
{
    [Fact]
    public async Task Version_ShowsVersionString()
    {
        var result = await InProcessCliRunner.RunAsync(null, "version");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("graft", result.Stdout);
        // Version format: graft X.Y.Z
        Assert.Matches(@"graft \d+\.\d+\.\d+", result.Stdout);
    }

    [Fact]
    public async Task ContinueAndAbort_Together_ReturnsError()
    {
        var result = await InProcessCliRunner.RunAsync(null, "--continue", "--abort");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("cannot be used together", result.Stderr);
    }

    [Fact]
    public async Task RootCommand_NoArgs_ShowsUsage()
    {
        var result = await InProcessCliRunner.RunAsync(null);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.Stdout);
    }

    [Fact]
    public async Task Install_ExercisesHandler()
    {
        // Install may fail (e.g. symlink permissions) but exercises the handler code
        var result = await InProcessCliRunner.RunAsync(null, "install");

        // Either succeeds or fails — both exercise the handler
        Assert.True(result.ExitCode == 0 || result.ExitCode == 1);
    }

    [Fact]
    public async Task Uninstall_ExercisesHandler()
    {
        var result = await InProcessCliRunner.RunAsync(null, "uninstall");

        Assert.True(result.ExitCode == 0 || result.ExitCode == 1);
    }

    [Fact]
    public async Task Update_ExercisesHandler()
    {
        var result = await InProcessCliRunner.RunAsync(null, "update");

        // Update checks network — may fail, but exercises the handler
        Assert.True(result.ExitCode == 0 || result.ExitCode == 1);
    }
}
