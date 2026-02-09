using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Graft.Core;
using Graft.Cli.Json;

namespace Graft.Cli.Server;

public sealed class ApiServer : IDisposable
{
    private const string StacksRoute = "stacks";
    private const string WorktreesRoute = "worktrees";

    private static readonly Assembly _assembly = typeof(ApiServer).Assembly;
    private static readonly string _resourcePrefix = FindResourcePrefix();

    private readonly HttpListener _listener = new();
    private readonly string _repoPath;
    private readonly SemaphoreSlim _gitLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public int Port { get; private set; }

    public ApiServer(string repoPath, int port = 0)
    {
        _repoPath = repoPath;
        Port = port == 0 ? FindFreePort() : port;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();

        // Retry port binding to handle TOCTOU race between FindFreePort() and Start()
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Start();
                _listenTask = ListenAsync(_cts.Token);
                return;
            }
            catch (HttpListenerException) when (attempt < 2)
            {
                Port = FindFreePort();
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _listener.Stop(); } catch (ObjectDisposedException) { /* Already disposed during shutdown */ }
        try { _listener.Close(); } catch (ObjectDisposedException) { /* Already disposed during shutdown */ }
        try { _listenTask?.Wait(TimeSpan.FromSeconds(2)); } catch { /* Shutdown best-effort — server is stopping */ }
        _cts?.Dispose();
        _gitLock.Dispose();
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }

            _ = Task.Run(() => HandleRequest(ctx, ct), ct);
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx, CancellationToken ct)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            var method = ctx.Request.HttpMethod;

            var origin = ctx.Request.Headers["Origin"];
            if (origin != null && IsLocalhostOrigin(origin))
            {
                ctx.Response.AddHeader("Access-Control-Allow-Origin", origin);
                ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

                if (method == "OPTIONS")
                {
                    ctx.Response.StatusCode = 204;
                    return;
                }
            }

            if (path.StartsWith("/api/"))
            {
                await HandleApiRequest(ctx, path, method, ct);
            }
            else
            {
                ServeStaticFile(ctx, path);
            }
        }
        catch (Exception ex)
        {
            try
            {
                await WriteJson(ctx, 500, new ErrorResponse { Error = ex.Message });
            }
            catch (Exception writeEx)
            {
                await Console.Error.WriteLineAsync($"Failed to write error response: {writeEx}");
            }
        }
        finally
        {
            try { ctx.Response.Close(); }
            catch (Exception closeEx)
            {
                await Console.Error.WriteLineAsync($"Failed to close HTTP response: {closeEx}");
            }
        }
    }

    private async Task HandleApiRequest(HttpListenerContext ctx, string path, string method, CancellationToken ct)
    {
        // Split path: "/api/stacks/foo" → ["api", "stacks", "foo"]
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        bool lockAcquired = false;
        try
        {
            await _gitLock.WaitAsync(ct);
            lockAcquired = true;

            switch (segments)
            {
                // Stack endpoints
                case ["api", StacksRoute] when method == "GET":
                    await StackHandler.ListStacks(ctx, _repoPath);
                    break;
                case ["api", StacksRoute] when method == "POST":
                    await StackHandler.InitStack(ctx, _repoPath, ct);
                    break;
                case ["api", StacksRoute, "active"] when method == "GET":
                    await StackHandler.GetActiveStack(ctx, _repoPath);
                    break;
                case ["api", StacksRoute, "active"] when method == "PUT":
                    await StackHandler.SetActiveStack(ctx, _repoPath);
                    break;
                case ["api", StacksRoute, var name] when method == "GET":
                    name = Uri.UnescapeDataString(name);
                    Validation.ValidateStackName(name);
                    await StackHandler.GetStack(ctx, name, _repoPath, ct);
                    break;
                case ["api", StacksRoute, var name] when method == "DELETE":
                    name = Uri.UnescapeDataString(name);
                    Validation.ValidateStackName(name);
                    await StackHandler.DeleteStack(ctx, name, _repoPath);
                    break;
                case ["api", StacksRoute, "push"] when method == "POST":
                    await StackHandler.PushBranch(ctx, _repoPath, ct);
                    break;
                case ["api", StacksRoute, "sync"] when method == "POST":
                    await StackHandler.SyncStack(ctx, _repoPath, ct);
                    break;
                case ["api", StacksRoute, "commit"] when method == "POST":
                    await StackHandler.CommitToStack(ctx, _repoPath, ct);
                    break;
                case ["api", StacksRoute, "pop"] when method == "POST":
                    await StackHandler.PopBranch(ctx, _repoPath, ct);
                    break;
                case ["api", StacksRoute, "drop"] when method == "POST":
                    await StackHandler.DropBranch(ctx, _repoPath, ct);
                    break;
                case ["api", StacksRoute, "shift"] when method == "POST":
                    await StackHandler.ShiftBranch(ctx, _repoPath, ct);
                    break;

                // Sync continue/abort (repo-level, not stack-specific)
                case ["api", "sync", "continue"] when method == "POST":
                    await StackHandler.ContinueSync(ctx, _repoPath, ct);
                    break;
                case ["api", "sync", "abort"] when method == "POST":
                    await StackHandler.AbortSync(ctx, _repoPath, ct);
                    break;

                // Nuke endpoints
                case ["api", "nuke"] when method == "POST":
                    await NukeHandler.NukeAll(ctx, _repoPath, ct);
                    break;
                case ["api", "nuke", WorktreesRoute] when method == "POST":
                    await NukeHandler.NukeWorktrees(ctx, _repoPath, ct);
                    break;
                case ["api", "nuke", StacksRoute] when method == "POST":
                    await NukeHandler.NukeStacks(ctx, _repoPath, ct);
                    break;
                case ["api", "nuke", "branches"] when method == "POST":
                    await NukeHandler.NukeBranches(ctx, _repoPath, ct);
                    break;

                // Worktree endpoints
                case ["api", WorktreesRoute] when method == "GET":
                    await WorktreeHandler.ListWorktrees(ctx, _repoPath, ct);
                    break;
                case ["api", WorktreesRoute] when method == "POST":
                    await WorktreeHandler.AddWorktree(ctx, _repoPath, ct);
                    break;
                // DELETE /api/worktrees/{branch...} — branch may contain slashes
                case ["api", WorktreesRoute, .. var rest] when method == "DELETE" && rest.Length > 0:
                {
                    var branch = Uri.UnescapeDataString(string.Join("/", rest));
                    Validation.ValidateName(branch, "Branch name");
                    var forceParam = ctx.Request.QueryString["force"];
                    var force = string.Equals(forceParam, "true", StringComparison.OrdinalIgnoreCase);
                    await WorktreeHandler.RemoveWorktree(ctx, branch, _repoPath, force, ct);
                    break;
                }

                // Git endpoints
                case ["api", "git", "status"] when method == "GET":
                    await GitHandler.GetStatus(ctx, _repoPath, ct);
                    break;
                case ["api", "git", "branches"] when method == "GET":
                    await GitHandler.GetBranches(ctx, _repoPath, ct);
                    break;

                default:
                    await WriteJson(ctx, 404, new ErrorResponse { Error = $"Not found: {method} {path}" });
                    break;
            }
        }
        catch (ArgumentException ex)
        {
            await WriteJson(ctx, 400, new ErrorResponse { Error = ex.Message });
        }
        finally
        {
            if (lockAcquired)
                _gitLock.Release();
        }
    }

    private static void ServeStaticFile(HttpListenerContext ctx, string path)
    {
        if (path == "/") path = "/index.html";

        // Map URL path to embedded resource name:
        // "/assets/index-Ca7GyzrY.css" → "{prefix}.assets.index-Ca7GyzrY.css"
        var resourceName = _resourcePrefix + path.Replace('/', '.');
        var stream = _assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            // SPA fallback: serve index.html for unmatched routes
            stream = _assembly.GetManifestResourceStream(_resourcePrefix + ".index.html");
            if (stream == null)
            {
                ctx.Response.StatusCode = 404;
                return;
            }
            path = "/index.html";
        }

        using (stream)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            ctx.Response.ContentType = ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".ico" => "image/x-icon",
                ".woff2" => "font/woff2",
                ".woff" => "font/woff",
                _ => "application/octet-stream",
            };

            stream.CopyTo(ctx.Response.OutputStream);
        }
    }

    private static string FindResourcePrefix()
    {
        foreach (var name in _assembly.GetManifestResourceNames())
        {
            var idx = name.IndexOf(".wwwroot.", StringComparison.Ordinal);
            if (idx >= 0)
                return name[..(idx + ".wwwroot".Length)];
        }
        return "graft.wwwroot";
    }

    // These generic overloads are AOT-safe because GraftJsonContext.Default.Options
    // contains the source-generated metadata for all serialized types.
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Options from GraftJsonContext source generator")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Options from GraftJsonContext source generator")]
    public static async Task WriteJson<T>(HttpListenerContext ctx, int statusCode, T value)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(ctx.Response.OutputStream, value, GraftJsonContext.Default.Options);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Options from GraftJsonContext source generator")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Options from GraftJsonContext source generator")]
    public static async Task<T?> ReadJson<T>(HttpListenerContext ctx) where T : class
    {
        var contentType = ctx.Request.ContentType;
        if (contentType == null || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Content-Type must be application/json");

        return await JsonSerializer.DeserializeAsync<T>(ctx.Request.InputStream, GraftJsonContext.Default.Options);
    }

    private static bool IsLocalhostOrigin(string origin)
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
               (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1");
    }

    private static int FindFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
