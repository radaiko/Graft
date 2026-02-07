using System.Text;
using System.Text.RegularExpressions;
using Graft.Core.Config;
using Tomlyn;
using Tomlyn.Model;

namespace Graft.Core.AutoUpdate;

public static class UpdateChecker
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public static bool ShouldCheck(string stateDir)
    {
        var state = ConfigLoader.LoadUpdateState(stateDir);

        if (state.LastChecked == default)
            return true;

        return DateTime.UtcNow - state.LastChecked > CheckInterval;
    }

    private static readonly Regex SafeVersionPattern = new(@"^[a-zA-Z0-9][a-zA-Z0-9.\-]{0,63}$", RegexOptions.Compiled);

    public static async Task StageUpdateAsync(string version, Stream binaryStream, string stagingDir, string checksum)
    {
        if (!SafeVersionPattern.IsMatch(version))
            throw new ArgumentException(
                "Version string must be 1-64 alphanumeric characters, dots, or hyphens, and start with alphanumeric.", nameof(version));

        Directory.CreateDirectory(stagingDir);
        var binaryPath = Path.Combine(stagingDir, $"graft-{version}");

        try
        {
            using (var fileStream = File.Create(binaryPath))
            {
                await binaryStream.CopyToAsync(fileStream);
            }
        }
        catch
        {
            // Clean up partial file on download failure
            try { File.Delete(binaryPath); } catch { }
            throw;
        }

        // Verify checksum
        var actualChecksum = await ComputeChecksumAsync(binaryPath);
        if (actualChecksum != checksum)
        {
            File.Delete(binaryPath);
            throw new InvalidOperationException(
                $"Checksum mismatch: expected {checksum}, got {actualChecksum}");
        }

        // Write pending update to state
        var stateDir = Path.GetDirectoryName(stagingDir)!;
        var statePath = Path.Combine(stateDir, "update-state.toml");

        var state = ConfigLoader.LoadUpdateState(stateDir);
        state.LastChecked = DateTime.UtcNow;
        state.PendingUpdate = new PendingUpdate
        {
            Version = version,
            BinaryPath = binaryPath,
            Checksum = checksum,
            DownloadedAt = DateTime.UtcNow,
        };

        SaveUpdateState(state, stateDir);
    }

    public static async Task<string> ComputeChecksumAsync(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static void SaveUpdateState(UpdateState state, string stateDir)
    {
        Directory.CreateDirectory(stateDir);
        var statePath = Path.Combine(stateDir, "update-state.toml");

        var table = new TomlTable
        {
            ["last_checked"] = state.LastChecked.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["current_version"] = state.CurrentVersion,
        };

        if (state.PendingUpdate != null)
        {
            table["pending_update"] = new TomlTable
            {
                ["version"] = state.PendingUpdate.Version,
                ["binary_path"] = state.PendingUpdate.BinaryPath,
                ["checksum"] = state.PendingUpdate.Checksum,
                ["downloaded_at"] = state.PendingUpdate.DownloadedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            };
        }

        // Atomic write: uniquely-named temp file then rename
        var tempPath = $"{statePath}.tmp.{Guid.NewGuid():N}";
        File.WriteAllText(tempPath, Toml.FromModel(table), Encoding.UTF8);
        File.Move(tempPath, statePath, overwrite: true);
    }
}
