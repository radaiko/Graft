using System.CommandLine;
using Graft.Core.Nuke;

namespace Graft.Cli.Commands;

public static class NukeCommand
{
    public static Command Create()
    {
        var forceOption = CreateForceOption("Override dirty checks");

        var command = new Command("nuke", "Remove all graft resources (worktrees, stacks, gone branches)");
        command.Add(forceOption);

        command.Add(CreateWtCommand());
        command.Add(CreateStackCommand());
        command.Add(CreateBranchesCommand());

        command.SetAction(async (parseResult, ct) =>
        {
            var force = parseResult.GetValue(forceOption);
            var repoPath = Directory.GetCurrentDirectory();

            Console.Write("This will remove all worktrees, stacks, and gone branches. Continue? [y/N] ");
            var response = Console.ReadLine();
            if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted.");
                return;
            }

            var result = await NukeManager.NukeAllAsync(repoPath, force, ct);
            PrintResult(result);
        });

        return command;
    }

    private static Command CreateWtCommand()
    {
        var forceOption = CreateForceOption("Override dirty checks");
        var command = new Command("wt", "Remove all worktrees");
        command.Add(forceOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var force = parseResult.GetValue(forceOption);
            var repoPath = Directory.GetCurrentDirectory();

            Console.Write("This will remove all worktrees. Continue? [y/N] ");
            var response = Console.ReadLine();
            if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted.");
                return;
            }

            var result = await NukeManager.NukeWorktreesAsync(repoPath, force, ct);
            PrintResult(result);
        });

        return command;
    }

    private static Command CreateStackCommand()
    {
        var forceOption = CreateForceOption("Override dirty checks");
        var command = new Command("stack", "Remove all stacks");
        command.Add(forceOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var force = parseResult.GetValue(forceOption);
            var repoPath = Directory.GetCurrentDirectory();

            Console.Write("This will remove all stacks. Continue? [y/N] ");
            var response = Console.ReadLine();
            if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted.");
                return;
            }

            var result = await NukeManager.NukeStacksAsync(repoPath, force, ct);
            PrintResult(result);
        });

        return command;
    }

    private static Command CreateBranchesCommand()
    {
        var command = new Command("branches", "Remove branches whose upstream is gone");

        command.SetAction(async (parseResult, ct) =>
        {
            var repoPath = Directory.GetCurrentDirectory();

            Console.Write("This will remove local branches whose upstream is gone. Continue? [y/N] ");
            var response = Console.ReadLine();
            if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted.");
                return;
            }

            var result = await NukeManager.NukeBranchesAsync(repoPath, ct);
            PrintResult(result);
        });

        return command;
    }

    private static Option<bool> CreateForceOption(string description = "Force operation")
    {
        var opt = new Option<bool>("--force") { Description = description };
        opt.Aliases.Add("-f");
        return opt;
    }

    private static void PrintResult(NukeResult result)
    {
        foreach (var item in result.Removed)
            Console.WriteLine($"  Removed: {item}");
        foreach (var item in result.Skipped)
            Console.WriteLine($"  Skipped: {item}");
        foreach (var item in result.Errors)
            Console.Error.WriteLine($"  Error: {item}");

        if (result.Removed.Count == 0 && result.Skipped.Count == 0 && result.Errors.Count == 0)
            Console.WriteLine("Nothing to remove.");
        else if (result.Errors.Count > 0)
            Environment.ExitCode = 1;
    }
}
