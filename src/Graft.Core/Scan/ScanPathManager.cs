using Graft.Core.Config;

namespace Graft.Core.Scan;

public static class ScanPathManager
{
    public static void Add(string directory, string configDir)
    {
        var resolved = Path.GetFullPath(directory);

        if (!Directory.Exists(resolved))
            throw new DirectoryNotFoundException($"Directory '{resolved}' does not exist.");

        var paths = ConfigLoader.LoadScanPaths(configDir);

        if (paths.Any(p => string.Equals(p.Path, resolved, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Scan path '{resolved}' is already registered.");

        paths.Add(new ScanPath { Path = resolved });
        ConfigLoader.SaveScanPaths(paths, configDir);
    }

    public static void Remove(string directory, string configDir)
    {
        var resolved = Path.GetFullPath(directory);

        var paths = ConfigLoader.LoadScanPaths(configDir);
        var removed = paths.RemoveAll(p => string.Equals(p.Path, resolved, StringComparison.Ordinal));

        if (removed == 0)
            throw new InvalidOperationException($"Scan path '{resolved}' is not registered.");

        ConfigLoader.SaveScanPaths(paths, configDir);
    }

    public static List<ScanPath> List(string configDir)
    {
        return ConfigLoader.LoadScanPaths(configDir);
    }
}
