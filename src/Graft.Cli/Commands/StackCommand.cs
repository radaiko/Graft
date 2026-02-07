using System.CommandLine;
using Graft.Core.Commit;
using Graft.Core.Config;
using Graft.Core.Git;
using Graft.Core.Stack;

namespace Graft.Cli.Commands;

public static class StackCommand
{
    public static Command Create()
    {
        var command = new Command("stack", "Manage stacked branches");

        command.Add(CreateInitCommand());
        command.Add(CreateListCommand());
        command.Add(CreateSwitchCommand());
        command.Add(CreatePushCommand());
        command.Add(CreatePopCommand());
        command.Add(CreateDropCommand());
        command.Add(CreateShiftCommand());
        command.Add(CreateCommitCommand());
        command.Add(CreateSyncCommand());
        command.Add(CreateLogCommand());
        command.Add(CreateDelCommand());

        return command;
    }

    private static Command CreateInitCommand()
    {
        var nameArg = new Argument<string>("name") { Description = "Name for the new stack" };
        var baseOption = new Option<string?>("--base") { Description = "Base branch (default: current branch)" };
        baseOption.Aliases.Add("-b");
        var command = new Command("init", "Create a new stack. Current branch is the trunk.");
        command.Add(nameArg);
        command.Add(baseOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var name = parseResult.GetValue(nameArg)!;
            var baseBranch = parseResult.GetValue(baseOption);
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                var stack = await StackManager.InitAsync(name, repoPath, baseBranch, ct);
                Console.WriteLine($"Created stack '{stack.Name}' with trunk '{stack.Trunk}'");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.Error.WriteLine($"Use a different name, or delete the existing stack with 'graft stack del {name}'.");
                Environment.ExitCode = 1;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not a git repository"))
            {
                Console.Error.WriteLine("Error: Not in a git repository.");
                Console.Error.WriteLine("Navigate to a git repository or run 'git init' to create one.");
                Environment.ExitCode = 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List all stacks");

        command.SetAction(async (parseResult, ct) =>
        {
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                var stacks = ConfigLoader.ListStacks(repoPath);
                if (stacks.Length == 0)
                {
                    Console.WriteLine("No stacks found. Run 'graft stack init <name>' to create one.");
                    return;
                }

                var active = ConfigLoader.LoadActiveStack(repoPath);
                foreach (var name in stacks)
                {
                    var marker = name == active ? "* " : "  ";
                    Console.WriteLine($"{marker}{name}");
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

    private static Command CreateSwitchCommand()
    {
        var nameArg = new Argument<string>("name") { Description = "Stack to switch to" };
        var command = new Command("switch", "Switch active stack");
        command.Add(nameArg);

        command.SetAction((parseResult) =>
        {
            var name = parseResult.GetValue(nameArg)!;
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                ActiveStackManager.SetActiveStack(name, repoPath);
                Console.WriteLine($"Switched to stack '{name}'");
            }
            catch (FileNotFoundException)
            {
                Console.Error.WriteLine($"Error: Stack '{name}' not found.");
                Environment.ExitCode = 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreatePushCommand()
    {
        var branchArg = new Argument<string>("branch") { Description = "Branch name to add to the stack" };
        var createOption = new Option<bool>("--create") { Description = "Create the branch if it doesn't exist" };
        createOption.Aliases.Add("-c");
        var command = new Command("push", "Add a branch to the top of the active stack");
        command.Add(branchArg);
        command.Add(createOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var branchName = parseResult.GetValue(branchArg)!;
            var create = parseResult.GetValue(createOption);
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                await StackManager.PushAsync(branchName, repoPath, create, ct);
                var stackName = ActiveStackManager.GetActiveStackName(repoPath);
                Console.WriteLine($"Added '{branchName}' to stack '{stackName}'");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already in stack"))
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreatePopCommand()
    {
        var command = new Command("pop", "Remove the top branch from the active stack");

        command.SetAction(async (parseResult, ct) =>
        {
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                var removed = await StackManager.PopAsync(repoPath, ct);
                Console.WriteLine($"Popped '{removed}' from stack");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateDropCommand()
    {
        var branchArg = new Argument<string>("branch") { Description = "Branch to remove from the stack" };
        var command = new Command("drop", "Remove a branch from the active stack (any position)");
        command.Add(branchArg);

        command.SetAction(async (parseResult, ct) =>
        {
            var branchName = parseResult.GetValue(branchArg)!;
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                await StackManager.DropAsync(branchName, repoPath, ct);
                Console.WriteLine($"Dropped '{branchName}' from stack");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateShiftCommand()
    {
        var branchArg = new Argument<string>("branch") { Description = "Branch to insert at the bottom of the stack" };
        var command = new Command("shift", "Insert a branch at the bottom of the active stack");
        command.Add(branchArg);

        command.SetAction(async (parseResult, ct) =>
        {
            var branchName = parseResult.GetValue(branchArg)!;
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                await StackManager.ShiftAsync(branchName, repoPath, ct);
                Console.WriteLine($"Inserted '{branchName}' at bottom of stack");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateCommitCommand()
    {
        var messageOption = new Option<string?>("-m")
        {
            Description = "Commit message",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var branchOption = new Option<string?>("--branch") { Description = "Target branch (default: top of stack)" };
        branchOption.Aliases.Add("-b");
        var amendOption = new Option<bool>("--amend") { Description = "Amend the latest commit instead of creating a new one" };
        var command = new Command("commit", "Commit staged changes to a branch in the active stack");
        command.Add(messageOption);
        command.Add(branchOption);
        command.Add(amendOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var message = parseResult.GetValue(messageOption);
            var branch = parseResult.GetValue(branchOption);
            var amend = parseResult.GetValue(amendOption);
            var repoPath = Directory.GetCurrentDirectory();

            if (!amend && string.IsNullOrWhiteSpace(message))
            {
                Console.Error.WriteLine("Error: Commit message is required. Use -m '<message>' or --amend.");
                Environment.ExitCode = 1;
                return;
            }

            try
            {
                var options = new CommitOptions { Amend = amend };
                var result = await CommitRouter.CommitAsync(branch, message ?? "", repoPath, options, ct);
                Console.WriteLine($"Committed to {result.TargetBranch} ({result.CommitSha})");
                if (result.BranchesAreStale)
                {
                    Console.WriteLine("Branches above are now stale. Run 'graft stack sync' to merge them.");
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No staged changes"))
            {
                Console.Error.WriteLine("Error: No staged changes to commit.");
                Console.Error.WriteLine("Stage changes first with 'git add <file>'.");
                Environment.ExitCode = 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateSyncCommand()
    {
        var branchArg = new Argument<string?>("branch")
        {
            Description = "Specific branch to sync (optional, syncs all if omitted)",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var command = new Command("sync", "Merge trunk into the active stack, bottom-to-top, then push");
        command.Add(branchArg);

        command.SetAction(async (parseResult, ct) =>
        {
            var branchName = parseResult.GetValue(branchArg);
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                var stackName = ActiveStackManager.GetActiveStackName(repoPath);
                var stack = ConfigLoader.LoadStack(stackName, repoPath);
                Console.WriteLine($"Syncing onto {stack.Trunk}...");

                var result = await StackManager.SyncAsync(repoPath, branchName, ct);

                foreach (var br in result.BranchResults)
                {
                    switch (br.Status)
                    {
                        case SyncStatus.UpToDate:
                            Console.WriteLine($"  \u2713 {br.Name} (up to date)");
                            break;
                        case SyncStatus.Merged:
                            Console.WriteLine($"  \u2713 {br.Name} (merged)");
                            break;
                        case SyncStatus.Conflict:
                            Console.WriteLine($"  \u2717 {br.Name} \u2014 conflict");
                            Console.WriteLine();
                            Console.WriteLine("Conflicting files:");
                            foreach (var file in br.ConflictingFiles)
                                Console.WriteLine($"  - {file}");
                            Console.WriteLine();
                            Console.WriteLine("To resolve:");
                            Console.WriteLine("  1. Fix conflicts in the files above");
                            Console.WriteLine("  2. Stage resolved files: git add <file>");
                            Console.WriteLine("  3. Continue: graft --continue");
                            Console.WriteLine();
                            Console.WriteLine("To abort: graft --abort");
                            Environment.ExitCode = 1;
                            return;
                    }
                }

                Console.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateLogCommand()
    {
        var command = new Command("log", "Visual graph of the active stack");

        command.SetAction(async (parseResult, ct) =>
        {
            var repoPath = Directory.GetCurrentDirectory();

            try
            {
                var stackName = ActiveStackManager.GetActiveStackName(repoPath);
                var stack = ConfigLoader.LoadStack(stackName, repoPath);

                var git = new GitRunner(repoPath, ct);
                var headResult = await git.RunAsync("rev-parse", "--abbrev-ref", "HEAD");
                var currentBranch = headResult.Success ? headResult.Stdout.Trim() : "";

                Console.WriteLine(stack.Trunk);

                if (stack.Branches.Count == 0)
                {
                    Console.WriteLine("  (no branches)");
                    return;
                }

                Console.WriteLine("\u2502");

                string parentBranch = stack.Trunk;
                for (int i = 0; i < stack.Branches.Count; i++)
                {
                    var branch = stack.Branches[i];
                    bool isLast = i == stack.Branches.Count - 1;

                    var branchCheck = await git.RunAsync("rev-parse", "--verify", $"refs/heads/{branch.Name}");
                    if (!branchCheck.Success)
                    {
                        var branchChar2 = isLast ? "\u2514" : "\u251c";
                        Console.WriteLine($"{branchChar2}\u2500\u2500 {branch.Name} (branch missing!)");
                        if (!isLast)
                            Console.WriteLine("\u2502");
                        parentBranch = branch.Name;
                        continue;
                    }

                    var countResult = await git.RunAsync("rev-list", "--count", $"{parentBranch}..{branch.Name}");
                    int commitCount = 0;
                    if (countResult.Success && int.TryParse(countResult.Stdout.Trim(), out var cc))
                        commitCount = cc;

                    var commitStr = commitCount == 1 ? "1 commit" : $"{commitCount} commits";
                    var headStr = currentBranch == branch.Name ? "  \u2190 HEAD" : "";

                    var branchChar = isLast ? "\u2514" : "\u251c";
                    Console.WriteLine($"{branchChar}\u2500\u2500 {branch.Name} ({commitStr}){headStr}");

                    var logResult = await git.RunAsync("log", "--oneline", $"{parentBranch}..{branch.Name}");
                    if (logResult.Success && !string.IsNullOrWhiteSpace(logResult.Stdout))
                    {
                        var prefix = isLast ? "    " : "\u2502   ";
                        foreach (var line in logResult.Stdout.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                        {
                            Console.WriteLine($"{prefix}{line.Trim()}");
                        }
                    }

                    if (!isLast)
                        Console.WriteLine("\u2502");

                    parentBranch = branch.Name;
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

    private static Command CreateDelCommand()
    {
        var nameArg = new Argument<string>("name") { Description = "Name of the stack to delete" };
        var forceOption = new Option<bool>("--force") { Description = "Override dirty checks" };
        forceOption.Aliases.Add("-f");
        var command = new Command("del", "Delete a stack. Branches are kept.");
        command.Add(nameArg);
        command.Add(forceOption);

        command.SetAction((parseResult) =>
        {
            var name = parseResult.GetValue(nameArg)!;
            var force = parseResult.GetValue(forceOption);
            var repoPath = Directory.GetCurrentDirectory();

            // Validate stack exists before prompting
            var stackPath = Path.Combine(
                Graft.Core.Git.GitRunner.ResolveGitCommonDir(repoPath),
                "graft", "stacks", $"{name}.toml");
            if (!File.Exists(stackPath))
            {
                Console.Error.WriteLine($"Error: Stack '{name}' not found.");
                Environment.ExitCode = 1;
                return;
            }

            Console.Write($"Delete stack '{name}'? Branches will be kept. [y/N] ");
            var response = Console.ReadLine();
            if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted.");
                return;
            }

            try
            {
                StackManager.Delete(name, repoPath);
                Console.WriteLine($"Deleted stack '{name}'. Branches are kept.");
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
