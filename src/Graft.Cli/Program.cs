using System.CommandLine;
using System.Diagnostics;
using Graft.Cli.Commands;
using Graft.Core.AutoUpdate;
using Graft.Core.Git;
using Graft.Core.Stack;

var stateDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config", "graft");

// 1. Apply pending update (blocking) — re-exec with new binary
try
{
    if (UpdateApplier.HasPendingUpdate(stateDir))
    {
        var binaryPath = Environment.ProcessPath;
        if (binaryPath is not null)
        {
            var applied = await UpdateApplier.ApplyPendingUpdateAsync(stateDir, binaryPath);
            if (applied)
            {
                // Re-exec with the updated binary
                var psi = new ProcessStartInfo(binaryPath)
                {
                    UseShellExecute = false,
                };
                foreach (var arg in args)
                    psi.ArgumentList.Add(arg);

                using var proc = Process.Start(psi);
                if (proc is not null)
                {
                    await proc.WaitForExitAsync();
                    return proc.ExitCode;
                }
            }
        }
    }
}
catch
{
    // Update failed (corrupt state, apply error, etc.) — continue with current binary
}

// 2. Spawn background update check (fire-and-forget)
//    Skip when running "update" — that command does its own check.
if (args.Length == 0 || !string.Equals(args[0], "update", StringComparison.OrdinalIgnoreCase))
{
    _ = Task.Run(async () =>
    {
        try
        {
            var version = Graft.Cli.Commands.VersionCommand.GetCurrentVersion();
            await ReleaseFetcher.CheckAndStageUpdateAsync(stateDir, version);
        }
        catch
        {
            // Silently swallow — background check must never crash the CLI
        }
    });
}

var root = new RootCommand("Graft — stacked branches and worktree management");

// Stack commands (grouped)
root.Add(StackCommand.Create());

// Worktree commands
root.Add(WorktreeCommand.Create());

// Nuke command
root.Add(NukeCommand.Create());

// Setup commands
root.Add(InstallCommand.Create());
root.Add(UninstallCommand.Create());
root.Add(UpdateCommand.Create());
root.Add(VersionCommand.Create());

// UI command
root.Add(UiCommand.Create());

// Global options: --continue / --abort
var continueOption = new Option<bool>("--continue") { Description = "Continue after resolving conflicts" };
var abortOption = new Option<bool>("--abort") { Description = "Abort an in-progress operation" };
root.Add(continueOption);
root.Add(abortOption);

root.SetAction(async (parseResult, ct) =>
{
    var doContinue = parseResult.GetValue(continueOption);
    var doAbort = parseResult.GetValue(abortOption);

    // Mutual exclusion check
    if (doContinue && doAbort)
    {
        Console.Error.WriteLine("Error: --continue and --abort cannot be used together.");
        Environment.ExitCode = 1;
        return;
    }

    if (doContinue)
    {
        var repoPath = Directory.GetCurrentDirectory();

        // Check if we have a graft operation state (cascade sync)
        var opState = StackManager.LoadOperationState(repoPath);
        if (opState != null)
        {
            try
            {
                var result = await StackManager.ContinueSyncAsync(repoPath, ct);
                foreach (var br in result.BranchResults)
                {
                    if (br.Status == SyncStatus.Conflict)
                    {
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
                    Console.WriteLine($"  \u2713 {br.Name} (merged)");
                }
                Console.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        // Fall back to raw git merge --continue
        var git = new GitRunner(repoPath, ct);
        var gitDir = GitRunner.ResolveGitDir(repoPath);
        var mergeHead = Path.Combine(gitDir, "MERGE_HEAD");

        if (File.Exists(mergeHead))
        {
            var result = await git.RunAsync("merge", "--continue");
            if (result.Success)
            {
                Console.WriteLine("Merge continued successfully.");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Stderr}");
                Console.Error.WriteLine("Fix remaining conflicts, stage with 'git add', then run 'graft --continue' again.");
                Environment.ExitCode = 1;
            }
        }
        else
        {
            Console.Error.WriteLine("Error: No operation in progress to continue.");
            Console.Error.WriteLine("There is no merge in progress. Nothing to continue.");
            Environment.ExitCode = 1;
        }
        return;
    }

    if (doAbort)
    {
        var repoPath = Directory.GetCurrentDirectory();

        // Check if we have a graft operation state
        var opState = StackManager.LoadOperationState(repoPath);
        if (opState != null)
        {
            try
            {
                await StackManager.AbortSyncAsync(repoPath, ct);
                Console.WriteLine("Sync aborted.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        var git = new GitRunner(repoPath, ct);
        var gitDir = GitRunner.ResolveGitDir(repoPath);
        var mergeHead = Path.Combine(gitDir, "MERGE_HEAD");

        if (File.Exists(mergeHead))
        {
            var result = await git.RunAsync("merge", "--abort");
            if (result.Success)
            {
                Console.WriteLine("Merge aborted.");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Stderr}");
                Environment.ExitCode = 1;
            }
        }
        else
        {
            Console.Error.WriteLine("Error: No operation in progress to abort.");
            Environment.ExitCode = 1;
        }
        return;
    }

    // No --continue or --abort: show help
    Console.WriteLine(root.Description);
    Console.WriteLine();
    Console.WriteLine("Usage: graft [command] [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    foreach (var cmd in root.Subcommands)
    {
        Console.WriteLine($"  {cmd.Name,-16} {cmd.Description}");
    }
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --continue     Continue after resolving conflicts");
    Console.WriteLine("  --abort        Abort an in-progress operation");
    Console.WriteLine();
    Console.WriteLine("Run 'graft [command] --help' for more information about a command.");
});

var exitCode = await root.Parse(args).InvokeAsync();
return exitCode != 0 ? exitCode : Environment.ExitCode;
