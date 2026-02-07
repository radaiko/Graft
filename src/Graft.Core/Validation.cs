using System.Text.RegularExpressions;

namespace Graft.Core;

/// <summary>
/// Validates user-supplied names to prevent path traversal and git injection attacks.
/// </summary>
public static partial class Validation
{
    // Allowed for branches: alphanumeric, hyphens, underscores, forward slashes (for branch namespacing like auth/base-types), dots
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9/_\-\.]*$")]
    private static partial Regex SafeNamePattern();

    // Allowed for stacks: same as branches but NO forward slashes (stacks map to filenames)
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9_\-\.]*$")]
    private static partial Regex SafeStackNamePattern();

    /// <summary>
    /// Validates a branch name is safe for use in git commands.
    /// Allows forward slashes for namespacing (e.g., auth/base-types).
    /// </summary>
    public static void ValidateName(string name, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, kind);

        if (name.Contains(".."))
            throw new ArgumentException($"{kind} '{name}' must not contain '..'", kind);

        if (name.Contains('\0'))
            throw new ArgumentException($"{kind} '{name}' must not contain null bytes", kind);

        if (name.StartsWith('-'))
            throw new ArgumentException($"{kind} '{name}' must not start with '-'", kind);

        if (name.StartsWith('/') || name.EndsWith('/'))
            throw new ArgumentException($"{kind} '{name}' must not start or end with '/'", kind);

        if (name.Contains("//"))
            throw new ArgumentException($"{kind} '{name}' must not contain consecutive slashes", kind);

        if (name.Contains('\\'))
            throw new ArgumentException($"{kind} '{name}' must not contain backslashes", kind);

        if (name.EndsWith(".lock", StringComparison.Ordinal))
            throw new ArgumentException($"{kind} '{name}' must not end with '.lock'", kind);

        if (name.Contains("@{"))
            throw new ArgumentException($"{kind} '{name}' must not contain '@{{'", kind);

        if (!SafeNamePattern().IsMatch(name))
            throw new ArgumentException(
                $"{kind} '{name}' contains invalid characters. Use alphanumeric, hyphens, underscores, dots, or forward slashes.", kind);
    }

    /// <summary>
    /// Validates a stack name. Stacks map to filenames so forward slashes are NOT allowed.
    /// </summary>
    public static void ValidateStackName(string name)
    {
        const string kind = "Stack name";
        ArgumentException.ThrowIfNullOrWhiteSpace(name, kind);

        if (name.Contains(".."))
            throw new ArgumentException($"{kind} '{name}' must not contain '..'", kind);

        if (name.Contains('\0'))
            throw new ArgumentException($"{kind} '{name}' must not contain null bytes", kind);

        if (name.StartsWith('-'))
            throw new ArgumentException($"{kind} '{name}' must not start with '-'", kind);

        if (name.Contains('/'))
            throw new ArgumentException($"{kind} '{name}' must not contain forward slashes. Stack names are used as filenames.", kind);

        if (name.Contains('\\'))
            throw new ArgumentException($"{kind} '{name}' must not contain backslashes", kind);

        if (!SafeStackNamePattern().IsMatch(name))
            throw new ArgumentException(
                $"{kind} '{name}' contains invalid characters. Use alphanumeric, hyphens, underscores, or dots.", kind);
    }
}
