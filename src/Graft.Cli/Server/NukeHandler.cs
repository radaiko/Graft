using System.Net;
using Graft.Core.Nuke;
using Graft.Cli.Json;

namespace Graft.Cli.Server;

public static class NukeHandler
{
    public static async Task NukeAll(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        var req = await ApiServer.ReadJson<NukeRequest>(ctx);
        var force = req?.Force ?? false;
        var result = await NukeManager.NukeAllAsync(repoPath, force, ct);
        await ApiServer.WriteJson(ctx, 200, result);
    }

    public static async Task NukeWorktrees(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        var req = await ApiServer.ReadJson<NukeRequest>(ctx);
        var force = req?.Force ?? false;
        var result = await NukeManager.NukeWorktreesAsync(repoPath, force, ct);
        await ApiServer.WriteJson(ctx, 200, result);
    }

    public static async Task NukeStacks(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        var req = await ApiServer.ReadJson<NukeRequest>(ctx);
        var force = req?.Force ?? false;
        var result = await NukeManager.NukeStacksAsync(repoPath, force, ct);
        await ApiServer.WriteJson(ctx, 200, result);
    }

    public static async Task NukeBranches(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        var result = await NukeManager.NukeBranchesAsync(repoPath, ct);
        await ApiServer.WriteJson(ctx, 200, result);
    }
}
