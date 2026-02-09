using System.Runtime.InteropServices;
using Graft.Core.Config;
using Graft.Core.Git;

namespace Graft.Core.Scan;

/// <summary>
/// Runs background <c>git fetch --all</c> for repos with auto-fetch enabled.
/// Rate-limited per repo using the <see cref="CachedRepo.LastFetched"/> timestamp.
/// </summary>
public static class AutoFetcher
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(60);

    private static readonly StringComparison PathComparison =
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Fetches all repos that have auto-fetch enabled and are due for a fetch.
    /// Updates LastFetched timestamps in the repo cache on success.
    /// </summary>
    public static async Task FetchDueReposAsync(string configDir, CancellationToken ct = default)
    {
        var cache = ConfigLoader.LoadRepoCache(configDir);
        var dueRepos = GetDueRepos(cache, DefaultInterval);

        if (dueRepos.Count == 0)
            return;

        var updated = false;
        foreach (var repo in dueRepos)
        {
            ct.ThrowIfCancellationRequested();

            if (await TryFetchRepoAsync(repo, ct))
                updated = true;
        }

        if (updated)
            MergeFetchTimestamps(configDir, dueRepos);
    }

    /// <summary>
    /// Attempts to fetch a single repo. Returns true if LastFetched was updated.
    /// </summary>
    private static async Task<bool> TryFetchRepoAsync(CachedRepo repo, CancellationToken ct)
    {
        if (!Directory.Exists(repo.Path))
            return false;

        try
        {
            var git = new GitRunner(repo.Path, ct);
            var result = await git.RunAsync(FetchTimeout, "fetch", "--all", "--quiet");
            if (result.Success)
            {
                repo.LastFetched = DateTime.UtcNow;
                return true;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Network error, unreachable remote, etc. — skip this repo.
        }

        return false;
    }

    /// <summary>
    /// Fetches all repos in the cache once, regardless of auto-fetch flag or rate limit.
    /// Updates LastFetched timestamps on success. Reports progress via an optional callback.
    /// </summary>
    public static async Task FetchAllReposAsync(string configDir, Action<string, bool>? onRepoFetched = null, CancellationToken ct = default)
    {
        var cache = ConfigLoader.LoadRepoCache(configDir);
        var repos = cache.Repos.Where(r => !string.IsNullOrEmpty(r.Name) && Directory.Exists(r.Path)).ToList();

        if (repos.Count == 0)
            return;

        var updated = false;
        foreach (var repo in repos)
        {
            ct.ThrowIfCancellationRequested();

            var success = await TryFetchRepoAsync(repo, ct);
            onRepoFetched?.Invoke(repo.Name, success);
            if (success)
                updated = true;
        }

        if (updated)
            MergeFetchTimestamps(configDir, repos);
    }

    /// <summary>
    /// Re-reads the cache under a lock and merges LastFetched timestamps from the given repos.
    /// </summary>
    private static void MergeFetchTimestamps(string configDir, List<CachedRepo> fetchedRepos)
    {
        ConfigLoader.WithCacheLock(configDir, () =>
        {
            var freshCache = ConfigLoader.LoadRepoCache(configDir);
            foreach (var fetchedRepo in fetchedRepos.Where(r => r.LastFetched.HasValue))
            {
                var match = freshCache.Repos.Find(r =>
                    string.Equals(r.Path, fetchedRepo.Path, PathComparison));
                if (match != null)
                    match.LastFetched = fetchedRepo.LastFetched;
            }
            ConfigLoader.SaveRepoCache(freshCache, configDir);
        });
    }

    /// <summary>
    /// Returns repos that have auto-fetch enabled and haven't been fetched within the interval.
    /// </summary>
    public static List<CachedRepo> GetDueRepos(RepoCache cache, TimeSpan interval)
    {
        var now = DateTime.UtcNow;
        return cache.Repos
            .Where(r => r.AutoFetch && (!r.LastFetched.HasValue || now - r.LastFetched.Value > interval))
            .ToList();
    }

    public static void Enable(string repoPath, string configDir)
    {
        ConfigLoader.WithCacheLock(configDir, () =>
        {
            var cache = ConfigLoader.LoadRepoCache(configDir);
            var repo = FindRepoByPath(cache, repoPath)
                ?? throw new InvalidOperationException(
                    $"Repository not found in cache: '{repoPath}'. "
                    + "Register a scan directory with 'graft scan add <dir>' first, or wait for the background scan to discover it.");
            repo.AutoFetch = true;
            ConfigLoader.SaveRepoCache(cache, configDir);
        });
    }

    public static void Disable(string repoPath, string configDir)
    {
        ConfigLoader.WithCacheLock(configDir, () =>
        {
            var cache = ConfigLoader.LoadRepoCache(configDir);
            var repo = FindRepoByPath(cache, repoPath)
                ?? throw new InvalidOperationException(
                    $"Repository not found in cache: '{repoPath}'. "
                    + "Register a scan directory with 'graft scan add <dir>' first, or wait for the background scan to discover it.");
            repo.AutoFetch = false;
            repo.LastFetched = null;
            ConfigLoader.SaveRepoCache(cache, configDir);
        });
    }

    public static void EnableByName(string name, string configDir)
    {
        ConfigLoader.WithCacheLock(configDir, () =>
        {
            var cache = ConfigLoader.LoadRepoCache(configDir);
            var repo = FindRepoByName(cache, name);
            repo.AutoFetch = true;
            ConfigLoader.SaveRepoCache(cache, configDir);
        });
    }

    public static void DisableByName(string name, string configDir)
    {
        ConfigLoader.WithCacheLock(configDir, () =>
        {
            var cache = ConfigLoader.LoadRepoCache(configDir);
            var repo = FindRepoByName(cache, name);
            repo.AutoFetch = false;
            repo.LastFetched = null;
            ConfigLoader.SaveRepoCache(cache, configDir);
        });
    }

    private static CachedRepo FindRepoByName(RepoCache cache, string name)
    {
        var matches = cache.Repos
            .Where(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            0 => throw new InvalidOperationException(
                $"Repository '{name}' not found in cache. "
                + "Register a scan directory with 'graft scan add <dir>' first, or wait for the background scan to discover it."),
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Multiple repos match '{name}':\n"
                + string.Join("\n", matches.Select(r => $"  {r.Name} — {r.Path}"))
                + "\nUse a more specific name or run 'graft scan auto-fetch enable' from within the repo directory."),
        };
    }

    private static CachedRepo? FindRepoByPath(RepoCache cache, string path)
    {
        return cache.Repos.Find(r =>
            string.Equals(r.Path, path, PathComparison));
    }
}
