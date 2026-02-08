namespace Graft.Core.Scan;

public sealed class ScanPath
{
    public string Path { get; set; } = "";
}

public sealed class CachedRepo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    /// <summary>
    /// Branch name for worktree entries (used by graft cd for branch-name lookup).
    /// Null for regular repos.
    /// </summary>
    public string? Branch { get; set; }
    public bool AutoFetch { get; set; }
    /// <summary>
    /// UTC timestamp of the last successful background fetch.
    /// Used for rate-limiting auto-fetch to avoid redundant network calls.
    /// </summary>
    public DateTime? LastFetched { get; set; }
}

public sealed class RepoCache
{
    public List<CachedRepo> Repos { get; set; } = [];
}
