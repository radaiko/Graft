using System.Net;
using Graft.Core.Worktree;
using Graft.Cli.Json;

namespace Graft.Cli.Server;

public static class WorktreeHandler
{
    public static async Task ListWorktrees(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        var worktrees = await WorktreeManager.ListAsync(repoPath, ct);
        await ApiServer.WriteJson(ctx, 200, worktrees);
    }

    public static async Task AddWorktree(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        try
        {
            var req = await ApiServer.ReadJson<AddWorktreeRequest>(ctx);
            if (req == null || string.IsNullOrWhiteSpace(req.Branch))
            {
                await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = "Missing required field: branch" });
                return;
            }

            await WorktreeManager.AddAsync(req.Branch, repoPath, req.CreateBranch, ct);
            var worktrees = await WorktreeManager.ListAsync(repoPath, ct);
            await ApiServer.WriteJson(ctx, 200, worktrees);
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task RemoveWorktree(HttpListenerContext ctx, string branch, string repoPath, bool force, CancellationToken ct)
    {
        try
        {
            await WorktreeManager.RemoveAsync(branch, repoPath, force, ct);
            var worktrees = await WorktreeManager.ListAsync(repoPath, ct);
            await ApiServer.WriteJson(ctx, 200, worktrees);
        }
        catch (InvalidOperationException ex)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }
}
