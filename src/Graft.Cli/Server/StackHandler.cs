using System.Net;
using Graft.Core;
using Graft.Core.Commit;
using Graft.Core.Config;
using Graft.Core.Git;
using Graft.Core.Stack;
using Graft.Cli.Json;

namespace Graft.Cli.Server;

public static class StackHandler
{
    private const string BranchNameLabel = "Branch name";

    public static async Task ListStacks(HttpListenerContext ctx, string repoPath)
    {
        var stacks = ConfigLoader.ListStacks(repoPath);
        await ApiServer.WriteJson(ctx, 200, stacks);
    }

    public static async Task GetStack(HttpListenerContext ctx, string name, string repoPath, CancellationToken ct)
    {
        try
        {
            var stack = ConfigLoader.LoadStack(name, repoPath);
            var git = new GitRunner(repoPath, ct);

            var headResult = await git.RunAsync("rev-parse", "--abbrev-ref", "HEAD");
            var currentBranch = headResult.Success ? headResult.Stdout.Trim() : "";

            var detail = new StackDetailResponse
            {
                Name = stack.Name,
                Trunk = stack.Trunk,
                CurrentBranch = currentBranch,
            };

            // Check for in-progress sync conflict
            var opState = StackManager.LoadOperationState(repoPath);
            if (opState != null && opState.StackName == name)
                detail.HasConflict = true;

            // Check active stack
            var active = ConfigLoader.LoadActiveStack(repoPath);
            detail.IsActive = active == name;

            string parentBranch = stack.Trunk;
            foreach (var branch in stack.Branches)
            {
                var bd = new BranchDetail
                {
                    Name = branch.Name,
                    Pr = branch.Pr,
                    IsHead = branch.Name == currentBranch,
                };

                var branchCheck = await git.RunAsync("rev-parse", "--verify", $"refs/heads/{branch.Name}");
                if (branchCheck.Success)
                {
                    var countResult = await git.RunAsync("rev-list", "--count", $"{parentBranch}..{branch.Name}");
                    if (countResult.Success && int.TryParse(countResult.Stdout.Trim(), out var cc))
                        bd.CommitCount = cc;

                    var mergeBase = await git.RunAsync("merge-base", parentBranch, branch.Name);
                    var parentHead = await git.RunAsync("rev-parse", parentBranch);
                    if (mergeBase.Success && parentHead.Success &&
                        mergeBase.Stdout.Trim() != parentHead.Stdout.Trim())
                        bd.NeedsMerge = true;
                }

                detail.Branches.Add(bd);
                parentBranch = branch.Name;
            }

            await ApiServer.WriteJson(ctx, 200, detail);
        }
        catch (FileNotFoundException ex)
        {
            await ApiServer.WriteJson(ctx, 404, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task InitStack(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        try
        {
            var req = await ApiServer.ReadJson<InitStackRequest>(ctx);
            if (req == null || string.IsNullOrWhiteSpace(req.Name))
            {
                await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = "Missing required field: name" });
                return;
            }

            var stack = await StackManager.InitAsync(req.Name, repoPath, req.BaseBranch, ct);
            await GetStack(ctx, stack.Name, repoPath, ct);
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task DeleteStack(HttpListenerContext ctx, string name, string repoPath)
    {
        try
        {
            StackManager.Delete(name, repoPath);
            ctx.Response.StatusCode = 204;
        }
        catch (FileNotFoundException ex)
        {
            await ApiServer.WriteJson(ctx, 404, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task PushBranch(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        try
        {
            var req = await ApiServer.ReadJson<PushBranchRequest>(ctx);
            if (req == null || string.IsNullOrWhiteSpace(req.BranchName))
            {
                await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = "Missing required field: branchName" });
                return;
            }

            Validation.ValidateName(req.BranchName, BranchNameLabel);
            await StackManager.PushAsync(req.BranchName, repoPath, req.CreateBranch, ct);
            var stackName = ActiveStackManager.GetActiveStackName(repoPath);
            await GetStack(ctx, stackName, repoPath, ct);
        }
        catch (FileNotFoundException ex)
        {
            await ApiServer.WriteJson(ctx, 404, new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task SyncStack(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        try
        {
            var result = await StackManager.SyncAsync(repoPath, ct: ct);
            await ApiServer.WriteJson(ctx, 200, result);
        }
        catch (FileNotFoundException ex)
        {
            await ApiServer.WriteJson(ctx, 404, new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task CommitToStack(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        try
        {
            var req = await ApiServer.ReadJson<CommitRequest>(ctx);
            if (req == null || (!req.Amend && string.IsNullOrWhiteSpace(req.Message)))
            {
                await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = "Missing required field: message (or use amend)" });
                return;
            }

            if (req.Branch != null)
                Validation.ValidateName(req.Branch, BranchNameLabel);

            var options = new CommitOptions { Amend = req.Amend };
            var result = await CommitRouter.CommitAsync(req.Branch, req.Message, repoPath, options, ct);
            await ApiServer.WriteJson(ctx, 200, result);
        }
        catch (FileNotFoundException ex)
        {
            await ApiServer.WriteJson(ctx, 404, new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task ContinueSync(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        try
        {
            var result = await StackManager.ContinueSyncAsync(repoPath, ct);
            await ApiServer.WriteJson(ctx, 200, result);
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task AbortSync(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        try
        {
            await StackManager.AbortSyncAsync(repoPath, ct);
            ctx.Response.StatusCode = 204;
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task GetActiveStack(HttpListenerContext ctx, string repoPath)
    {
        var active = ConfigLoader.LoadActiveStack(repoPath);
        await ApiServer.WriteJson(ctx, 200, new ActiveStackResponse { Name = active });
    }

    public static async Task SetActiveStack(HttpListenerContext ctx, string repoPath)
    {
        try
        {
            var req = await ApiServer.ReadJson<SetActiveStackRequest>(ctx);
            if (req == null || string.IsNullOrWhiteSpace(req.Name))
            {
                await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = "Missing required field: name" });
                return;
            }

            ActiveStackManager.SetActiveStack(req.Name, repoPath);
            await ApiServer.WriteJson(ctx, 200, new ActiveStackResponse { Name = req.Name });
        }
        catch (FileNotFoundException ex)
        {
            await ApiServer.WriteJson(ctx, 404, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task PopBranch(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        try
        {
            var removed = await StackManager.PopAsync(repoPath, ct);
            await ApiServer.WriteJson(ctx, 200, new PopBranchResponse { RemovedBranch = removed });
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task DropBranch(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        try
        {
            var req = await ApiServer.ReadJson<DropBranchRequest>(ctx);
            if (req == null || string.IsNullOrWhiteSpace(req.BranchName))
            {
                await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = "Missing required field: branchName" });
                return;
            }

            Validation.ValidateName(req.BranchName, BranchNameLabel);
            await StackManager.DropAsync(req.BranchName, repoPath, ct);
            ctx.Response.StatusCode = 204;
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task ShiftBranch(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        try
        {
            var req = await ApiServer.ReadJson<ShiftBranchRequest>(ctx);
            if (req == null || string.IsNullOrWhiteSpace(req.BranchName))
            {
                await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = "Missing required field: branchName" });
                return;
            }

            Validation.ValidateName(req.BranchName, BranchNameLabel);
            await StackManager.ShiftAsync(req.BranchName, repoPath, ct);
            ctx.Response.StatusCode = 204;
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }
}
