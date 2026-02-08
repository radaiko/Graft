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

    // GitResult.HasConflict with "CONFLICT" in stderr
    [Fact]
    public void HasConflict_WithConflictInStderr_ReturnsTrue()
    {
        var result = new GitResult(1, "", "CONFLICT (content): Merge conflict in file.cs");
        Assert.True(result.HasConflict);
    }

    // GitResult.HasConflict with clean stderr
    [Fact]
    public void HasConflict_WithCleanStderr_ReturnsFalse()
    {
        var result = new GitResult(1, "", "fatal: some other error");
        Assert.False(result.HasConflict);
    }

    // GitResult.HasConflict with success exit code
    [Fact]
    public void HasConflict_WithSuccessExitCode_ReturnsFalse()
    {
        var result = new GitResult(0, "success", "");
        Assert.False(result.HasConflict);
    }

    // ResolveGitCommonDir for a regular repo
    [Fact]
    public void ResolveGitCommonDir_RegularRepo_ReturnsDotGit()
    {
        var gitDir = GitRunner.ResolveGitCommonDir(_repo.Path);
        Assert.True(Directory.Exists(gitDir));
        Assert.Equal(Path.Combine(_repo.Path, ".git"), gitDir);
    }

    // ResolveGitDir for a regular repo
    [Fact]
    public void ResolveGitDir_RegularRepo_ReturnsDotGit()
    {
        var gitDir = GitRunner.ResolveGitDir(_repo.Path);
        Assert.Equal(Path.Combine(_repo.Path, ".git"), gitDir);
    }

    // ResolveGitCommonDir for a worktree returns common dir
    [Fact]
    public async Task ResolveGitCommonDir_Worktree_ReturnsMainDotGit()
    {
        var runner = new GitRunner(_repo.Path);
        await runner.RunAsync("checkout", "-b", "wt-test-branch");
        await runner.RunAsync("checkout", "master");

        var repoName = Path.GetFileName(Path.GetFullPath(_repo.Path));
        var parentDir = Path.GetDirectoryName(Path.GetFullPath(_repo.Path))!;
        var wtPath = Path.Combine(parentDir, $"{repoName}.wt.wt-test-branch");

        try
        {
            await runner.RunAsync("worktree", "add", wtPath, "wt-test-branch");

            var commonDir = GitRunner.ResolveGitCommonDir(wtPath);
            // Should resolve back to main repo's .git
            var expected = Path.GetFullPath(Path.Combine(_repo.Path, ".git"));
            var actual = Path.GetFullPath(commonDir);
            Assert.True(
                expected == actual ||
                actual == "/private" + expected ||
                expected == "/private" + actual,
                $"Common dir should point to main .git: expected={expected}, actual={actual}");
        }
        finally
        {
            try { await runner.RunAsync("worktree", "remove", "--force", wtPath); } catch { }
            if (Directory.Exists(wtPath)) Directory.Delete(wtPath, recursive: true);
        }
    }

    // ResolveGitDir for a worktree returns per-worktree dir
    [Fact]
    public async Task ResolveGitDir_Worktree_ReturnsWorktreeSpecificDir()
    {
        var runner = new GitRunner(_repo.Path);
        await runner.RunAsync("checkout", "-b", "wt-gitdir-branch");
        await runner.RunAsync("checkout", "master");

        var repoName = Path.GetFileName(Path.GetFullPath(_repo.Path));
        var parentDir = Path.GetDirectoryName(Path.GetFullPath(_repo.Path))!;
        var wtPath = Path.Combine(parentDir, $"{repoName}.wt.wt-gitdir-branch");

        try
        {
            await runner.RunAsync("worktree", "add", wtPath, "wt-gitdir-branch");

            var gitDir = GitRunner.ResolveGitDir(wtPath);
            // Per-worktree dir should be different from common dir
            var commonDir = GitRunner.ResolveGitCommonDir(wtPath);
            Assert.NotEqual(Path.GetFullPath(gitDir), Path.GetFullPath(commonDir));
        }
        finally
        {
            try { await runner.RunAsync("worktree", "remove", "--force", wtPath); } catch { }
            if (Directory.Exists(wtPath)) Directory.Delete(wtPath, recursive: true);
        }
    }

    // ResolveGitCommonDir for non-git directory throws
    [Fact]
    public void ResolveGitCommonDir_NonGitDir_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"not-a-repo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => GitRunner.ResolveGitCommonDir(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }
}
