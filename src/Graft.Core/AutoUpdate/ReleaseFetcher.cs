using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using Graft.Core.Config;

namespace Graft.Core.AutoUpdate;

public static class ReleaseFetcher
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "graft-cli" },
            { "Accept", "application/vnd.github+json" },
        },
        Timeout = TimeSpan.FromSeconds(30),
    };

    private const string ReleasesUrl = "https://api.github.com/repos/radaiko/Graft/releases";

    /// <summary>
    /// Fetches the latest non-draft cli/v* release from GitHub.
    /// Returns null if no suitable release is found.
    /// </summary>
    public static async Task<(Version Version, List<GitHubAsset> Assets)?> FetchLatestVersionAsync()
    {
        using var response = await Http.GetAsync(ReleasesUrl);
        response.EnsureSuccessStatusCode();

        var releases = await JsonSerializer.DeserializeAsync(
            await response.Content.ReadAsStreamAsync(),
            GitHubJsonContext.Default.ListGitHubRelease);

        if (releases is null || releases.Count == 0)
            return null;

        Version? bestVersion = null;
        List<GitHubAsset>? bestAssets = null;

        foreach (var release in releases)
        {
            if (release.Draft || release.Prerelease)
                continue;

            if (!release.TagName.StartsWith("cli/v", StringComparison.Ordinal))
                continue;

            var versionStr = release.TagName["cli/v".Length..];
            if (!Version.TryParse(versionStr, out var version))
                continue;

            if (bestVersion is null || version > bestVersion)
            {
                bestVersion = version;
                bestAssets = release.Assets;
            }
        }

        if (bestVersion is null || bestAssets is null)
            return null;

        return (bestVersion, bestAssets);
    }

    /// <summary>
    /// Checks for a newer version and stages it if found.
    /// Rate-limited to once per hour unless <paramref name="ignoreRateLimit"/> is true.
    /// </summary>
    public static async Task<bool> CheckAndStageUpdateAsync(
        string stateDir, Version currentVersion, bool ignoreRateLimit = false)
    {
        if (!ignoreRateLimit && !UpdateChecker.ShouldCheck(stateDir))
            return false;

        var result = await FetchLatestVersionAsync();

        if (result is null || result.Value.Version <= currentVersion)
        {
            // No newer version — just update last_checked
            var state = ConfigLoader.LoadUpdateState(stateDir);
            state.LastChecked = DateTime.UtcNow;
            UpdateChecker.SaveUpdateState(state, stateDir);
            return false;
        }

        var (latestVersion, assets) = result.Value;
        // Always use 3-component version (major.minor.build) to match release asset naming
        var versionStr = latestVersion.ToString(3);
        var (rid, archiveExt, binaryName) = PlatformHelper.GetCurrentRid();
        var expectedAssetName = $"graft-cli-v{versionStr}-{rid}.{archiveExt}";

        var asset = assets.Find(a =>
            string.Equals(a.Name, expectedAssetName, StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            // No asset for this platform — update last_checked to avoid re-hitting the API every run
            var noAssetState = ConfigLoader.LoadUpdateState(stateDir);
            noAssetState.LastChecked = DateTime.UtcNow;
            UpdateChecker.SaveUpdateState(noAssetState, stateDir);
            return false;
        }

        // Download the archive
        using var archiveStream = await Http.GetStreamAsync(asset.BrowserDownloadUrl);
        using var tempArchive = new MemoryStream();
        await archiveStream.CopyToAsync(tempArchive);
        tempArchive.Position = 0;

        // Extract the binary
        using var binaryStream = archiveExt == "zip"
            ? ExtractFromZip(tempArchive, binaryName)
            : ExtractFromTarGz(tempArchive, binaryName);

        // Compute checksum from the extracted binary by writing to a temp file first
        var stagingDir = Path.Combine(stateDir, "staging");
        Directory.CreateDirectory(stagingDir);
        var tempPath = Path.Combine(stagingDir, $"graft-{versionStr}.tmp.{Guid.NewGuid():N}");

        try
        {
            using (var fileStream = File.Create(tempPath))
            {
                await binaryStream.CopyToAsync(fileStream);
            }

            var checksum = await UpdateChecker.ComputeChecksumAsync(tempPath);

            // Stage the update using existing infrastructure
            using (var stagedStream = File.OpenRead(tempPath))
            {
                await UpdateChecker.StageUpdateAsync(versionStr, stagedStream, stagingDir, checksum);
            }
            return true;
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* Best-effort cleanup of temp file */ }
        }
    }

    public static MemoryStream ExtractFromTarGz(Stream archiveStream, string binaryName)
    {
        using var gzip = new GZipStream(archiveStream, CompressionMode.Decompress, leaveOpen: true);
        using var tar = new TarReader(gzip);

        while (tar.GetNextEntry() is { } entry)
        {
            var entryName = Path.GetFileName(entry.Name);
            if (string.Equals(entryName, binaryName, StringComparison.Ordinal)
                && entry.DataStream is not null)
            {
                var ms = new MemoryStream();
                entry.DataStream.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
        }

        throw new InvalidOperationException(
            $"Binary '{binaryName}' not found in tar.gz archive.");
    }

    public static MemoryStream ExtractFromZip(Stream archiveStream, string binaryName)
    {
        using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);

        foreach (var entry in zip.Entries)
        {
            var entryName = Path.GetFileName(entry.FullName);
            if (string.Equals(entryName, binaryName, StringComparison.OrdinalIgnoreCase))
            {
                var ms = new MemoryStream();
                using var entryStream = entry.Open();
                entryStream.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
        }

        throw new InvalidOperationException(
            $"Binary '{binaryName}' not found in zip archive.");
    }
}
