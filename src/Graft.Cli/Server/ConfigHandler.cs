using System.Net;
using Graft.Core.Config;
using Graft.Core.Worktree;
using Graft.Cli.Json;

namespace Graft.Cli.Server;

public static class ConfigHandler
{
    public static async Task GetConfig(HttpListenerContext ctx, string repoPath)
    {
        var config = ConfigLoader.LoadRepoConfig(repoPath);
        await ApiServer.WriteJson(ctx, 200, config);
    }

    public static async Task PutConfig(HttpListenerContext ctx, string repoPath)
    {
        try
        {
            var config = await ApiServer.ReadJson<GraftConfig>(ctx);
            if (config == null)
            {
                await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = "Invalid config body" });
                return;
            }

            ConfigLoader.SaveRepoConfig(config, repoPath);
            ctx.Response.StatusCode = 204;
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }

    public static async Task GetWorktreeConfig(HttpListenerContext ctx, string repoPath)
    {
        var config = ConfigLoader.LoadWorktreeConfig(repoPath);
        await ApiServer.WriteJson(ctx, 200, config);
    }

    public static async Task PutWorktreeConfig(HttpListenerContext ctx, string repoPath)
    {
        try
        {
            var config = await ApiServer.ReadJson<WorktreeConfig>(ctx);
            if (config == null)
            {
                await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = "Invalid worktree config body" });
                return;
            }

            ConfigLoader.SaveWorktreeConfig(config, repoPath);
            ctx.Response.StatusCode = 204;
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException)
        {
            await ApiServer.WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
    }
}
