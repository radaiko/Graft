using System.CommandLine;
using Graft.Core.Scan;

namespace Graft.Cli.Commands;

public static class ScanCommand
{
    public static Command Create()
    {
        var command = new Command("scan", "Manage repo scan directories");

        command.Add(CreateAddCommand());
        command.Add(CreateRemoveCommand());
        command.Add(CreateRemoveAlias());
        command.Add(CreateListCommand());
        command.Add(CreateListAlias());

        return command;
    }

    private static Command CreateAddCommand()
    {
        var dirArg = new Argument<string>("directory") { Description = "Directory to register for repo scanning" };

        var command = new Command("add", "Register a directory for repo scanning");
        command.Add(dirArg);

        command.SetAction((parseResult) =>
        {
            var directory = parseResult.GetValue(dirArg)!;
            DoAdd(directory);
        });

        return command;
    }

    private static void DoAdd(string directory)
    {
        var configDir = GetConfigDir();

        try
        {
            ScanPathManager.Add(directory, configDir);
            Console.WriteLine($"Added scan path: {Path.GetFullPath(directory)}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static Command CreateRemoveCommand()
    {
        var dirArg = new Argument<string>("directory") { Description = "Directory to unregister" };

        var command = new Command("remove", "Unregister a scan directory");
        command.Add(dirArg);

        command.SetAction((parseResult) =>
        {
            var directory = parseResult.GetValue(dirArg)!;
            DoRemove(directory);
        });

        return command;
    }

    private static Command CreateRemoveAlias()
    {
        var dirArg = new Argument<string>("directory") { Description = "Directory to unregister" };

        var command = new Command("rm", "Unregister a scan directory");
        command.Hidden = true;
        command.Add(dirArg);

        command.SetAction((parseResult) =>
        {
            var directory = parseResult.GetValue(dirArg)!;
            DoRemove(directory);
        });

        return command;
    }

    private static void DoRemove(string directory)
    {
        var configDir = GetConfigDir();

        try
        {
            ScanPathManager.Remove(directory, configDir);
            Console.WriteLine($"Removed scan path: {Path.GetFullPath(directory)}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List registered scan directories");

        command.SetAction((parseResult) =>
        {
            DoList();
        });

        return command;
    }

    private static Command CreateListAlias()
    {
        var command = new Command("ls", "List registered scan directories");
        command.Hidden = true;

        command.SetAction((parseResult) =>
        {
            DoList();
        });

        return command;
    }

    private static void DoList()
    {
        var configDir = GetConfigDir();
        var paths = ScanPathManager.List(configDir);

        if (paths.Count == 0)
        {
            Console.WriteLine("No scan paths registered. Use 'graft scan add <directory>' to add one.");
            return;
        }

        foreach (var p in paths)
            Console.WriteLine(p.Path);
    }

    private static string GetConfigDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "graft");
    }
}
