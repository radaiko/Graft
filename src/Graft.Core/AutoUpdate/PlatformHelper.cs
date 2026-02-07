using System.Runtime.InteropServices;

namespace Graft.Core.AutoUpdate;

public static class PlatformHelper
{
    public static (string Rid, string ArchiveExt, string BinaryName) GetCurrentRid()
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : throw new PlatformNotSupportedException(
                $"Unsupported OS. Auto-update supports Windows, macOS, and Linux.");

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported architecture: {RuntimeInformation.ProcessArchitecture}. Auto-update supports x64 and arm64."),
        };

        var archiveExt = os == "win" ? "zip" : "tar.gz";
        var binaryName = os == "win" ? "graft.exe" : "graft";

        return ($"{os}-{arch}", archiveExt, binaryName);
    }
}
