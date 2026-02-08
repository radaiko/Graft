using System.CommandLine;
using Graft.Core.Config;
using Graft.Core.Status;

namespace Graft.Cli.Commands;

public static class StatusCommand
{
    public static Command Create() => BuildCommand("status", hidden: false);

    public static Command CreateAlias() => BuildCommand("st", hidden: true);

    private static Command BuildCommand(string name, bool hidden)
    {
        var nameArg = new Argument<string?>("reponame")
        {
            Description = "Repo name for detailed status (omit for overview of all repos)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var command = new Command(name, "Cross-repo status overview");
        command.Hidden = hidden;
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: Failed to collect status: {ex.Message}");
            await Console.Error.WriteLineAsync("Ensure your repos are accessible and try again.");
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

            PrintRepoOverview(status);
        }
    }

    private static void PrintRepoOverview(RepoStatus status)
    {
        var tilded = TildePath(status.Path);
        Console.WriteLine($"{status.Name}  {tilded}");

        if (!status.IsAccessible)
        {
            Console.WriteLine($"  status   inaccessible: {status.Error}");
            return;
        }

        // Branch
        Console.WriteLine($"  branch   {status.Branch ?? "(detached)"}");

        // Status line: ahead/behind + changed
        var parts = new List<string>();
        if (status.Ahead > 0) parts.Add($"\u2191{status.Ahead}");
        if (status.Behind > 0) parts.Add($"\u2193{status.Behind}");
        if (status.ChangedFiles > 0) parts.Add($"{status.ChangedFiles} changed");
        if (status.UntrackedFiles > 0) parts.Add($"{status.UntrackedFiles} untracked");

        var statusText = parts.Count > 0 ? string.Join("  ", parts) : "clean";
        Console.WriteLine($"  status   {statusText}");

        // Stack
        if (status.ActiveStackName != null)
            Console.WriteLine($"  stack    {status.ActiveStackName} ({status.ActiveStackBranchCount} branches)");
        else
            Console.WriteLine("  stack    \u2014");

        // Worktrees
        if (status.Worktrees.Count > 0)
            Console.WriteLine($"  worktrees  {status.Worktrees.Count} active");
        else
            Console.WriteLine("  worktrees  \u2014");
    }

    private static async Task DoDetailedStatus(string repoName, string configDir, CancellationToken ct)
    {
        // Find repo in cache
        var cache = ConfigLoader.LoadRepoCache(configDir);
        var repo = cache.Repos.FirstOrDefault(r =>
            string.Equals(r.Name, repoName, StringComparison.OrdinalIgnoreCase));

        if (repo == null)
        {
            await Console.Error.WriteLineAsync($"Error: No repo found matching '{repoName}'.");
            await Console.Error.WriteLineAsync("Run 'graft scan add <directory>' to register scan paths, then try again.");
            Environment.ExitCode = 1;
            return;
        }

        if (!Directory.Exists(repo.Path))
        {
            await Console.Error.WriteLineAsync($"Error: Repo path no longer exists: {repo.Path}");
            await Console.Error.WriteLineAsync("Run 'graft scan' to refresh the repo cache.");
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
                : $"\u2191{status.Ahead} \u2193{status.Behind}";
            Console.WriteLine($"  upstream  {status.Upstream} ({upstreamDetail})");
        }
        else
        {
            Console.WriteLine("  upstream  \u2014");
        }

        // Changed/untracked
        Console.WriteLine($"  changed   {(status.ChangedFiles > 0 ? status.ChangedFiles.ToString() : "\u2014")}");
        Console.WriteLine($"  untracked {(status.UntrackedFiles > 0 ? status.UntrackedFiles.ToString() : "\u2014")}");

        // Stacks
        PrintDetailedStacks(status);

        // Worktrees
        if (status.Worktrees.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  worktrees:");
            foreach (var wt in status.Worktrees)
            {
                var branchLabel = wt.Branch ?? "(detached)";
                Console.WriteLine($"    {branchLabel}  \u2192 {wt.Path}");
            }
        }
    }

    private static void PrintDetailedStacks(RepoStatus status)
    {
        if (status.Stacks.Count == 0)
            return;

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
                var connector = "\u2514\u2500\u2500 ";
                var abInfo = $"(\u2191{branch.Ahead} \u2193{branch.Behind})";
                Console.WriteLine($"{indent}{connector}{branch.Name}  {abInfo}");
            }
        }
    }

    private static string TildePath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return path;

        try
        {
            var normalizedHome = Path.GetFullPath(home);
            var normalizedPath = Path.GetFullPath(path);
            var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (normalizedPath.StartsWith(normalizedHome, comparison) &&
                (normalizedPath.Length == normalizedHome.Length ||
                 normalizedPath[normalizedHome.Length] == Path.DirectorySeparatorChar))
                return "~" + normalizedPath[normalizedHome.Length..];
        }
        catch
        {
            // Malformed path — fall through to return original
        }

        return path;
    }
}
