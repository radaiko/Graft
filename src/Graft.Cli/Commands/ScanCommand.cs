using System.CommandLine;
using Graft.Core.Config;
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
        command.Add(CreateAutoFetchCommand());

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
        var configDir = CliPaths.GetConfigDir();

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
        var configDir = CliPaths.GetConfigDir();

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
        var configDir = CliPaths.GetConfigDir();

        try
        {
            var paths = ScanPathManager.List(configDir);

            if (paths.Count == 0)
            {
                Console.WriteLine("No scan paths registered. Use 'graft scan add <directory>' to add one.");
                return;
            }

            foreach (var p in paths)
                Console.WriteLine(p.Path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    // ========================
    // Auto-fetch subcommands
    // ========================

    private static Command CreateAutoFetchCommand()
    {
        var command = new Command("auto-fetch", "Manage automatic background fetching for repos");

        command.Add(CreateAutoFetchEnableCommand());
        command.Add(CreateAutoFetchDisableCommand());
        command.Add(CreateAutoFetchListCommand());

        return command;
    }

    private static Command CreateAutoFetchEnableCommand()
    {
        var nameArg = new Argument<string?>("name")
        {
            Description = "Repository name (uses current directory if omitted)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var command = new Command("enable", "Enable auto-fetch for a repository");
        command.Add(nameArg);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArg);
            DoAutoFetchEnable(name);
        });

        return command;
    }

    private static Command CreateAutoFetchDisableCommand()
    {
        var nameArg = new Argument<string?>("name")
        {
            Description = "Repository name (uses current directory if omitted)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var command = new Command("disable", "Disable auto-fetch for a repository");
        command.Add(nameArg);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArg);
            DoAutoFetchDisable(name);
        });

        return command;
    }

    private static Command CreateAutoFetchListCommand()
    {
        var command = new Command("list", "List repos with their auto-fetch status");

        command.SetAction(_ =>
        {
            DoAutoFetchList();
        });

        return command;
    }

    private static void DoAutoFetchEnable(string? name)
    {
        var configDir = CliPaths.GetConfigDir();

        try
        {
            if (name != null)
            {
                AutoFetcher.EnableByName(name, configDir);
                Console.WriteLine($"Auto-fetch enabled for '{name}'.");
            }
            else
            {
                var repoPath = Path.GetFullPath(Directory.GetCurrentDirectory());
                AutoFetcher.Enable(repoPath, configDir);
                Console.WriteLine($"Auto-fetch enabled for '{Path.GetFileName(repoPath)}'.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static void DoAutoFetchDisable(string? name)
    {
        var configDir = CliPaths.GetConfigDir();

        try
        {
            if (name != null)
            {
                AutoFetcher.DisableByName(name, configDir);
                Console.WriteLine($"Auto-fetch disabled for '{name}'.");
            }
            else
            {
                var repoPath = Path.GetFullPath(Directory.GetCurrentDirectory());
                AutoFetcher.Disable(repoPath, configDir);
                Console.WriteLine($"Auto-fetch disabled for '{Path.GetFileName(repoPath)}'.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static void DoAutoFetchList()
    {
        var configDir = CliPaths.GetConfigDir();

        try
        {
            var cache = ConfigLoader.LoadRepoCache(configDir);
            var repos = cache.Repos.Where(r => !string.IsNullOrEmpty(r.Name)).OrderBy(r => r.Name).ToList();

            if (repos.Count == 0)
            {
                Console.WriteLine("No repos in cache. Register a scan directory with 'graft scan add <dir>' first.");
                return;
            }

            foreach (var repo in repos)
            {
                var status = repo.AutoFetch ? "on" : "off";
                var lastFetched = repo.LastFetched.HasValue
                    ? repo.LastFetched.Value.ToLocalTime().ToString("g")
                    : "never";
                Console.WriteLine($"  {status,-4} {repo.Name,-30} (last fetch: {lastFetched})");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

}
