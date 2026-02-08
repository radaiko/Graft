using System.Globalization;
using System.Text;
using Tomlyn;
using Tomlyn.Model;
using Graft.Core.Git;
using Graft.Core.Scan;
using Graft.Core.Stack;

namespace Graft.Core.Config;

public static class ConfigLoader
{
    private static string GetGraftDir(string repoPath) =>
        Path.Combine(GitRunner.ResolveGitCommonDir(repoPath), "graft");

    private static string GetStacksDir(string repoPath) =>
        Path.Combine(GetGraftDir(repoPath), "stacks");

    private static string GetActiveStackPath(string repoPath) =>
        Path.Combine(GetGraftDir(repoPath), "active-stack");

    /// <summary>
    /// Reads the active stack name from .git/graft/active-stack.
    /// Returns null if no active stack is set.
    /// </summary>
    public static string? LoadActiveStack(string repoPath)
    {
        var path = GetActiveStackPath(repoPath);
        if (!File.Exists(path))
            return null;

        var name = File.ReadAllText(path, Encoding.UTF8).Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>
    /// Writes the active stack name to .git/graft/active-stack.
    /// Pass null to delete the file (clear active stack).
    /// </summary>
    public static void SaveActiveStack(string? name, string repoPath)
    {
        var path = GetActiveStackPath(repoPath);

        if (name == null)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        var graftDir = GetGraftDir(repoPath);
        Directory.CreateDirectory(graftDir);

        // Atomic write
        var tempPath = $"{path}.tmp.{Guid.NewGuid():N}";
        File.WriteAllText(tempPath, name, Encoding.UTF8);
        File.Move(tempPath, path, overwrite: true);
    }

    public static StackDefinition LoadStack(string name, string repoPath)
    {
        Validation.ValidateStackName(name);

        var stackPath = Path.Combine(GetStacksDir(repoPath), $"{name}.toml");
        if (!File.Exists(stackPath))
            throw new FileNotFoundException($"Stack '{name}' not found", stackPath);

        var toml = File.ReadAllText(stackPath, Encoding.UTF8);
        TomlTable table;
        try
        {
            table = Toml.ToModel(toml);
        }
        catch (TomlException ex)
        {
            throw new InvalidOperationException($"Stack '{name}' has invalid TOML: {ex.Message}", ex);
        }

        if (!table.ContainsKey("trunk"))
            throw new InvalidOperationException($"Stack '{name}' is missing required field 'trunk'");

        if (table["trunk"] is not string trunkValue)
            throw new InvalidOperationException($"Stack '{name}' field 'trunk' must be a string");

        Validation.ValidateName(trunkValue, "Trunk branch");

        var stack = new StackDefinition
        {
            Name = table.TryGetValue("name", out var n) && n is string nameStr ? nameStr : name,
            Trunk = trunkValue,
        };

        Validation.ValidateStackName(stack.Name);

        if (table.TryGetValue("created_at", out var createdAt))
        {
            if (createdAt is string createdAtStr)
                stack.CreatedAt = DateTime.Parse(createdAtStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            else if (createdAt is TomlDateTime dt)
                stack.CreatedAt = dt.DateTime.DateTime;
        }

        if (table.TryGetValue("updated_at", out var updatedAt))
        {
            if (updatedAt is string updatedAtStr)
                stack.UpdatedAt = DateTime.Parse(updatedAtStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            else if (updatedAt is TomlDateTime dt2)
                stack.UpdatedAt = dt2.DateTime.DateTime;
        }

        if (table.TryGetValue("branches", out var branchesObj) && branchesObj is TomlTableArray branches)
        {
            foreach (TomlTable branchTable in branches)
            {
                if (branchTable["name"] is not string branchName)
                    throw new InvalidOperationException($"Stack '{name}' has a branch entry without a valid 'name' field");

                Validation.ValidateName(branchName, "Branch name");
                var branch = new StackBranch { Name = branchName };
                if (branchTable.TryGetValue("pr_number", out var prNum) &&
                    branchTable.TryGetValue("pr_url", out var prUrl) &&
                    prUrl is string prUrlStr)
                {
                    ulong prNumber;
                    try
                    {
                        prNumber = Convert.ToUInt64(prNum, CultureInfo.InvariantCulture);
                    }
                    catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                    {
                        throw new InvalidOperationException(
                            $"Stack '{name}' branch '{branchName}' has invalid pr_number: expected an integer, got '{prNum}'", ex);
                    }

                    var prState = PrState.Open;
                    if (branchTable.TryGetValue("pr_state", out var stateObj) && stateObj is string stateStr)
                    {
                        prState = stateStr switch
                        {
                            "merged" => PrState.Merged,
                            "closed" => PrState.Closed,
                            _ => PrState.Open,
                        };
                    }

                    branch.Pr = new PullRequestRef
                    {
                        Number = prNumber,
                        Url = prUrlStr,
                        State = prState,
                    };
                }
                stack.Branches.Add(branch);
            }
        }

        return stack;
    }

    public static void SaveStack(StackDefinition stack, string repoPath)
    {
        Validation.ValidateStackName(stack.Name);

        var stacksDir = GetStacksDir(repoPath);
        Directory.CreateDirectory(stacksDir);
        var stackPath = Path.Combine(stacksDir, $"{stack.Name}.toml");

        var table = new TomlTable
        {
            ["name"] = stack.Name,
            ["trunk"] = stack.Trunk,
            ["created_at"] = stack.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            ["updated_at"] = stack.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
        };

        if (stack.Branches.Count > 0)
        {
            var branches = new TomlTableArray();
            foreach (var branch in stack.Branches)
            {
                var bt = new TomlTable { ["name"] = branch.Name };
                if (branch.Pr != null)
                {
                    bt["pr_number"] = (long)branch.Pr.Number;
                    bt["pr_url"] = branch.Pr.Url;
                    bt["pr_state"] = branch.Pr.State.ToString().ToLowerInvariant();
                }
                branches.Add(bt);
            }
            table["branches"] = branches;
        }

        // Atomic write: write to uniquely-named temp file then rename
        var tempPath = $"{stackPath}.tmp.{Guid.NewGuid():N}";
        File.WriteAllText(tempPath, Toml.FromModel(table), Encoding.UTF8);
        File.Move(tempPath, stackPath, overwrite: true);
    }

    public static string[] ListStacks(string repoPath)
    {
        var stacksDir = GetStacksDir(repoPath);
        if (!Directory.Exists(stacksDir))
            return [];
        return Directory.GetFiles(stacksDir, "*.toml")
            .Where(f => Path.GetFileName(f).EndsWith(".toml", StringComparison.Ordinal))
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n)
            .ToArray();
    }

    public static UpdateState LoadUpdateState(string configDir)
    {
        var statePath = Path.Combine(configDir, "update-state.toml");
        if (!File.Exists(statePath))
            return new UpdateState();

        var toml = File.ReadAllText(statePath, Encoding.UTF8);
        var table = Toml.ToModel(toml);

        var state = new UpdateState();
        if (table.TryGetValue("last_checked", out var lastChecked))
        {
            if (lastChecked is string lcStr)
                state.LastChecked = DateTime.Parse(lcStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            else if (lastChecked is TomlDateTime dt)
                state.LastChecked = dt.DateTime.DateTime;
        }

        if (table.TryGetValue("current_version", out var version) && version is string versionStr)
            state.CurrentVersion = versionStr;

        if (table.TryGetValue("pending_update", out var puObj) && puObj is TomlTable pu)
        {
            if (pu["version"] is not string puVersion ||
                pu["binary_path"] is not string puBinaryPath ||
                pu["checksum"] is not string puChecksum)
            {
                throw new InvalidOperationException("pending_update section is missing required string fields (version, binary_path, checksum)");
            }

            state.PendingUpdate = new PendingUpdate
            {
                Version = puVersion,
                BinaryPath = puBinaryPath,
                Checksum = puChecksum,
            };
            if (pu.TryGetValue("downloaded_at", out var dlAt) && dlAt is string dlStr)
            {
                state.PendingUpdate.DownloadedAt = DateTime.Parse(dlStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
        }

        return state;
    }

    // ========================
    // Scan Paths
    // ========================

    public static List<ScanPath> LoadScanPaths(string configDir)
    {
        var configPath = Path.Combine(configDir, "config.toml");
        if (!File.Exists(configPath))
            return [];

        var toml = File.ReadAllText(configPath, Encoding.UTF8);
        var table = Toml.ToModel(toml);

        var paths = new List<ScanPath>();
        if (table.TryGetValue("scan_paths", out var scanPathsObj) && scanPathsObj is TomlTableArray scanPaths)
        {
            foreach (TomlTable entry in scanPaths)
            {
                if (entry.TryGetValue("path", out var pathObj) && pathObj is string pathStr)
                    paths.Add(new ScanPath { Path = pathStr });
            }
        }

        return paths;
    }

    public static void SaveScanPaths(List<ScanPath> scanPaths, string configDir)
    {
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "config.toml");

        // Load existing config to preserve other keys
        TomlTable table;
        if (File.Exists(configPath))
        {
            var existing = File.ReadAllText(configPath, Encoding.UTF8);
            table = Toml.ToModel(existing);
        }
        else
        {
            table = new TomlTable();
        }

        // Replace scan_paths
        if (scanPaths.Count > 0)
        {
            var arr = new TomlTableArray();
            foreach (var sp in scanPaths)
                arr.Add(new TomlTable { ["path"] = sp.Path });
            table["scan_paths"] = arr;
        }
        else
        {
            table.Remove("scan_paths");
        }

        var tempPath = $"{configPath}.tmp.{Guid.NewGuid():N}";
        File.WriteAllText(tempPath, Toml.FromModel(table), Encoding.UTF8);
        File.Move(tempPath, configPath, overwrite: true);
    }

    // ========================
    // Repo Cache
    // ========================

    public static RepoCache LoadRepoCache(string configDir)
    {
        var cachePath = Path.Combine(configDir, "repo-cache.toml");
        if (!File.Exists(cachePath))
            return new RepoCache();

        var toml = File.ReadAllText(cachePath, Encoding.UTF8);
        var table = Toml.ToModel(toml);

        var cache = new RepoCache();
        if (table.TryGetValue("repos", out var reposObj) && reposObj is TomlTableArray repos)
        {
            foreach (TomlTable entry in repos)
            {
                if (entry.TryGetValue("name", out var nameObj) && nameObj is string name &&
                    entry.TryGetValue("path", out var pathObj) && pathObj is string path)
                {
                    var repo = new CachedRepo { Name = name, Path = path };
                    if (entry.TryGetValue("branch", out var branchObj) && branchObj is string branch)
                        repo.Branch = branch;
                    if (entry.TryGetValue("auto_fetch", out var afObj) && afObj is bool af)
                        repo.AutoFetch = af;
                    cache.Repos.Add(repo);
                }
            }
        }

        return cache;
    }

    public static void SaveRepoCache(RepoCache cache, string configDir)
    {
        Directory.CreateDirectory(configDir);
        var cachePath = Path.Combine(configDir, "repo-cache.toml");

        var table = new TomlTable();
        if (cache.Repos.Count > 0)
        {
            var arr = new TomlTableArray();
            foreach (var repo in cache.Repos)
            {
                var entry = new TomlTable
                {
                    ["name"] = repo.Name,
                    ["path"] = repo.Path,
                    ["auto_fetch"] = repo.AutoFetch,
                };
                if (repo.Branch != null)
                    entry["branch"] = repo.Branch;
                arr.Add(entry);
            }
            table["repos"] = arr;
        }

        var tempPath = $"{cachePath}.tmp.{Guid.NewGuid():N}";
        File.WriteAllText(tempPath, Toml.FromModel(table), Encoding.UTF8);
        File.Move(tempPath, cachePath, overwrite: true);
    }

    public static void AddRepoToCache(CachedRepo repo, string configDir)
    {
        var cache = LoadRepoCache(configDir);
        // Avoid duplicates by path
        if (cache.Repos.Any(r => string.Equals(r.Path, repo.Path, StringComparison.Ordinal)))
            return;
        cache.Repos.Add(repo);
        SaveRepoCache(cache, configDir);
    }

    public static void RemoveRepoFromCache(string repoPath, string configDir)
    {
        var cache = LoadRepoCache(configDir);
        var removed = cache.Repos.RemoveAll(r => string.Equals(r.Path, repoPath, StringComparison.Ordinal));
        if (removed > 0)
            SaveRepoCache(cache, configDir);
    }
}
