using System.Diagnostics;
using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// In-process tests for stack commit command.
/// Uses a repo with a stack and staged files.
/// </summary>
[Collection("InProcess")]
public sealed class InProcessCommitTests : IDisposable
{
    private readonly TempCliRepo _repo;

    public InProcessCommitTests()
    {
        _repo = TempCliRepo.CreateEmpty();
    }

    public void Dispose() => _repo.Dispose();

    private static void RunGit(string workDir, params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        process.Start();
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
    }

    [Fact]
    public async Task StackCommit_WithStagedChanges_Commits()
    {
        // Set up stack with branch
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "commit-stack");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "feature-a", "-c");

        // Stage a file change
        File.WriteAllText(Path.Combine(_repo.Path, "new-file.cs"), "// new file");
        RunGit(_repo.Path, "add", "new-file.cs");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "commit", "-m", "Add new file");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Committed to", result.Stdout);
    }

    [Fact]
    public async Task StackCommit_ToSpecificBranch_Commits()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "multi-stack");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "branch-a", "-c");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "branch-b", "-c");

        // Stage a file
        File.WriteAllText(Path.Combine(_repo.Path, "targeted.cs"), "// targeted");
        RunGit(_repo.Path, "add", "targeted.cs");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "commit", "-m", "Add targeted", "-b", "branch-a");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Committed to", result.Stdout);
    }

    [Fact]
    public async Task StackCommit_NoStagedChanges_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "empty-commit");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "empty-branch", "-c");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "commit", "-m", "nothing to commit");

        Assert.NotEqual(0, result.ExitCode);
        var combined = result.Stdout + result.Stderr;
        Assert.Contains("staged", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StackCommit_NoMessage_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "no-msg");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "commit");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task StackCommit_Amend_WorksWithStagedChanges()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "amend-stack");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "amend-branch", "-c");

        // Create initial commit
        File.WriteAllText(Path.Combine(_repo.Path, "amend-file.cs"), "// v1");
        RunGit(_repo.Path, "add", "amend-file.cs");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "commit", "-m", "Initial");

        // Amend with staged changes
        File.WriteAllText(Path.Combine(_repo.Path, "amend-file.cs"), "// v2");
        RunGit(_repo.Path, "add", "amend-file.cs");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "commit", "--amend");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task StackCommit_BranchNotInStack_ShowsError()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "wrong-branch");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "real-branch", "-c");

        File.WriteAllText(Path.Combine(_repo.Path, "file.cs"), "// content");
        RunGit(_repo.Path, "add", "file.cs");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "commit", "-m", "test", "-b", "nonexistent");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task StackCommit_WithStaleWarning()
    {
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "init", "stale-stack");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "stale-a", "-c");
        await InProcessCliRunner.RunAsync(_repo.Path, "stack", "push", "stale-b", "-c");

        // Commit to the lower branch — upper branch becomes stale
        File.WriteAllText(Path.Combine(_repo.Path, "stale-file.cs"), "// stale test");
        RunGit(_repo.Path, "add", "stale-file.cs");

        var result = await InProcessCliRunner.RunAsync(_repo.Path, "stack", "commit", "-m", "Stale test", "-b", "stale-a");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("stale", result.Stdout, StringComparison.OrdinalIgnoreCase);
    }
}
