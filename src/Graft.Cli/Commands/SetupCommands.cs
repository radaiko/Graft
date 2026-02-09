using System.CommandLine;
using Graft.Core.AutoUpdate;
using Graft.Core.Install;

namespace Graft.Cli.Commands;

public static class InstallCommand
{
    public static Command Create()
    {
        var command = new Command("install", "Create gt and git gt aliases.");

        command.SetAction((parseResult) =>
        {
            try
            {
                var binaryPath = Environment.ProcessPath
                    ?? throw new InvalidOperationException(
                        "Cannot determine binary path. Run graft using its full path.");
                AliasInstaller.Install(binaryPath);
                Console.WriteLine("Installed aliases: gt, git gt");

                var shellResult = ShellProfileInstaller.Install();
                if (shellResult is null)
                {
                    Console.WriteLine();
                    Console.WriteLine("Could not detect your shell. Shell integration for 'graft cd' was not installed.");
                    Console.WriteLine("Supported shells: bash, zsh, fish, PowerShell.");
                }
                else if (shellResult.AlreadyPresent)
                {
                    Console.WriteLine($"Shell integration already in {shellResult.ProfilePath}");
                }
                else
                {
                    Console.WriteLine($"Added shell integration to {shellResult.ProfilePath}");
                    Console.WriteLine("Restart your shell or run: source " + shellResult.ProfilePath);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }
}

public static class UninstallCommand
{
    public static Command Create()
    {
        var command = new Command("uninstall", "Remove aliases.");

        command.SetAction((parseResult) =>
        {
            try
            {
                var binaryPath = Environment.ProcessPath
                    ?? throw new InvalidOperationException(
                        "Cannot determine binary path. Run graft using its full path.");
                AliasInstaller.Uninstall(binaryPath);
                ShellProfileInstaller.Uninstall();
                Console.WriteLine("Removed aliases and shell integration.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }
}

public static class UpdateCommand
{
    public static Command Create()
    {
        var command = new Command("update", "Check for and apply updates.");

        command.SetAction(async (parseResult, ct) =>
        {
            try
            {
                var stateDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "graft");
                var binaryPath = Environment.ProcessPath
                    ?? throw new InvalidOperationException(
                        "Cannot determine binary path. Run graft using its full path.");

                // 1. If pending update exists, apply it
                if (UpdateApplier.HasPendingUpdate(stateDir))
                {
                    var applied = await UpdateApplier.ApplyPendingUpdateAsync(stateDir, binaryPath);
                    if (applied)
                    {
                        Console.WriteLine("Update applied successfully. Restart graft to use the new version.");
                        return;
                    }
                }

                // 2. Check for new versions (blocking, ignore rate limit)
                var currentVersion = VersionCommand.GetCurrentVersion();
                Console.WriteLine($"Current version: {currentVersion.ToString(3)}");
                Console.WriteLine("Checking for updates...");

                var staged = await ReleaseFetcher.CheckAndStageUpdateAsync(
                    stateDir, currentVersion, ignoreRateLimit: true);

                if (staged)
                {
                    // 3. Apply immediately
                    var applied = await UpdateApplier.ApplyPendingUpdateAsync(stateDir, binaryPath);
                    if (applied)
                    {
                        Console.WriteLine("Update applied successfully. Restart graft to use the new version.");
                        return;
                    }
                }

                Console.WriteLine("graft is up to date.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }
}

public static class VersionCommand
{
    /// <summary>
    /// Returns the assembly version as a 3-component Version (major.minor.build).
    /// Falls back to 0.1.0 if assembly metadata is unavailable (e.g. under aggressive trimming).
    /// </summary>
    public static Version GetCurrentVersion() =>
        typeof(VersionCommand).Assembly.GetName().Version ?? new Version(0, 1, 0);

    public static Command Create()
    {
        var command = new Command("version", "Print version.");

        command.SetAction((parseResult) =>
        {
            Console.WriteLine($"graft {GetCurrentVersion().ToString(3)}");
        });

        return command;
    }
}
