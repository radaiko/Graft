using System.Net;
using System.Text;
using Graft.Cli.Server;
using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Server;

/// <summary>
/// Integration tests for sync endpoints using a repo that needs merging.
/// </summary>
[Collection("Server")]
public sealed class ServerSyncIntegrationTests : IDisposable
{
    private readonly TempCliRepo _repo;
    private readonly ApiServer _server;
    private readonly HttpClient _client;

    public ServerSyncIntegrationTests()
    {
        _repo = TempCliRepo.CreateWithNeedsRebase();
        _server = new ApiServer(_repo.Path);
        _server.Start();
        _client = new HttpClient { BaseAddress = new Uri($"http://localhost:{_server.Port}") };
    }

    public void Dispose()
    {
        _client.Dispose();
        try { _server.Dispose(); } catch { /* HttpListener may throw during cleanup */ }
        _repo.Dispose();
    }

    [Fact]
    public async Task SyncStack_WithDirtyBranches_MergesSuccessfully()
    {
        var resp = await _client.PostAsync("/api/stacks/sync", Json("{}"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        // Should contain branch results
        Assert.Contains("branchResults", body);
    }

    [Fact]
    public async Task GetStack_WithNeedsMerge_ShowsFlag()
    {
        var resp = await _client.GetAsync("/api/stacks/test-stack");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        // At least one branch should need a merge
        Assert.Contains("needsMerge", body);
    }

    [Fact]
    public async Task CommitToStack_WithStagedChanges()
    {
        // Stage a file
        File.WriteAllText(Path.Combine(_repo.Path, "api-test.cs"), "// api test");
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _repo.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        process.StartInfo.ArgumentList.Add("add");
        process.StartInfo.ArgumentList.Add("api-test.cs");
        process.Start();
        process.WaitForExit();

        var resp = await _client.PostAsync("/api/stacks/commit",
            Json("""{"message":"API test commit"}"""));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("targetBranch", body);
    }

    private static StringContent Json(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
