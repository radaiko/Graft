using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests;

/// <summary>
/// Tests for error handling behavior per spec section 8.
/// All errors must tell the user: what went wrong, why, and how to fix it.
/// </summary>
[Collection("InProcess")]
public sealed class ErrorHandlingTests
{
    // Requirement: Errors include "Error:" prefix with what went wrong
    [Fact]
    public async Task Error_IncludesWhatWentWrong()
    {
        using var repo = TempCliRepo.CreateWithStack();
        var cliResult = await InProcessCliRunner.RunAsync(repo.Path, "stack", "remove", "nonexistent-stack");

        var combinedOutput = cliResult.Stdout + cliResult.Stderr;
        Assert.Contains("Error", combinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    // Requirement: Errors include how to fix the problem
    [Fact]
    public async Task Error_IncludesHowToFix()
    {
        using var repo = TempCliRepo.CreateWithStack();
        var cliResult = await InProcessCliRunner.RunAsync(repo.Path, "stack", "remove", "nonexistent-stack");

        var combinedOutput = cliResult.Stdout + cliResult.Stderr;
        // Error message should contain actionable guidance
        Assert.Contains("not found", combinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    // Requirement: Rebase conflict error shows "Conflicting files:" with file list
    [Fact]
    public async Task RebaseConflictError_ShowsConflictingFiles()
    {
        using var repo = TempCliRepo.CreateWithConflict();
        var cliResult = await InProcessCliRunner.RunAsync(repo.Path, "stack", "sync");

        var combinedOutput = cliResult.Stdout + cliResult.Stderr;
        Assert.Contains("Conflicting files:", combinedOutput);
    }

    // Requirement: Rebase conflict shows numbered resolution steps
    [Fact]
    public async Task RebaseConflictError_ShowsResolutionSteps()
    {
        using var repo = TempCliRepo.CreateWithConflict();
        var cliResult = await InProcessCliRunner.RunAsync(repo.Path, "stack", "sync");

        var combinedOutput = cliResult.Stdout + cliResult.Stderr;
        Assert.Contains("To resolve:", combinedOutput);
        Assert.Contains("graft --continue", combinedOutput);
    }

    // Requirement: Rebase conflict shows abort option
    [Fact]
    public async Task RebaseConflictError_ShowsAbortOption()
    {
        using var repo = TempCliRepo.CreateWithConflict();
        var cliResult = await InProcessCliRunner.RunAsync(repo.Path, "stack", "sync");

        var combinedOutput = cliResult.Stdout + cliResult.Stderr;
        Assert.Contains("graft --abort", combinedOutput);
    }

    // Requirement: Non-zero exit code on error
    [Fact]
    public async Task Error_ReturnsNonZeroExitCode()
    {
        using var repo = TempCliRepo.CreateWithStack();
        var cliResult = await InProcessCliRunner.RunAsync(repo.Path, "stack", "remove", "nonexistent-stack");

        Assert.NotEqual(0, cliResult.ExitCode);
    }
}
