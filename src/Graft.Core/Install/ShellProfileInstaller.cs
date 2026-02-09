using System.Runtime.InteropServices;

namespace Graft.Core.Install;

public record ShellIntegrationResult(string Shell, string ProfilePath, bool AlreadyPresent);

public static class ShellProfileInstaller
{
    private const string BeginMarker = "# >>> graft shell integration >>>";
    private const string EndMarker = "# <<< graft shell integration <<<";

    public static ShellIntegrationResult? Install(string? profilePathOverride = null)
    {
        var (shell, profilePath) = profilePathOverride != null
            ? (DetectShell() ?? "unknown", profilePathOverride)
            : DetectShellAndProfile();

        if (profilePath is null)
            return null;

        var code = ShellInitGenerator.Generate(shell);
        if (code is null)
            return null;

        var block = $"{BeginMarker}\n{code}\n{EndMarker}\n";

        // Ensure parent directory exists (e.g. ~/.config/fish/)
        var dir = Path.GetDirectoryName(profilePath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(profilePath))
        {
            var content = File.ReadAllText(profilePath);
            if (content.Contains(BeginMarker))
                return new ShellIntegrationResult(shell, profilePath, AlreadyPresent: true);

            // Append with a blank line separator
            var separator = content.Length > 0 && !content.EndsWith('\n') ? "\n\n" : "\n";
            File.AppendAllText(profilePath, separator + block);
        }
        else
        {
            File.WriteAllText(profilePath, block);
        }

        return new ShellIntegrationResult(shell, profilePath, AlreadyPresent: false);
    }

    public static bool Uninstall(string? profilePathOverride = null)
    {
        var (shell, profilePath) = profilePathOverride != null
            ? (DetectShell() ?? "unknown", profilePathOverride)
            : DetectShellAndProfile();

        if (profilePath is null || !File.Exists(profilePath))
            return false;

        var content = File.ReadAllText(profilePath);

        var startIdx = content.IndexOf(BeginMarker, StringComparison.Ordinal);
        if (startIdx < 0)
            return false;

        var endIdx = content.IndexOf(EndMarker, startIdx, StringComparison.Ordinal);
        if (endIdx < 0)
            return false;

        endIdx += EndMarker.Length;
        // Also consume the trailing newline if present
        if (endIdx < content.Length && content[endIdx] == '\n')
            endIdx++;

        // Remove leading blank line if the block was preceded by one
        if (startIdx > 0 && content[startIdx - 1] == '\n')
            startIdx--;

        var newContent = content[..startIdx] + content[endIdx..];
        File.WriteAllText(profilePath, newContent);
        return true;
    }

    private static (string shell, string? profilePath) DetectShellAndProfile()
    {
        var shell = DetectShell();
        if (shell is null)
            return ("unknown", null);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var profilePath = shell switch
        {
            "zsh" => Path.Combine(home, ".zshrc"),
            "bash" => GetBashProfile(home),
            "fish" => Path.Combine(home, ".config", "fish", "config.fish"),
            "pwsh" or "powershell" => GetPowershellProfile(),
            _ => null,
        };

        return (shell, profilePath);
    }

    private static string? DetectShell()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
            if (psModulePath != null)
                return "pwsh";
            return null;
        }

        var shellEnv = Environment.GetEnvironmentVariable("SHELL");
        if (shellEnv is null)
            return null;

        var name = Path.GetFileName(shellEnv);
        return name switch
        {
            "zsh" => "zsh",
            "bash" => "bash",
            "fish" => "fish",
            _ => null,
        };
    }

    private static string GetBashProfile(string home)
    {
        // Prefer .bashrc if it exists; fall back to .bash_profile (macOS login shells)
        var bashrc = Path.Combine(home, ".bashrc");
        if (File.Exists(bashrc))
            return bashrc;
        return Path.Combine(home, ".bash_profile");
    }

    private static string? GetPowershellProfile()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(home, "Documents", "PowerShell", "Microsoft.PowerShell_profile.ps1");
        return Path.Combine(home, ".config", "powershell", "Microsoft.PowerShell_profile.ps1");
    }
}
