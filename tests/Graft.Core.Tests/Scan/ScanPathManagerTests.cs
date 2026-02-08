using Graft.Core.Scan;

namespace Graft.Core.Tests.Scan;

public sealed class ScanPathManagerTests : IDisposable
{
    private readonly string _configDir;
    private readonly string _testDir;

    public ScanPathManagerTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), $"graft-config-test-{Guid.NewGuid():N}");
        _testDir = Path.Combine(Path.GetTempPath(), $"graft-scan-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configDir);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDir))
            Directory.Delete(_configDir, recursive: true);
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void Add_ValidDirectory_AddsSuccessfully()
    {
        ScanPathManager.Add(_testDir, _configDir);

        var paths = ScanPathManager.List(_configDir);
        Assert.Single(paths);
        Assert.Equal(Path.GetFullPath(_testDir), paths[0].Path);
    }

    [Fact]
    public void Add_NonexistentDirectory_Throws()
    {
        var ex = Assert.Throws<DirectoryNotFoundException>(() =>
            ScanPathManager.Add("/nonexistent/path/12345", _configDir));
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void Add_Duplicate_Throws()
    {
        ScanPathManager.Add(_testDir, _configDir);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ScanPathManager.Add(_testDir, _configDir));
        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public void Remove_ExistingPath_RemovesSuccessfully()
    {
        ScanPathManager.Add(_testDir, _configDir);
        ScanPathManager.Remove(_testDir, _configDir);

        var paths = ScanPathManager.List(_configDir);
        Assert.Empty(paths);
    }

    [Fact]
    public void Remove_NonexistentPath_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ScanPathManager.Remove("/nonexistent/whatever", _configDir));
        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public void List_NoPaths_ReturnsEmpty()
    {
        var paths = ScanPathManager.List(_configDir);
        Assert.Empty(paths);
    }

    [Fact]
    public void Add_MultiplePaths_AllPresent()
    {
        var dir2 = Path.Combine(Path.GetTempPath(), $"graft-scan-dir2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir2);

        try
        {
            ScanPathManager.Add(_testDir, _configDir);
            ScanPathManager.Add(dir2, _configDir);

            var paths = ScanPathManager.List(_configDir);
            Assert.Equal(2, paths.Count);
        }
        finally
        {
            if (Directory.Exists(dir2))
                Directory.Delete(dir2, recursive: true);
        }
    }
}
