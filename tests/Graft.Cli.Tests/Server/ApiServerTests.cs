using Graft.Cli.Server;

namespace Graft.Cli.Tests.Server;

public sealed class ApiServerTests
{
    [Fact]
    public void ApiServer_StartAndDispose_DoesNotThrow()
    {
        // Exercises Dispose() path including the try/catch blocks for listener cleanup.
        using var server = new ApiServer(Path.GetTempPath());
        server.Start();
    }
}
