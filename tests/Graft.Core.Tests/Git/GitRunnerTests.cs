using Graft.Core.Git;
using Graft.Core.Tests.Helpers;

namespace Graft.Core.Tests.Git;

public sealed class GitRunnerTests : IDisposable
{
    private readonly TempGitRepo _repo = new();

    public void Dispose() => _repo.Dispose();

    // Requirement: GitRunner executes git commands via Process
    [Fact]
    public async Task RunAsync_SimpleCommand_ReturnsSuccess()
    {
        var runner = new GitRunner(_repo.Path);

        var result = await runner.RunAsync("status");

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
    }

    // Requirement: GitRunner returns GitResult with Stdout
    [Fact]
    public async Task RunAsync_RevParse_ReturnsStdout()
    {
        var runner = new GitRunner(_repo.Path);

        var result = await runner.RunAsync("rev-parse", "--abbrev-ref", "HEAD");

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Stdout));
    }

    // Requirement: GitRunner returns GitResult with Stderr on failure
    [Fact]
    public async Task RunAsync_InvalidCommand_ReturnsFailure()
    {
        var runner = new GitRunner(_repo.Path);

        var result = await runner.RunAsync("not-a-real-command");

        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Stderr));
    }

    // Requirement: GitResult.ThrowOnFailure() throws GitException on failure
    [Fact]
    public async Task ThrowOnFailure_FailedCommand_ThrowsGitException()
    {
        var runner = new GitRunner(_repo.Path);

        var result = await runner.RunAsync("not-a-real-command");

        Assert.Throws<GitException>(() => result.ThrowOnFailure());
    }

    // Requirement: ThrowOnFailure returns result on success
    [Fact]
    public async Task ThrowOnFailure_SuccessfulCommand_ReturnsResult()
    {
        var runner = new GitRunner(_repo.Path);

        var result = await runner.RunAsync("status");
        var returned = result.ThrowOnFailure();

        Assert.Equal(result, returned);
    }

    // Requirement: GitRunner works with specified working directory
    [Fact]
    public async Task RunAsync_WorkingDirectory_ExecutesInCorrectDir()
    {
        var runner = new GitRunner(_repo.Path);

        var result = await runner.RunAsync("rev-parse", "--show-toplevel");

        Assert.True(result.Success);
        // Normalize paths for comparison (macOS: /var → /private/var)
        var expected = Path.GetFullPath(_repo.Path);
        var actual = Path.GetFullPath(result.Stdout);
        Assert.True(
            expected == actual ||
            actual == "/private" + expected ||
            expected == "/private" + actual,
            $"Paths should match: expected={expected}, actual={actual}");
    }

    // Edge case: Empty arguments
    [Fact]
    public async Task RunAsync_NoArgs_ReturnsResult()
    {
        var runner = new GitRunner(_repo.Path);

        // "git" with no args should output help to stderr or stdout
        var result = await runner.RunAsync();

        // git with no args exits with 1 and shows help
        Assert.True(result.ExitCode is 0 or 1);
    }

    // Edge case: Multiple sequential commands
    [Fact]
    public async Task RunAsync_MultipleCommands_AllSucceed()
    {
        var runner = new GitRunner(_repo.Path);

        var r1 = await runner.RunAsync("status");
        var r2 = await runner.RunAsync("branch", "--list");
        var r3 = await runner.RunAsync("log", "--oneline", "-1");

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        Assert.True(r3.Success);
    }

    // Edge case: Non-existent working directory
    [Fact]
    public async Task RunAsync_NonExistentDirectory_Throws()
    {
        var runner = new GitRunner("/nonexistent/path/that/does/not/exist");

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await runner.RunAsync("status"));
    }
}
