using Graft.Core.Config;
using Graft.Core.Tui;

namespace Graft.Core.Scan;

public sealed class NavigationResult
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string? Branch { get; init; }
}

public static class RepoNavigator
{
    /// <summary>
    /// Finds repos matching the given name. Checks repo Name first, then Branch field for worktrees.
    /// </summary>
    public static List<NavigationResult> FindByName(string name, string configDir)
    {
        var cache = ConfigLoader.LoadRepoCache(configDir);

        // Exact match on repo name
        var results = cache.Repos
            .Where(repo => string.Equals(repo.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(repo => new NavigationResult { Name = repo.Name, Path = repo.Path, Branch = repo.Branch })
            .ToList();

        if (results.Count > 0)
            return results;

        // Match on branch field (for worktree entries)
        results = cache.Repos
            .Where(repo => repo.Branch != null && string.Equals(repo.Branch, name, StringComparison.OrdinalIgnoreCase))
            .Select(repo => new NavigationResult { Name = repo.Name, Path = repo.Path, Branch = repo.Branch })
            .ToList();

        return results;
    }

    /// <summary>
    /// Returns all cached repos as picker items for the fuzzy picker.
    /// </summary>
    public static List<PickerItem<NavigationResult>> GetAllAsPickerItems(string configDir)
    {
        var cache = ConfigLoader.LoadRepoCache(configDir);
        var items = new List<PickerItem<NavigationResult>>();

        foreach (var repo in cache.Repos)
        {
            var description = repo.Branch != null ? $"[{repo.Branch}]" : repo.Path;
            items.Add(new PickerItem<NavigationResult>
            {
                Value = new NavigationResult
                {
                    Name = repo.Name,
                    Path = repo.Path,
                    Branch = repo.Branch,
                },
                Label = repo.Name,
                Description = description,
            });
        }

        return items;
    }
}
