using System.CommandLine;
using System.Diagnostics;
using Graft.Cli.Commands;
using Graft.Core.AutoUpdate;
using Graft.Core.Git;
using Graft.Core.Scan;
using Graft.Core.Stack;

var stateDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config", "graft");

await TryApplyPendingUpdateAsync(stateDir, args);
SpawnBackgroundTasks(stateDir, args);

var root = BuildRootCommand();
var exitCode = await root.Parse(args).InvokeAsync();
return exitCode != 0 ? exitCode : Environment.ExitCode;

static async Task TryApplyPendingUpdateAsync(string stateDir, string[] args)
{
    try
    {
        if (!UpdateApplier.HasPendingUpdate(stateDir))
            return;

        var binaryPath = Environment.ProcessPath;
        if (binaryPath is null)
            return;

        var applied = await UpdateApplier.ApplyPendingUpdateAsync(stateDir, binaryPath);
        if (!applied)
            return;

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
            Environment.Exit(proc.ExitCode);
        }
    }
    catch
    {
        // Update failed (corrupt state, apply error, etc.) — continue with current binary
    }
}

static void SpawnBackgroundTasks(string stateDir, string[] args)
{
    // Background update check (fire-and-forget)
    // Skip when running "update" — that command does its own check.
    if (args.Length == 0 || !string.Equals(args[0], "update", StringComparison.OrdinalIgnoreCase))
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var version = VersionCommand.GetCurrentVersion();
                await ReleaseFetcher.CheckAndStageUpdateAsync(stateDir, version);
            }
            catch
            {
                // Silently swallow — background check must never crash the CLI
            }
        });
    }

    // Background repo scan (fire-and-forget)
    _ = Task.Run(() =>
    {
        try
        {
            RepoScanner.ScanAndUpdateCache(stateDir);
        }
        catch
        {
            // Silently swallow — background scan must never crash the CLI
        }
    });

    // Background auto-fetch (fire-and-forget)
    _ = Task.Run(async () =>
    {
        try
        {
            await AutoFetcher.FetchDueReposAsync(stateDir);
        }
        catch
        {
            // Silently swallow — background fetch must never crash the CLI
        }
    });
}

static RootCommand BuildRootCommand()
{
    var root = new RootCommand("Graft — stacked branches and worktree management");

    root.Add(StackCommand.Create());
    root.Add(WorktreeCommand.Create());
    root.Add(NukeCommand.Create());
    root.Add(ScanCommand.Create());
    root.Add(CdCommand.Create());
    root.Add(StatusCommand.Create());
    root.Add(StatusCommand.CreateAlias());
    root.Add(InstallCommand.Create());
    root.Add(UninstallCommand.Create());
    root.Add(UpdateCommand.Create());
    root.Add(VersionCommand.Create());
    root.Add(UiCommand.Create());

    var continueOption = new Option<bool>("--continue") { Description = "Continue after resolving conflicts" };
    var abortOption = new Option<bool>("--abort") { Description = "Abort an in-progress operation" };
    root.Add(continueOption);
    root.Add(abortOption);

    root.SetAction(async (parseResult, ct) =>
    {
        var doContinue = parseResult.GetValue(continueOption);
        var doAbort = parseResult.GetValue(abortOption);

        if (doContinue && doAbort)
        {
            await Console.Error.WriteLineAsync("Error: --continue and --abort cannot be used together.");
            Environment.ExitCode = 1;
            return;
        }

        if (doContinue) { await HandleContinueAsync(ct); return; }
        if (doAbort) { await HandleAbortAsync(ct); return; }
        ShowHelp(root);
    });

    return root;
}

static async Task HandleContinueAsync(CancellationToken ct)
{
    var repoPath = Directory.GetCurrentDirectory();

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
                    PrintConflictDetails(br);
                    Environment.ExitCode = 1;
                    return;
                }
                Console.WriteLine($"  \u2713 {br.Name} (merged)");
            }
            Console.WriteLine("Done.");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
        return;
    }

    // Fall back to raw git merge --continue
    await ContinueGitMergeAsync(repoPath, ct);
}

static async Task ContinueGitMergeAsync(string repoPath, CancellationToken ct)
{
    var git = new GitRunner(repoPath, ct);
    var gitDir = GitRunner.ResolveGitDir(repoPath);
    var mergeHead = Path.Combine(gitDir, "MERGE_HEAD");

    if (!File.Exists(mergeHead))
    {
        await Console.Error.WriteLineAsync("Error: No operation in progress to continue.");
        await Console.Error.WriteLineAsync("There is no merge in progress. Nothing to continue.");
        Environment.ExitCode = 1;
        return;
    }

    var result = await git.RunAsync("merge", "--continue");
    if (result.Success)
    {
        Console.WriteLine("Merge continued successfully.");
    }
    else
    {
        await Console.Error.WriteLineAsync($"Error: {result.Stderr}");
        await Console.Error.WriteLineAsync("Fix remaining conflicts, stage with 'git add', then run 'graft --continue' again.");
        Environment.ExitCode = 1;
    }
}

static void PrintConflictDetails(BranchSyncResult br)
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
}

static async Task HandleAbortAsync(CancellationToken ct)
{
    var repoPath = Directory.GetCurrentDirectory();

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
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
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
            await Console.Error.WriteLineAsync($"Error: {result.Stderr}");
            Environment.ExitCode = 1;
        }
    }
    else
    {
        await Console.Error.WriteLineAsync("Error: No operation in progress to abort.");
        Environment.ExitCode = 1;
    }
}

static void ShowHelp(RootCommand root)
{
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
}
