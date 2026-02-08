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
        // We need exclusive access because we change CWD and redirect console
        return await Task.Run(() =>
        {
            lock (Lock)
            {
                var originalOut = Console.Out;
                var originalErr = Console.Error;
                var originalCwd = Directory.GetCurrentDirectory();
                var originalExitCode = Environment.ExitCode;

                using var stdoutWriter = new StringWriter();
                using var stderrWriter = new StringWriter();

                try
                {
                    Console.SetOut(stdoutWriter);
                    Console.SetError(stderrWriter);
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
                    Directory.SetCurrentDirectory(originalCwd);
                    Environment.ExitCode = originalExitCode;
                }
            }
        });
    }
}
