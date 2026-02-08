using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Graft.Cli.Commands;

namespace Graft.Cli.Tests.Helpers;

/// <summary>
/// Shared helper for CLI integration tests.
/// Mirrors Program.cs command setup and provides utilities for parsing and running commands.
/// </summary>
public static class CliTestHelper
{
    /// <summary>
    /// Builds the root command the same way Program.cs does.
    /// Use for argument parsing tests via root.Parse(...).
    /// </summary>
    public static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("Graft — stacked branches and worktree management");

        // Grouped commands — mirrors Program.cs
        root.Add(StackCommand.Create());
        root.Add(WorktreeCommand.Create());
        root.Add(NukeCommand.Create());
        root.Add(ScanCommand.Create());
        root.Add(CdCommand.Create());

        // Status command
        root.Add(StatusCommand.Create());
        root.Add(StatusCommand.CreateAlias());

        // UI command
        root.Add(UiCommand.Create());

        // Setup commands
        root.Add(InstallCommand.Create());
        root.Add(UninstallCommand.Create());
        root.Add(UpdateCommand.Create());
        root.Add(VersionCommand.Create());

        // Global options: --continue / --abort
        var continueOption = new Option<bool>("--continue") { Description = "Continue after resolving conflicts" };
        var abortOption = new Option<bool>("--abort") { Description = "Abort an in-progress operation" };
        root.Add(continueOption);
        root.Add(abortOption);

        // Root action handles --continue / --abort (must match Program.cs)
        root.SetAction(async (parseResult, ct) =>
        {
            var doContinue = parseResult.GetValue(continueOption);
            var doAbort = parseResult.GetValue(abortOption);

            if (doContinue && doAbort)
            {
                Console.Error.WriteLine("Error: --continue and --abort cannot be used together.");
                Environment.ExitCode = 1;
                return;
            }

            if (doContinue)
            {
                Console.WriteLine("Continuing...");
                return;
            }
            if (doAbort)
            {
                Console.WriteLine("Aborting...");
                return;
            }

            // No args: show help
            Console.WriteLine(root.Description);
            Console.WriteLine("Usage: graft [command] [options]");
        });

        return root;
    }

    /// <summary>
    /// Parses a command line string and returns the ParseResult.
    /// Convenience wrapper around BuildRootCommand().Parse().
    /// </summary>
    public static ParseResult Parse(string commandLine)
    {
        return BuildRootCommand().Parse(commandLine);
    }

    /// <summary>
    /// Runs the CLI via `dotnet run` and captures output.
    /// Use for E2E tests that need to verify actual CLI output.
    /// </summary>
    public static async Task<CliResult> RunAsync(string? workingDir = null, params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.StartInfo.ArgumentList.Add("run");
        process.StartInfo.ArgumentList.Add("--no-build");
        process.StartInfo.ArgumentList.Add("--project");
        process.StartInfo.ArgumentList.Add(GetCliProjectPath());
        process.StartInfo.ArgumentList.Add("--");

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        if (workingDir != null)
            process.StartInfo.WorkingDirectory = workingDir;

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync();

        return new CliResult(process.ExitCode, stdoutTask.Result.TrimEnd(), stderrTask.Result.TrimEnd());
    }

    private static string GetCliProjectPath()
    {
        // Walk up from the test assembly to find the repo root (contains src/Graft.sln)
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "src", "Graft.sln")))
            dir = Path.GetDirectoryName(dir);

        if (dir == null)
            throw new InvalidOperationException("Cannot find src/Graft.sln from " + AppContext.BaseDirectory);

        return Path.Combine(dir, "src", "Graft.Cli");
    }
}

public record CliResult(int ExitCode, string Stdout, string Stderr);
