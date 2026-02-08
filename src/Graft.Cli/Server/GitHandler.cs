using System.Net;
using Graft.Core.Git;
using Graft.Cli.Json;

namespace Graft.Cli.Server;

public static class GitHandler
{
    public static async Task GetStatus(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        var git = new GitRunner(repoPath, ct);

        var headResult = await git.RunAsync("rev-parse", "--abbrev-ref", "HEAD");
        var currentBranch = headResult.Success ? headResult.Stdout.Trim() : "(detached)";

        var statusResult = await git.RunAsync("status", "--porcelain");
        var response = new GitStatusResponse { CurrentBranch = currentBranch };

        if (statusResult.Success && !string.IsNullOrWhiteSpace(statusResult.Stdout))
        {
            ParsePorcelainStatus(statusResult.Stdout, response);
        }

        await ApiServer.WriteJson(ctx, 200, response);
    }

    private static void ParsePorcelainStatus(string porcelainOutput, GitStatusResponse response)
    {
        foreach (var line in porcelainOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 3) continue;
            var indexStatus = line[0];
            var workTreeStatus = line[1];
            var file = line[3..].Trim();

            if (indexStatus != ' ' && indexStatus != '?')
                response.StagedFiles.Add(file);

            if (workTreeStatus == 'M' || workTreeStatus == 'D')
                response.ModifiedFiles.Add(file);

            if (indexStatus == '?' && workTreeStatus == '?')
                response.UntrackedFiles.Add(file);
        }
    }

    public static async Task GetBranches(HttpListenerContext ctx, string repoPath, CancellationToken ct)
    {
        var git = new GitRunner(repoPath, ct);
        var result = await git.RunAsync("branch", "--list", "--format=%(refname:short)");

        var branches = Array.Empty<string>();
        if (result.Success && !string.IsNullOrWhiteSpace(result.Stdout))
        {
            branches = result.Stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .OrderBy(b => b)
                .ToArray();
        }

        await ApiServer.WriteJson(ctx, 200, branches);
    }
}
