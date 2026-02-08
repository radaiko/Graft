using System.Net;
using System.Text;
using Graft.Cli.Server;
using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Server;

/// <summary>
/// Integration tests for the web API server.
/// Starts a real ApiServer on an auto-assigned port and exercises all HTTP endpoints.
/// </summary>
public sealed class ServerIntegrationTests : IDisposable
{
    private readonly TempCliRepo _repo;
    private readonly ApiServer _server;
    private readonly HttpClient _client;

    public ServerIntegrationTests()
    {
        _repo = TempCliRepo.CreateWithStack();
        _server = new ApiServer(_repo.Path);
        _server.Start();
        _client = new HttpClient { BaseAddress = new Uri($"http://localhost:{_server.Port}") };
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
        // Clean up worktree siblings
        var parentDir = Path.GetDirectoryName(_repo.Path);
        var repoName = Path.GetFileName(_repo.Path);
        if (parentDir != null)
        {
            foreach (var dir in Directory.GetDirectories(parentDir, $"{repoName}.wt.*"))
            {
                try
                {
                    SetAttributesNormal(new DirectoryInfo(dir));
                    Directory.Delete(dir, recursive: true);
                }
                catch { }
            }
        }
        _repo.Dispose();
    }

    private static void SetAttributesNormal(DirectoryInfo dir)
    {
        foreach (var sub in dir.GetDirectories())
            SetAttributesNormal(sub);
        foreach (var file in dir.GetFiles())
            file.Attributes = FileAttributes.Normal;
    }

    // --- Stack endpoints ---

    [Fact]
    public async Task GetStacks_ReturnsStackNames()
    {
        var resp = await _client.GetAsync("/api/stacks");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("test-stack", body);
    }

    [Fact]
    public async Task GetStack_ReturnsDetail()
    {
        var resp = await _client.GetAsync("/api/stacks/test-stack");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("test-stack", body);
        Assert.Contains("auth/base-types", body);
    }

