using System.Runtime.InteropServices;
using Graft.Core.Config;

namespace Graft.Core.AutoUpdate;

public static class UpdateApplier
{
    public static bool HasPendingUpdate(string stateDir)
    {
        var state = ConfigLoader.LoadUpdateState(stateDir);
        return state.PendingUpdate != null;
    }

    public static async Task<bool> ApplyPendingUpdateAsync(string stateDir, string currentBinaryPath)
    {
        var state = ConfigLoader.LoadUpdateState(stateDir);
        if (state.PendingUpdate == null)
            return false;

        var stagedPath = state.PendingUpdate.BinaryPath;

        // Validate that staged binary path is within the expected staging directory
        var expectedStagingDir = Path.GetFullPath(Path.Combine(stateDir, "staging"));
        var resolvedStagedPath = Path.GetFullPath(stagedPath);
        var pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        if (!resolvedStagedPath.StartsWith(expectedStagingDir + Path.DirectorySeparatorChar, pathComparison)
            && !string.Equals(resolvedStagedPath, expectedStagingDir, pathComparison))
        {
            state.PendingUpdate = null;
            UpdateChecker.SaveUpdateState(state, stateDir);
            throw new InvalidOperationException(
                $"Staged binary path '{stagedPath}' is outside the expected staging directory. Update rejected.");
        }

        if (!File.Exists(stagedPath))
        {
            // Staged binary missing, clear pending
            state.PendingUpdate = null;
            UpdateChecker.SaveUpdateState(state, stateDir);
            return false;
        }

        // Verify checksum before applying
        var actualChecksum = await UpdateChecker.ComputeChecksumAsync(stagedPath);
        if (actualChecksum != state.PendingUpdate.Checksum)
        {
            // Checksum mismatch, reject
            File.Delete(stagedPath);
            state.PendingUpdate = null;
            UpdateChecker.SaveUpdateState(state, stateDir);
            throw new InvalidOperationException("Staged update checksum mismatch, update rejected");
        }

        // Replace binary: rename current to backup, staged to current
        var backupPath = currentBinaryPath + ".bak";

        try
        {
            // Delete backup if it exists (use try-catch instead of check-then-delete to avoid TOCTOU)
            try { File.Delete(backupPath); } catch (FileNotFoundException) { }

            File.Move(currentBinaryPath, backupPath);
            File.Move(stagedPath, currentBinaryPath);

            // Set executable permissions on Unix
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(currentBinaryPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            // Clear pending update
            state.CurrentVersion = state.PendingUpdate.Version;
            state.PendingUpdate = null;
            UpdateChecker.SaveUpdateState(state, stateDir);

            // Clean up backup
            try { File.Delete(backupPath); } catch (FileNotFoundException) { }

            return true;
        }
        catch
        {
            // Rollback on failure: restore from backup if the current binary is missing
            try
            {
                if (!File.Exists(currentBinaryPath) && File.Exists(backupPath))
                    File.Move(backupPath, currentBinaryPath);
            }
            catch (Exception rollbackEx)
            {
                throw new InvalidOperationException(
                    $"Update failed and rollback also failed. Backup is at '{backupPath}'. " +
                    $"Manually rename it to '{currentBinaryPath}' to recover. Rollback error: {rollbackEx.Message}");
            }
            throw;
        }
    }
}
