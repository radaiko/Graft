using System.CommandLine;
using Graft.Core.Config;
using Graft.Core.Scan;
using Graft.Core.Worktree;

namespace Graft.Cli.Commands;

public static class WorktreeCommand
{
    public static Command Create()
    {
        var branchArg = new Argument<string?>("branch")
        {
            Description = "Branch to create worktree for",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var createOption = new Option<bool>("--create") { Description = "Create the branch if it doesn't exist" };
        createOption.Aliases.Add("-c");

        var command = new Command("wt", "Manage worktrees");
        command.Add(branchArg);
        command.Add(createOption);

        command.Add(CreateRemoveCommand());
        command.Add(CreateRemoveAlias());
        command.Add(CreateDelDeprecated());
        command.Add(CreateListCommand());
        command.Add(CreateListAlias());
        command.Add(CreateGotoCommand());

        command.SetAction(async (parseResult, ct) =>
        {
            var branch = parseResult.GetValue(branchArg);
            var create = parseResult.GetValue(createOption);
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                if (branch != null)
                {
                    await WorktreeManager.AddAsync(branch, repoPath, create, ct);
                    Console.WriteLine($"Created worktree for '{branch}'");

                    // Best-effort: add worktree to repo cache for graft cd
                    try
                    {
                        var wtPath = WorktreeManager.GetWorktreePath(branch, repoPath);
                        var configDir = CliPaths.GetConfigDir();
                        ConfigLoader.AddRepoToCache(new CachedRepo
                        {
                            Name = Path.GetFileName(wtPath),
                            Path = wtPath,
                            Branch = branch,
                        }, configDir);
                    }
                    catch { }
                }
                else
                {
                    // No args: show help
                    Console.Error.WriteLine("Usage: graft wt <branch> [--create/-c] | graft wt remove <branch> [--force/-f] | graft wt list");
                    Environment.ExitCode = 1;
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

    private static Command CreateRemoveCommand()
    {
        var branchArg = new Argument<string>("branch") { Description = "Branch whose worktree to delete" };
        var forceOption = new Option<bool>("--force") { Description = "Override dirty checks" };
        forceOption.Aliases.Add("-f");

        var command = new Command("remove", "Delete a worktree");
        command.Add(branchArg);
        command.Add(forceOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var branch = parseResult.GetValue(branchArg)!;
            var force = parseResult.GetValue(forceOption);
            await DoRemove(branch, force, ct);
        });

        return command;
    }

    private static Command CreateRemoveAlias()
    {
        var branchArg = new Argument<string>("branch") { Description = "Branch whose worktree to delete" };
        var forceOption = new Option<bool>("--force") { Description = "Override dirty checks" };
        forceOption.Aliases.Add("-f");

        var command = new Command("rm", "Delete a worktree");
        command.Hidden = true;
        command.Add(branchArg);
        command.Add(forceOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var branch = parseResult.GetValue(branchArg)!;
            var force = parseResult.GetValue(forceOption);
            await DoRemove(branch, force, ct);
        });

        return command;
    }

    private static Command CreateDelDeprecated()
    {
        var branchArg = new Argument<string>("branch") { Description = "Branch whose worktree to delete" };
        var forceOption = new Option<bool>("--force") { Description = "Override dirty checks" };
        forceOption.Aliases.Add("-f");

        var command = new Command("del", "Delete a worktree");
        command.Hidden = true;
        command.Add(branchArg);
        command.Add(forceOption);

        command.SetAction(async (parseResult, ct) =>
        {
            Console.Error.WriteLine("Warning: 'graft wt del' is deprecated. Use 'graft wt remove' instead.");
            var branch = parseResult.GetValue(branchArg)!;
            var force = parseResult.GetValue(forceOption);
            await DoRemove(branch, force, ct);
        });

        return command;
    }

    private static async Task DoRemove(string branch, bool force, CancellationToken ct)
    {
        var repoPath = Directory.GetCurrentDirectory();

        var wtPath = WorktreeManager.GetWorktreePath(branch, repoPath);
        if (!Directory.Exists(wtPath))
        {
            Console.Error.WriteLine($"Error: No worktree found for '{branch}'. Use 'graft wt list' to see existing worktrees.");
            Environment.ExitCode = 1;
            return;
        }

        if (!force)
        {
            if (Console.IsInputRedirected)
            {
                Console.Error.WriteLine("Error: Cannot prompt for confirmation. Use --force to skip.");
                Environment.ExitCode = 1;
                return;
            }

            Console.Write($"Remove worktree for '{branch}'? [y/N] ");
            var response = Console.ReadLine();
            if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted.");
                return;
            }
        }

        try
        {
            await WorktreeManager.RemoveAsync(branch, repoPath, force, ct);
            Console.WriteLine($"Removed worktree for '{branch}'");

            // Best-effort: remove worktree from repo cache
            try
            {
                ConfigLoader.RemoveRepoFromCache(wtPath, CliPaths.GetConfigDir());
            }
            catch { }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List worktrees with status");
        command.SetAction(async (parseResult, ct) => await DoList(ct));
        return command;
    }

    private static Command CreateListAlias()
    {
        var command = new Command("ls", "List worktrees with status");
        command.Hidden = true;
        command.SetAction(async (parseResult, ct) => await DoList(ct));
        return command;
    }

    private static async Task DoList(CancellationToken ct)
    {
        var repoPath = Directory.GetCurrentDirectory();

        try
        {
            var worktrees = await WorktreeManager.ListAsync(repoPath, ct);
            foreach (var wt in worktrees)
            {
                var branch = wt.Branch ?? "(detached)";
                Console.WriteLine($"{wt.Path}  [{branch}]");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static Command CreateGotoCommand()
    {
        var branchArg = new Argument<string>("branch") { Description = "Branch whose worktree to navigate to" };

        var command = new Command("goto", "Print worktree path for shell cd");
        command.Hidden = true;
        command.Add(branchArg);

        command.SetAction((parseResult) =>
        {
            Console.Error.WriteLine("Warning: 'graft wt goto' is deprecated. Use 'graft cd <name>' instead.");

            var branch = parseResult.GetValue(branchArg)!;
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                var wtPath = WorktreeManager.GetWorktreePath(branch, repoPath);
                Console.WriteLine(wtPath);
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
