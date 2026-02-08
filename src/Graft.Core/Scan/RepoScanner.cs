using System.Runtime.InteropServices;
using Graft.Core.Config;

namespace Graft.Core.Scan;

public static class RepoScanner
{
    private static readonly StringComparer PathComparer =
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "vendor", ".cache", "bin", "obj",
        "__pycache__", ".tox", ".venv", "venv",
    };

    /// <summary>
    /// Scans a directory for git repos up to maxDepth levels deep.
    /// Returns discovered repos as CachedRepo entries.
    /// </summary>
    public static List<CachedRepo> ScanDirectory(string directory, int maxDepth = 4)
    {
        var repos = new List<CachedRepo>();
        ScanRecursive(directory, 0, maxDepth, repos);
        return repos;
    }

    private static void ScanRecursive(string dir, int depth, int maxDepth, List<CachedRepo> repos)
    {
        if (depth > maxDepth)
            return;

        try
        {
            // Check if this directory is a git repo (has .git dir or file)
            var gitPath = Path.Combine(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                var name = Path.GetFileName(dir);
                var repo = new CachedRepo { Name = name, Path = dir };

                // If .git is a file, this is a worktree — try to read branch
                if (File.Exists(gitPath))
                {
                    repo.Branch = TryReadWorktreeBranch(dir);
                }

                repos.Add(repo);
                // Don't recurse into git repos (submodules will be separate repos)
                return;
            }

            foreach (var subDir in Directory.GetDirectories(dir))
            {
                var dirName = Path.GetFileName(subDir);

                // Skip hidden directories
                if (dirName.StartsWith('.'))
                    continue;

                // Skip known non-repo directories
                if (SkipDirs.Contains(dirName))
                    continue;

                ScanRecursive(subDir, depth + 1, maxDepth, repos);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
        catch (DirectoryNotFoundException)
        {
            // Directory was removed between enumeration and access
        }
    }

    private static string? TryReadWorktreeBranch(string wtPath)
    {
        try
        {
            // For worktrees, HEAD is in the worktree's .git directory
            // Read HEAD to get current branch
            var headPath = Path.Combine(wtPath, ".git");
            if (!File.Exists(headPath))
                return null;

            // .git file in worktree contains: "gitdir: /path/to/main/.git/worktrees/<name>"
            var gitdirLine = File.ReadAllText(headPath).Trim();
            if (!gitdirLine.StartsWith("gitdir: "))
                return null;

            var gitdir = gitdirLine["gitdir: ".Length..].Trim();
            var worktreeHeadPath = Path.Combine(gitdir, "HEAD");
            if (!File.Exists(worktreeHeadPath))
                return null;

            var headContent = File.ReadAllText(worktreeHeadPath).Trim();
            const string refPrefix = "ref: refs/heads/";
            if (headContent.StartsWith(refPrefix))
                return headContent[refPrefix.Length..];
        }
        catch
        {
            // Best effort
        }

        return null;
    }

    /// <summary>
    /// Scans all registered directories and updates the repo cache.
    /// Prunes stale entries (paths that no longer exist) and merges new discoveries.
    /// </summary>
    public static void ScanAndUpdateCache(string configDir)
    {
        var scanPaths = ConfigLoader.LoadScanPaths(configDir);
        if (scanPaths.Count == 0)
            return;

        var discovered = new List<CachedRepo>();
        foreach (var sp in scanPaths)
        {
            if (Directory.Exists(sp.Path))
                discovered.AddRange(ScanDirectory(sp.Path));
        }

        var cache = ConfigLoader.LoadRepoCache(configDir);

        // Prune stale entries: remove repos whose path no longer exists
        cache.Repos.RemoveAll(r => !Directory.Exists(r.Path));

        // Merge new discoveries (avoid duplicates by path)
        var existingByPath = cache.Repos.ToDictionary(r => r.Path, PathComparer);
        var existingPaths = new HashSet<string>(existingByPath.Keys, PathComparer);
        foreach (var repo in discovered)
        {
            if (!existingPaths.Contains(repo.Path))
            {
                cache.Repos.Add(repo);
                existingPaths.Add(repo.Path);
            }
            else if (repo.Branch != null && existingByPath.TryGetValue(repo.Path, out var existing) && existing.Branch != repo.Branch)
            {
                existing.Branch = repo.Branch;
            }
        }

        ConfigLoader.SaveRepoCache(cache, configDir);
    }
}
