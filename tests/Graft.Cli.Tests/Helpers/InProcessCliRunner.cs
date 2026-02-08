using System.CommandLine;

namespace Graft.Cli.Tests.Helpers;

/// <summary>
/// Runs CLI commands in-process via the root command's Parse/InvokeAsync pipeline.
/// This allows coverlet to instrument the Graft.Cli code for coverage.
/// </summary>
public static class InProcessCliRunner
{
    private static readonly object Lock = new();

    /// <summary>
    /// Runs a CLI command in-process, capturing stdout and stderr.
    /// Uses a lock to prevent concurrent CWD changes.
    /// </summary>
    public static async Task<CliResult> RunAsync(string? workingDir, params string[] args)
    {
        return await RunCoreAsync(workingDir, stdin: null, args);
    }

    /// <summary>
    /// Runs a CLI command in-process with stdin input, capturing stdout and stderr.
    /// Used for commands that prompt for confirmation (e.g., nuke).
    /// </summary>
    public static async Task<CliResult> RunWithStdinAsync(string? workingDir, string stdin, params string[] args)
    {
        return await RunCoreAsync(workingDir, stdin, args);
    }

    private static async Task<CliResult> RunCoreAsync(string? workingDir, string? stdin, string[] args)
    {
        // We need exclusive access because we change CWD and redirect console
        return await Task.Run(() =>
        {
            lock (Lock)
            {
                var originalOut = Console.Out;
                var originalErr = Console.Error;
                var originalIn = Console.In;
                var originalCwd = Directory.GetCurrentDirectory();
                var originalExitCode = Environment.ExitCode;

                using var stdoutWriter = new StringWriter();
                using var stderrWriter = new StringWriter();

                try
                {
                    Console.SetOut(stdoutWriter);
                    Console.SetError(stderrWriter);
                    if (stdin != null)
                        Console.SetIn(new StringReader(stdin));
                    Environment.ExitCode = 0;

                    if (workingDir != null)
                        Directory.SetCurrentDirectory(workingDir);

                    var root = CliTestHelper.BuildRootCommand();
                    var parseResult = root.Parse(args);
                    parseResult.Invoke();

                    var exitCode = Environment.ExitCode;
                    return new CliResult(
                        exitCode,
                        stdoutWriter.ToString().TrimEnd(),
                        stderrWriter.ToString().TrimEnd());
                }
                catch (Exception ex)
                {
                    return new CliResult(
                        1,
                        stdoutWriter.ToString().TrimEnd(),
                        ex.Message);
                }
                finally
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalErr);
                    Console.SetIn(originalIn);
                    Directory.SetCurrentDirectory(originalCwd);
                    Environment.ExitCode = originalExitCode;
                }
            }
        });
    }
}
