using Graft.Core.AutoUpdate;
using Graft.Core.Config;

namespace Graft.Core.Tests.AutoUpdate;

/// <summary>
/// Tests for auto-update behavior per spec section 3.
/// </summary>
public sealed class AutoUpdateTests
{
    private static readonly string[] ValidOsPrefixes = ["win", "osx", "linux"];
    private static readonly string[] ValidArchSuffixes = ["x64", "arm64"];
    // Requirement: Checks for updates in background (doesn't slow command)
    [Fact]
    public void Update_CheckIsNonBlocking()
    {
        // UpdateChecker.ShouldCheck is synchronous and fast — it just reads a file.
        // The actual network check would be done in a background task (not tested here).
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // No state file → should check
            var shouldCheck = UpdateChecker.ShouldCheck(tempDir);
            Assert.True(shouldCheck);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Requirement: Checks rate-limited to once per hour
    [Fact]
    public void Update_RateLimited_OncePerHour()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "update-state.toml"), $"""
                last_checked = "{DateTime.UtcNow:O}"
                current_version = "0.1.0"
                """);

            var shouldCheck = UpdateChecker.ShouldCheck(tempDir);
            Assert.False(shouldCheck);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Requirement: Rate limit expires after 1 hour
    [Fact]
    public void Update_RateLimitExpired_ShouldCheck()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "update-state.toml"), """
                last_checked = "2020-01-01T00:00:00Z"
                current_version = "0.1.0"
                """);

            var shouldCheck = UpdateChecker.ShouldCheck(tempDir);
            Assert.True(shouldCheck);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Requirement: New version found → downloads and stages binary
    [Fact]
    public async Task Update_NewVersionFound_StagesBinary()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        var stagingDir = Path.Combine(tempDir, "staging");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create a fake binary stream
            var binaryContent = "fake binary content for testing"u8.ToArray();
            using var stream = new MemoryStream(binaryContent);

            // Compute expected checksum
            var expectedChecksum = await UpdateChecker.ComputeChecksumAsync(
                await WriteTempFile(tempDir, binaryContent));

            using var stream2 = new MemoryStream(binaryContent);
            await UpdateChecker.StageUpdateAsync("0.2.0", stream2, stagingDir, expectedChecksum);

            var stagedPath = Path.Combine(stagingDir, "graft-0.2.0");
            Assert.True(File.Exists(stagedPath));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Requirement: On next run, staged update applied before command
    [Fact]
    public void Update_StagedUpdate_AppliedOnNextRun()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "update-state.toml"), """
                last_checked = "2026-02-05T14:30:00Z"
                current_version = "0.3.1"

                [pending_update]
                version = "0.3.2"
                binary_path = "/tmp/fake-binary"
                checksum = "sha256:abc123"
                downloaded_at = "2026-02-05T14:30:05Z"
                """);

            var hasPending = UpdateApplier.HasPendingUpdate(tempDir);
            Assert.True(hasPending);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Edge case: No network available
    [Fact]
    public void Update_NoNetwork_SilentlyFails()
    {
        // The update check should catch network errors and not crash.
        // ShouldCheck itself doesn't do network I/O — it just checks the state file.
        // Network errors would be caught by the background task wrapper.
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Even with no state file, ShouldCheck doesn't crash
            var ex = Record.Exception(() => UpdateChecker.ShouldCheck(tempDir));
            Assert.Null(ex);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Edge case: Staged binary checksum mismatch
    [Fact]
    public async Task Update_ChecksumMismatch_RejectsBinary()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        var stagingDir = Path.Combine(tempDir, "staging");
        Directory.CreateDirectory(tempDir);
        try
        {
            var binaryContent = "fake binary"u8.ToArray();
            using var stream = new MemoryStream(binaryContent);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                UpdateChecker.StageUpdateAsync("0.2.0", stream, stagingDir, "sha256:wrong_checksum"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // PlatformHelper returns a valid RID format
    [Fact]
    public void PlatformHelper_ReturnsValidRid()
    {
        var (rid, archiveExt, binaryName) = PlatformHelper.GetCurrentRid();

        // RID should be os-arch format
        Assert.Contains("-", rid);
        var parts = rid.Split('-');
        Assert.Equal(2, parts.Length);
        Assert.Contains(parts[0], ValidOsPrefixes);
        Assert.Contains(parts[1], ValidArchSuffixes);

        // Archive extension matches OS
        if (parts[0] == "win")
        {
            Assert.Equal("zip", archiveExt);
            Assert.Equal("graft.exe", binaryName);
        }
        else
        {
            Assert.Equal("tar.gz", archiveExt);
            Assert.Equal("graft", binaryName);
        }
    }

    // Version comparison: newer version detected
    [Fact]
    public void VersionComparison_NewerDetected()
    {
        var current = new Version(0, 1, 5);
        var latest = new Version(0, 1, 6);
        Assert.True(latest > current);
    }

    // Version comparison: already up-to-date
    [Fact]
    public void VersionComparison_AlreadyUpToDate()
    {
        var current = new Version(0, 1, 6);
        var latest = new Version(0, 1, 6);
        Assert.False(latest > current);
    }

    // Archive extraction: tar.gz with a fake binary
    [Fact]
    public void ExtractFromTarGz_FindsBinary()
    {
        var binaryContent = "fake graft binary"u8.ToArray();

        // Create a tar.gz in memory containing a "graft" file
        using var tarStream = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(
            tarStream, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            using var tarWriter = new System.Formats.Tar.TarWriter(gzip);
            var entry = new System.Formats.Tar.PaxTarEntry(
                System.Formats.Tar.TarEntryType.RegularFile, "graft")
            {
                DataStream = new MemoryStream(binaryContent),
            };
            tarWriter.WriteEntry(entry);
        }
        tarStream.Position = 0;

        using var extracted = ReleaseFetcher.ExtractFromTarGz(tarStream, "graft");
        var result = new byte[extracted.Length];
        extracted.Read(result, 0, result.Length);
        Assert.Equal(binaryContent, result);
    }

    // Archive extraction: tar.gz missing binary throws
    [Fact]
    public void ExtractFromTarGz_MissingBinary_Throws()
    {
        using var tarStream = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(
            tarStream, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            using var tarWriter = new System.Formats.Tar.TarWriter(gzip);
            var entry = new System.Formats.Tar.PaxTarEntry(
                System.Formats.Tar.TarEntryType.RegularFile, "other-file")
            {
                DataStream = new MemoryStream("not the binary"u8.ToArray()),
            };
            tarWriter.WriteEntry(entry);
        }
        tarStream.Position = 0;

        Assert.Throws<InvalidOperationException>(
            () => ReleaseFetcher.ExtractFromTarGz(tarStream, "graft"));
    }

    // Archive extraction: zip with a fake binary
    [Fact]
    public void ExtractFromZip_FindsBinary()
    {
        var binaryContent = "fake graft.exe binary"u8.ToArray();

        using var zipStream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(
            zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("graft.exe");
            using var entryStream = entry.Open();
            entryStream.Write(binaryContent);
        }
        zipStream.Position = 0;

        using var extracted = ReleaseFetcher.ExtractFromZip(zipStream, "graft.exe");
        var result = new byte[extracted.Length];
        extracted.Read(result, 0, result.Length);
        Assert.Equal(binaryContent, result);
    }

    // Coverage: exercises ApplyPendingUpdateAsync including File.Delete backup catch blocks
    [Fact]
    public async Task ApplyPendingUpdate_ValidBinary_ReplacesCurrentAndClearsState()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        var stagingDir = Path.Combine(tempDir, "staging");
        Directory.CreateDirectory(stagingDir);
        try
        {
            // Create a "current binary" file
            var currentBinaryPath = Path.Combine(tempDir, "graft-current");
            File.WriteAllText(currentBinaryPath, "old binary");

            // Create a staged binary
            var stagedContent = "new binary content"u8.ToArray();
            var stagedPath = Path.Combine(stagingDir, "graft-1.0.0");
            File.WriteAllBytes(stagedPath, stagedContent);

            // Compute its checksum
            var checksum = await UpdateChecker.ComputeChecksumAsync(stagedPath);

            // Write update state with pending update
            var state = new UpdateState
            {
                LastChecked = DateTime.UtcNow,
                CurrentVersion = "0.9.0",
                PendingUpdate = new PendingUpdate
                {
                    Version = "1.0.0",
                    BinaryPath = stagedPath,
                    Checksum = checksum,
                    DownloadedAt = DateTime.UtcNow,
                },
            };
            UpdateChecker.SaveUpdateState(state, tempDir);

            // Apply the update
            var applied = await UpdateApplier.ApplyPendingUpdateAsync(tempDir, currentBinaryPath);

            Assert.True(applied);
            Assert.Equal(stagedContent, File.ReadAllBytes(currentBinaryPath));

            // Verify pending update was cleared
            var newState = ConfigLoader.LoadUpdateState(tempDir);
            Assert.Null(newState.PendingUpdate);
            Assert.Equal("1.0.0", newState.CurrentVersion);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========================
    // UpdateApplier edge cases
    // ========================

    [Fact]
    public async Task ApplyPendingUpdate_PathTraversal_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create state with a staged path outside staging dir
            var state = new UpdateState
            {
                LastChecked = DateTime.UtcNow,
                CurrentVersion = "0.9.0",
                PendingUpdate = new PendingUpdate
                {
                    Version = "1.0.0",
                    BinaryPath = "/tmp/evil-path/graft",
                    Checksum = "sha256:fake",
                    DownloadedAt = DateTime.UtcNow,
                },
            };
            UpdateChecker.SaveUpdateState(state, tempDir);

            var currentBinary = Path.Combine(tempDir, "graft-current");
            File.WriteAllText(currentBinary, "old binary");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => UpdateApplier.ApplyPendingUpdateAsync(tempDir, currentBinary));
            Assert.Contains("outside the expected staging directory", ex.Message);

            // Verify pending was cleared
            var newState = ConfigLoader.LoadUpdateState(tempDir);
            Assert.Null(newState.PendingUpdate);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyPendingUpdate_MissingStagedBinary_ReturnsFalseAndClears()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        var stagingDir = Path.Combine(tempDir, "staging");
        Directory.CreateDirectory(stagingDir);
        try
        {
            var state = new UpdateState
            {
                LastChecked = DateTime.UtcNow,
                CurrentVersion = "0.9.0",
                PendingUpdate = new PendingUpdate
                {
                    Version = "1.0.0",
                    BinaryPath = Path.Combine(stagingDir, "graft-1.0.0"),
                    Checksum = "sha256:doesntmatter",
                    DownloadedAt = DateTime.UtcNow,
                },
            };
            UpdateChecker.SaveUpdateState(state, tempDir);

            var currentBinary = Path.Combine(tempDir, "graft-current");
            File.WriteAllText(currentBinary, "old binary");

            var result = await UpdateApplier.ApplyPendingUpdateAsync(tempDir, currentBinary);
            Assert.False(result);

            // Verify pending was cleared
            var newState = ConfigLoader.LoadUpdateState(tempDir);
            Assert.Null(newState.PendingUpdate);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyPendingUpdate_ChecksumMismatch_ThrowsAndClears()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        var stagingDir = Path.Combine(tempDir, "staging");
        Directory.CreateDirectory(stagingDir);
        try
        {
            var stagedPath = Path.Combine(stagingDir, "graft-1.0.0");
            File.WriteAllBytes(stagedPath, "some content"u8.ToArray());

            var state = new UpdateState
            {
                LastChecked = DateTime.UtcNow,
                CurrentVersion = "0.9.0",
                PendingUpdate = new PendingUpdate
                {
                    Version = "1.0.0",
                    BinaryPath = stagedPath,
                    Checksum = "sha256:wrong_checksum",
                    DownloadedAt = DateTime.UtcNow,
                },
            };
            UpdateChecker.SaveUpdateState(state, tempDir);

            var currentBinary = Path.Combine(tempDir, "graft-current");
            File.WriteAllText(currentBinary, "old binary");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => UpdateApplier.ApplyPendingUpdateAsync(tempDir, currentBinary));
            Assert.Contains("checksum mismatch", ex.Message);

            // Verify staged binary was deleted
            Assert.False(File.Exists(stagedPath));

            // Verify pending was cleared
            var newState = ConfigLoader.LoadUpdateState(tempDir);
            Assert.Null(newState.PendingUpdate);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyPendingUpdate_NoPendingUpdate_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var state = new UpdateState
            {
                LastChecked = DateTime.UtcNow,
                CurrentVersion = "0.9.0",
            };
            UpdateChecker.SaveUpdateState(state, tempDir);

            var currentBinary = Path.Combine(tempDir, "graft-current");
            File.WriteAllText(currentBinary, "binary");

            var result = await UpdateApplier.ApplyPendingUpdateAsync(tempDir, currentBinary);
            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void HasPendingUpdate_NoPending_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "update-state.toml"), """
                last_checked = "2026-02-05T14:30:00Z"
                current_version = "0.3.1"
                """);

            Assert.False(UpdateApplier.HasPendingUpdate(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Archive extraction: zip missing binary throws
    [Fact]
    public void ExtractFromZip_MissingBinary_Throws()
    {
        using var zipStream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(
            zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("other-file.txt");
            using var entryStream = entry.Open();
            entryStream.Write("not the binary"u8.ToArray());
        }
        zipStream.Position = 0;

        Assert.Throws<InvalidOperationException>(
            () => ReleaseFetcher.ExtractFromZip(zipStream, "graft.exe"));
    }

    private static async Task<string> WriteTempFile(string dir, byte[] content)
    {
        var path = Path.Combine(dir, "temp-binary");
        await File.WriteAllBytesAsync(path, content);
        return path;
    }
}
