using Graft.Core.Config;
using Graft.Core.Git;

namespace Graft.Core.Stack;

public static class ActiveStackManager
{
    /// <summary>
    /// Gets the active stack name. Throws if no active stack is set.
    /// If no active-stack file exists and exactly one stack exists, auto-sets it.
    /// </summary>
    public static string GetActiveStackName(string repoPath)
    {
        var name = ConfigLoader.LoadActiveStack(repoPath);
        if (name != null)
            return name;

        // Migration: auto-set if exactly one stack exists
        var stacks = ConfigLoader.ListStacks(repoPath);
        if (stacks.Length == 1)
        {
            SetActiveStack(stacks[0], repoPath);
            return stacks[0];
        }

        if (stacks.Length == 0)
            throw new InvalidOperationException(
                "No stacks found. Run 'graft stack init <name>' to create one.");

        throw new InvalidOperationException(
            $"No active stack. Multiple stacks exist ({string.Join(", ", stacks)}). " +
            $"Switch to one with 'graft stack switch <name>'.");
    }

    /// <summary>
    /// Sets the active stack. Validates that the stack exists.
    /// </summary>
    public static void SetActiveStack(string name, string repoPath)
    {
        Validation.ValidateStackName(name);

        // Verify stack exists
        var commonDir = GitRunner.ResolveGitCommonDir(repoPath);
        var stackPath = Path.Combine(commonDir, "graft", "stacks", $"{name}.toml");
        if (!File.Exists(stackPath))
            throw new FileNotFoundException($"Stack '{name}' not found", stackPath);

        ConfigLoader.SaveActiveStack(name, repoPath);
    }

    /// <summary>
    /// Clears the active stack (e.g. when deleting the active stack).
    /// </summary>
    public static void ClearActiveStack(string repoPath)
    {
        ConfigLoader.SaveActiveStack(null, repoPath);
    }
}
