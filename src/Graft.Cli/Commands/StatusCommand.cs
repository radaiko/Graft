using System.CommandLine;
using Graft.Core.Config;
using Graft.Core.Status;

namespace Graft.Cli.Commands;

public static class StatusCommand
{
    public static Command Create()
    {
        var nameArg = new Argument<string?>("reponame")
        {
            Description = "Repo name for detailed status (omit for overview of all repos)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var command = new Command("status", "Cross-repo status overview");
        command.Add(nameArg);

        command.SetAction(async (parseResult, ct) =>
        {
            var repoName = parseResult.GetValue(nameArg);
            await DoStatus(repoName, ct);
        });

        // Hidden alias: st
        var alias = new Command("st", "Cross-repo status overview");
        alias.Hidden = true;

        var aliasNameArg = new Argument<string?>("reponame")
        {
            Description = "Repo name for detailed status (omit for overview of all repos)",
            Arity = ArgumentArity.ZeroOrOne,
        };
        alias.Add(aliasNameArg);

        alias.SetAction(async (parseResult, ct) =>
        {
            var repoName = parseResult.GetValue(aliasNameArg);
            await DoStatus(repoName, ct);
        });

        // Return both as a list trick — caller adds them individually
        // Actually, we need to return just the main command and have Program.cs add the alias separately.
        // Follow CdCommand pattern: return main command. We'll add alias registration separately.
        return command;
    }

    public static Command CreateAlias()
    {
        var nameArg = new Argument<string?>("reponame")
        {
            Description = "Repo name for detailed status (omit for overview of all repos)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var command = new Command("st", "Cross-repo status overview");
        command.Hidden = true;
        command.Add(nameArg);

        command.SetAction(async (parseResult, ct) =>
        {
            var repoName = parseResult.GetValue(nameArg);
            await DoStatus(repoName, ct);
        });

        return command;
    }

    private static async Task DoStatus(string? repoName, CancellationToken ct)
    {
        var configDir = CliPaths.GetConfigDir();

        try
        {
            if (repoName != null)
            {
                await DoDetailedStatus(repoName, configDir, ct);
            }
            else
            {
                await DoOverviewStatus(configDir, ct);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async Task DoOverviewStatus(string configDir, CancellationToken ct)
    {
        var statuses = await StatusCollector.CollectAllAsync(configDir, ct);

        if (statuses.Count == 0)
        {
            Console.WriteLine("No repos found. Run 'graft scan add <directory>' to register scan paths.");
            return;
        }

        var first = true;
        foreach (var status in statuses)
        {
            if (!first) Console.WriteLine();
            first = false;

            var tilded = TildePath(status.Path);
            Console.WriteLine($"{status.Name}  {tilded}");

            if (!status.IsAccessible)
            {
                Console.WriteLine($"  status   inaccessible: {status.Error}");
                continue;
            }

            // Branch
            Console.WriteLine($"  branch   {status.Branch ?? "(detached)"}");

            // Status line: ahead/behind + changed
            var parts = new List<string>();
            if (status.Ahead > 0) parts.Add($"↑{status.Ahead}");
            if (status.Behind > 0) parts.Add($"↓{status.Behind}");
            if (status.ChangedFiles > 0) parts.Add($"{status.ChangedFiles} changed");
            if (status.UntrackedFiles > 0) parts.Add($"{status.UntrackedFiles} untracked");

            var statusText = parts.Count > 0 ? string.Join("  ", parts) : "clean";
            Console.WriteLine($"  status   {statusText}");

            // Stack
            if (status.ActiveStackName != null)
                Console.WriteLine($"  stack    {status.ActiveStackName} ({status.ActiveStackBranchCount} branches)");
            else
                Console.WriteLine("  stack    —");

            // Worktrees
            if (status.Worktrees.Count > 0)
                Console.WriteLine($"  worktrees  {status.Worktrees.Count} active");
            else
                Console.WriteLine("  worktrees  —");
        }
    }

    private static async Task DoDetailedStatus(string repoName, string configDir, CancellationToken ct)
    {
        // Find repo in cache
        var cache = ConfigLoader.LoadRepoCache(configDir);
        var repo = cache.Repos.FirstOrDefault(r =>
            string.Equals(r.Name, repoName, StringComparison.OrdinalIgnoreCase));

        if (repo == null)
        {
            Console.Error.WriteLine($"Error: No repo found matching '{repoName}'.");
            Console.Error.WriteLine("Run 'graft scan add <directory>' to register scan paths, then try again.");
            Environment.ExitCode = 1;
            return;
        }

        if (!Directory.Exists(repo.Path))
        {
            Console.Error.WriteLine($"Error: Repo path no longer exists: {repo.Path}");
            Environment.ExitCode = 1;
            return;
        }

        var status = await StatusCollector.CollectOneAsync(repo.Path, ct);

        var tilded = TildePath(status.Path);
        Console.WriteLine($"{status.Name}  {tilded}");

        if (!status.IsAccessible)
        {
            Console.WriteLine($"  error: {status.Error}");
            return;
        }

        // Branch + upstream
        Console.WriteLine($"  branch    {status.Branch ?? "(detached)"}");
        if (status.Upstream != null)
        {
            var upstreamDetail = (status.Ahead == 0 && status.Behind == 0)
                ? "up to date"
                : $"↑{status.Ahead} ↓{status.Behind}";
            Console.WriteLine($"  upstream  {status.Upstream} ({upstreamDetail})");
        }
        else
        {
            Console.WriteLine("  upstream  —");
        }

        // Changed/untracked
        Console.WriteLine($"  changed   {(status.ChangedFiles > 0 ? status.ChangedFiles.ToString() : "—")}");
        Console.WriteLine($"  untracked {(status.UntrackedFiles > 0 ? status.UntrackedFiles.ToString() : "—")}");

        // Stacks
        if (status.Stacks.Count > 0)
        {
            foreach (var stack in status.Stacks)
            {
                var active = stack.IsActive ? " (active)" : "";
                Console.WriteLine();
                Console.WriteLine($"  stack: {stack.Name}{active}");
                Console.WriteLine($"    {stack.Trunk}");

                for (int i = 0; i < stack.Branches.Count; i++)
                {
                    var branch = stack.Branches[i];
                    var indent = new string(' ', 4 + (i * 5));
                    var connector = "└── ";
                    var abInfo = $"(↑{branch.Ahead} ↓{branch.Behind})";
                    Console.WriteLine($"{indent}{connector}{branch.Name}  {abInfo}");
                }
            }
        }

        // Worktrees
        if (status.Worktrees.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  worktrees:");
            foreach (var wt in status.Worktrees)
            {
                var branchLabel = wt.Branch ?? "(detached)";
                Console.WriteLine($"    {branchLabel}  → {wt.Path}");
            }
        }
    }

    private static string TildePath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.Ordinal))
            return "~" + path[home.Length..];
        return path;
    }
}
