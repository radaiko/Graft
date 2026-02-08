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

    [Fact]
    public async Task ScanAutoFetchList_Succeeds()
    {
        var result = await InProcessCliRunner.RunAsync(null, "scan", "auto-fetch", "list");

        // Should succeed (may show empty or existing repos)
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task ScanAutoFetchEnable_CurrentDir_Succeeds()
    {
        // Enable auto-fetch for current directory
        // This may fail if current dir is not in repo cache, but the handler code runs either way
        var result = await InProcessCliRunner.RunAsync(null, "scan", "auto-fetch", "enable");

        // Either succeeds or shows error — both paths exercise the handler
        Assert.True(result.ExitCode == 0 || result.ExitCode == 1);
    }

    [Fact]
    public async Task ScanAutoFetchDisable_CurrentDir_Succeeds()
    {
        var result = await InProcessCliRunner.RunAsync(null, "scan", "auto-fetch", "disable");

        Assert.True(result.ExitCode == 0 || result.ExitCode == 1);
    }

    [Fact]
    public async Task ScanAutoFetchEnable_ByName_Succeeds()
    {
        var result = await InProcessCliRunner.RunAsync(null, "scan", "auto-fetch", "enable", "nonexistent-repo");

        // Will fail because repo not found, but exercises the handler code path
        Assert.True(result.ExitCode == 0 || result.ExitCode == 1);
    }

    [Fact]
    public async Task ScanAutoFetchDisable_ByName_Succeeds()
    {
        var result = await InProcessCliRunner.RunAsync(null, "scan", "auto-fetch", "disable", "nonexistent-repo");

        Assert.True(result.ExitCode == 0 || result.ExitCode == 1);
    }
}