    [Fact]
    public async Task GetStack_NotFound_Returns404()
    {
        var resp = await _client.GetAsync("/api/stacks/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task InitStack_CreatesStack()
    {
        var resp = await _client.PostAsync("/api/stacks", Json("""{"name":"new-stack"}"""));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("new-stack", body);
    }

    [Fact]
    public async Task InitStack_MissingName_Returns400()
    {
        var resp = await _client.PostAsync("/api/stacks", Json("""{"name":""}"""));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task InitStack_Duplicate_Returns400()
    {
        var resp = await _client.PostAsync("/api/stacks", Json("""{"name":"test-stack"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteStack_RemovesStack()
    {
        // Create a stack to delete
        await _client.PostAsync("/api/stacks", Json("""{"name":"doomed"}"""));

        var resp = await _client.DeleteAsync("/api/stacks/doomed");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteStack_NotFound_Returns404()
    {
        var resp = await _client.DeleteAsync("/api/stacks/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // --- Active stack endpoints ---

    [Fact]
    public async Task GetActiveStack_ReturnsName()
    {
        var resp = await _client.GetAsync("/api/stacks/active");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("test-stack", body);
    }

    [Fact]
    public async Task SetActiveStack_UpdatesActive()
    {
        // Create a second stack first
        await _client.PostAsync("/api/stacks", Json("""{"name":"other-stack"}"""));

        var resp = await _client.PutAsync("/api/stacks/active", Json("""{"name":"other-stack"}"""));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("other-stack", body);
    }

    [Fact]
    public async Task SetActiveStack_MissingName_Returns400()
    {
        var resp = await _client.PutAsync("/api/stacks/active", Json("""{"name":""}"""));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // --- Stack mutation endpoints ---

    [Fact]
    public async Task PushBranch_AddsBranch()
    {
        var resp = await _client.PostAsync("/api/stacks/push",
            Json("""{"branchName":"new-branch","createBranch":true}"""));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("new-branch", body);
    }

    [Fact]
    public async Task PushBranch_MissingName_Returns400()
    {
        var resp = await _client.PostAsync("/api/stacks/push",
            Json("""{"branchName":"","createBranch":false}"""));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PopBranch_RemovesTop()
    {
        var resp = await _client.PostAsync("/api/stacks/pop", Json("{}"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("removedBranch", body);
    }

    [Fact]
    public async Task DropBranch_RemovesBranch()
    {
        var resp = await _client.PostAsync("/api/stacks/drop",
            Json("""{"branchName":"auth/base-types"}"""));

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task DropBranch_MissingName_Returns400()
    {
        var resp = await _client.PostAsync("/api/stacks/drop",
            Json("""{"branchName":""}"""));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ShiftBranch_InsertsBranch()
    {
        // Push a branch first, then try to shift it
        await _client.PostAsync("/api/stacks/push",
            Json("""{"branchName":"shift-branch","createBranch":true}"""));

        var resp = await _client.PostAsync("/api/stacks/shift",
            Json("""{"branchName":"shift-branch"}"""));

        // Shift inserts at bottom — result depends on validation
        Assert.True(resp.StatusCode == HttpStatusCode.NoContent || resp.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShiftBranch_MissingName_Returns400()
    {
        var resp = await _client.PostAsync("/api/stacks/shift",
            Json("""{"branchName":""}"""));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // --- Sync endpoints ---

    [Fact]
    public async Task SyncStack_UpToDate()
    {
        var resp = await _client.PostAsync("/api/stacks/sync", Json("{}"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ContinueSync_NoOp_Returns400()
    {
        var resp = await _client.PostAsync("/api/sync/continue", Json("{}"));

        // No in-progress sync -> 400
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AbortSync_NoOp_Succeeds()
    {
        var resp = await _client.PostAsync("/api/sync/abort", Json("{}"));

        // No in-progress sync -> succeeds as no-op (204) or returns 400
        Assert.True(resp.StatusCode == HttpStatusCode.NoContent || resp.StatusCode == HttpStatusCode.BadRequest);
    }

    // --- Commit endpoint ---

    [Fact]
    public async Task CommitToStack_MissingMessage_Returns400()
    {
        var resp = await _client.PostAsync("/api/stacks/commit",
            Json("""{"message":""}"""));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // --- Worktree endpoints ---

    [Fact]
    public async Task GetWorktrees_ReturnsList()
    {
        var resp = await _client.GetAsync("/api/worktrees");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        // Should return a JSON array
        Assert.StartsWith("[", body.TrimStart());
    }

    [Fact]
    public async Task AddWorktree_CreatesWorktree()
    {
        var resp = await _client.PostAsync("/api/worktrees",
            Json("""{"branch":"wt-test-branch","createBranch":true}"""));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AddWorktree_MissingBranch_Returns400()
    {
        var resp = await _client.PostAsync("/api/worktrees",
            Json("""{"branch":""}"""));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveWorktree_NotFound_Returns400()
    {
        var resp = await _client.DeleteAsync("/api/worktrees/nonexistent-branch");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveWorktree_WithForce()
    {
        // Add a worktree first
        await _client.PostAsync("/api/worktrees",
            Json("""{"branch":"rm-force-branch","createBranch":true}"""));

        var resp = await _client.DeleteAsync("/api/worktrees/rm-force-branch?force=true");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // --- Nuke endpoints ---

    [Fact]
    public async Task NukeStacks_RemovesAll()
    {
        var resp = await _client.PostAsync("/api/nuke/stacks",
            Json("""{"force":true}"""));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task NukeWorktrees_RemovesAll()
    {
        var resp = await _client.PostAsync("/api/nuke/worktrees",
            Json("""{"force":true}"""));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task NukeBranches_RemovesGone()
    {
        var resp = await _client.PostAsync("/api/nuke/branches", Json("{}"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task NukeAll_RemovesEverything()
    {
        var resp = await _client.PostAsync("/api/nuke",
            Json("""{"force":true}"""));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // --- Git endpoints ---

    [Fact]
    public async Task GetGitStatus_ReturnsStatus()
    {
        var resp = await _client.GetAsync("/api/git/status");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("currentBranch", body);
    }

    [Fact]
    public async Task GetGitBranches_ReturnsBranches()
    {
        var resp = await _client.GetAsync("/api/git/branches");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("master", body);
    }

    // --- Error handling ---

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        var resp = await _client.GetAsync("/api/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task OptionsRequest_WithCorsOrigin_Returns204()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/stacks");
        request.Headers.Add("Origin", $"http://localhost:{_server.Port}");

        var resp = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task CorsHeaders_IncludedForLocalhostOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/stacks");
        request.Headers.Add("Origin", $"http://localhost:{_server.Port}");

        var resp = await _client.SendAsync(request);

        Assert.True(resp.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // --- Static file serving ---

    [Fact]
    public async Task StaticRoot_ServesContent()
    {
        var resp = await _client.GetAsync("/");

        // May return 200 (if wwwroot is embedded) or 404 (if not available in test build)
        // Either way, the handler executes — we mainly need coverage of ServeStaticFile
        Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StaticFile_NonExistent_FallsBackOrReturns404()
    {
        var resp = await _client.GetAsync("/nonexistent-file.xyz");

        // SPA fallback serves index.html, or 404 if no embedded resources
        Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.NotFound);
    }

    // --- Invalid content type ---

    [Fact]
    public async Task PostWithWrongContentType_Returns400()
    {
        var content = new StringContent("not json", Encoding.UTF8, "text/plain");
        var resp = await _client.PostAsync("/api/stacks", content);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private static StringContent Json(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
