using System.Runtime.InteropServices;
using Graft.Core.Config;

namespace Graft.Core.Scan;

public static class ScanPathManager
{
    private static readonly StringComparison PathComparison =
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static void Add(string directory, string configDir)
    {
        var resolved = NormalizePath(directory);

        if (!Directory.Exists(resolved))
            throw new DirectoryNotFoundException($"Directory '{resolved}' does not exist.");

        var paths = ConfigLoader.LoadScanPaths(configDir);

        if (paths.Any(p => string.Equals(NormalizePath(p.Path), resolved, PathComparison)))
            throw new InvalidOperationException($"Scan path '{resolved}' is already registered.");

        paths.Add(new ScanPath { Path = resolved });
        ConfigLoader.SaveScanPaths(paths, configDir);
    }

    public static void Remove(string directory, string configDir)
    {
        var resolved = NormalizePath(directory);

        var paths = ConfigLoader.LoadScanPaths(configDir);
        var removed = paths.RemoveAll(p => string.Equals(NormalizePath(p.Path), resolved, PathComparison));

        if (removed == 0)
            throw new InvalidOperationException($"Scan path '{resolved}' is not registered.");

        ConfigLoader.SaveScanPaths(paths, configDir);
    }

    public static List<ScanPath> List(string configDir)
    {
        return ConfigLoader.LoadScanPaths(configDir);
    }
}
