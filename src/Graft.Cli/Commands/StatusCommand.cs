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

    // ── Overview (all repos, 1 line each) ────────────────────────────────

    private static async Task DoOverviewStatus(string configDir, CancellationToken ct)
    {
        var statuses = await StatusCollector.CollectAllAsync(configDir, ct);

        if (statuses.Count == 0)
        {
            Console.WriteLine("No repos found. Run 'graft scan add <directory>' to register scan paths.");
            return;
        }

        var maxNameWidth = statuses.Max(s => s.Name.Length);

        foreach (var status in statuses)
            PrintRepoOverview(status, maxNameWidth);
    }

    private static void PrintRepoOverview(RepoStatus status, int nameWidth)
    {
        var namePad = status.Name.PadRight(nameWidth);
        var nameStr = $"{Ansi.Bold}{namePad}{Ansi.Reset}";

        if (!status.IsAccessible)
        {
            Console.WriteLine($"  {nameStr}  {Ansi.Red}\u2717 {status.Error}{Ansi.Reset}");
            return;
        }

        var branch = $"{Ansi.Cyan}{status.Branch ?? "(detached)"}{Ansi.Reset}";

        var badges = new List<string>();

        bool isClean = status.Ahead == 0 && status.Behind == 0
                       && status.ChangedFiles == 0 && status.UntrackedFiles == 0;

        if (isClean)
        {
            badges.Add($"{Ansi.Green}\u2713{Ansi.Reset}");
        }
        else
        {
            if (status.Ahead > 0)
                badges.Add($"{Ansi.Green}\u2191{status.Ahead}{Ansi.Reset}");
            if (status.Behind > 0)
                badges.Add($"{Ansi.Red}\u2193{status.Behind}{Ansi.Reset}");
            if (status.ChangedFiles > 0)
                badges.Add($"{Ansi.Yellow}{status.ChangedFiles}\u0394{Ansi.Reset}");
            if (status.UntrackedFiles > 0)
                badges.Add($"{Ansi.Gray}{status.UntrackedFiles}?{Ansi.Reset}");
        }

        if (status.ActiveStackName != null)
            badges.Add($"{Ansi.Magenta}\u2691{status.ActiveStackName}({status.ActiveStackBranchCount}){Ansi.Reset}");

        if (status.Worktrees.Count > 0)
            badges.Add($"{Ansi.Blue}{status.Worktrees.Count}wt{Ansi.Reset}");

        Console.WriteLine($"  {nameStr}  {branch}  {string.Join("  ", badges)}");
    }

    // ── Detailed (single repo) ───────────────────────────────────────────

    private static async Task DoDetailedStatus(string repoName, string configDir, CancellationToken ct)
    {
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
        Console.WriteLine($"  {Ansi.Bold}{status.Name}{Ansi.Reset}  {Ansi.Gray}{tilded}{Ansi.Reset}");

        if (!status.IsAccessible)
        {
            Console.WriteLine($"  {Ansi.Red}error: {status.Error}{Ansi.Reset}");
            return;
        }

        Console.WriteLine();

        // Branch + upstream
        Console.WriteLine($"  {Ansi.Gray}branch{Ansi.Reset}     {Ansi.Cyan}{status.Branch ?? "(detached)"}{Ansi.Reset}");

        if (status.Upstream != null)
        {
            var syncLabel = (status.Ahead == 0 && status.Behind == 0)
                ? $"{Ansi.Green}\u2713 synced{Ansi.Reset}"
                : FormatAheadBehind(status.Ahead, status.Behind);
            Console.WriteLine($"  {Ansi.Gray}upstream{Ansi.Reset}   {status.Upstream}  {syncLabel}");
        }
        else
        {
            Console.WriteLine($"  {Ansi.Gray}upstream{Ansi.Reset}   {Ansi.Gray}\u2014{Ansi.Reset}");
        }

        // Changed / untracked
        Console.WriteLine($"  {Ansi.Gray}changed{Ansi.Reset}    {FormatCount(status.ChangedFiles, Ansi.Yellow)}");
        Console.WriteLine($"  {Ansi.Gray}untracked{Ansi.Reset}  {FormatCount(status.UntrackedFiles, Ansi.Yellow)}");

        // Stacks
        PrintDetailedStacks(status);

        // Worktrees
        if (status.Worktrees.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  {Ansi.Blue}worktrees{Ansi.Reset}");
            foreach (var wt in status.Worktrees)
            {
                var branchLabel = wt.Branch ?? "(detached)";
                Console.WriteLine($"    {Ansi.Cyan}{branchLabel}{Ansi.Reset}  {Ansi.Gray}\u2192 {wt.Path}{Ansi.Reset}");
            }
        }
    }

    private static void PrintDetailedStacks(RepoStatus status)
    {
        if (status.Stacks.Count == 0)
            return;

        foreach (var stack in status.Stacks)
        {
            var active = stack.IsActive ? $" {Ansi.Green}(active){Ansi.Reset}" : "";
            Console.WriteLine();
            Console.WriteLine($"  {Ansi.Magenta}\u2691 {stack.Name}{Ansi.Reset}{active}");
            Console.WriteLine($"    {Ansi.Gray}{stack.Trunk}{Ansi.Reset}");

            for (int i = 0; i < stack.Branches.Count; i++)
            {
                var branch = stack.Branches[i];
                var indent = new string(' ', 4 + (i * 5));
                var connector = $"{Ansi.Gray}\u2514\u2500\u2500 {Ansi.Reset}";

                var abInfo = FormatBranchAheadBehind(branch.Ahead, branch.Behind);
                Console.WriteLine($"{indent}{connector}{Ansi.Cyan}{branch.Name}{Ansi.Reset}{abInfo}");
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string FormatAheadBehind(int ahead, int behind)
    {
        var parts = new List<string>();
        if (ahead > 0) parts.Add($"{Ansi.Green}\u2191{ahead}{Ansi.Reset}");
        if (behind > 0) parts.Add($"{Ansi.Red}\u2193{behind}{Ansi.Reset}");
        return string.Join(" ", parts);
    }

    private static string FormatBranchAheadBehind(int ahead, int behind)
    {
        if (ahead == 0 && behind == 0) return "";

        var parts = new List<string>();
        if (ahead > 0) parts.Add($"{Ansi.Green}\u2191{ahead}{Ansi.Reset}");
        if (behind > 0) parts.Add($"{Ansi.Red}\u2193{behind}{Ansi.Reset}");
        return "  " + string.Join(" ", parts);
    }

    private static string FormatCount(int count, string color)
    {
        return count > 0
            ? $"{color}{count}{Ansi.Reset}"
            : $"{Ansi.Gray}\u2014{Ansi.Reset}";
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
