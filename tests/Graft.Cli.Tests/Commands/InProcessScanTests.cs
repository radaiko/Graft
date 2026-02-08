using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// In-process tests for scan commands.
/// Note: scan commands use CliPaths.GetConfigDir() which returns ~/.config/graft.
/// These tests exercise the command handler code paths.
/// </summary>
[Collection("InProcess")]
public sealed class InProcessScanTests : IDisposable
{
    private readonly string _tempDir;

    public InProcessScanTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"graft-scan-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ScanAdd_ValidDir_Succeeds()
    {
        var result = await InProcessCliRunner.RunAsync(null, "scan", "add", _tempDir);

        // scan add should succeed (it writes to real ~/.config/graft)
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task ScanList_Succeeds()
    {
        var result = await InProcessCliRunner.RunAsync(null, "scan", "list");

        // Should succeed (may show empty or existing paths)
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task ScanRemove_ValidDir_Succeeds()
    {
        // Add first, then remove
        await InProcessCliRunner.RunAsync(null, "scan", "add", _tempDir);
        var result = await InProcessCliRunner.RunAsync(null, "scan", "remove", _tempDir);

        Assert.Equal(0, result.ExitCode);
    }
}
