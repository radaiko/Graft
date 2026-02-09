namespace Graft.Cli;

/// <summary>
/// ANSI escape code helpers for colorized terminal output.
/// Respects NO_COLOR env var and non-TTY output.
/// </summary>
internal static class Ansi
{
    private static readonly bool _enabled = CheckEnabled();

    public static bool Enabled => _enabled;

    public static string Reset => _enabled ? "\x1b[0m" : "";
    public static string Bold => _enabled ? "\x1b[1m" : "";
    public static string Dim => _enabled ? "\x1b[2m" : "";

    public static string Red => _enabled ? "\x1b[31m" : "";
    public static string Green => _enabled ? "\x1b[32m" : "";
    public static string Yellow => _enabled ? "\x1b[33m" : "";
    public static string Blue => _enabled ? "\x1b[34m" : "";
    public static string Magenta => _enabled ? "\x1b[35m" : "";
    public static string Cyan => _enabled ? "\x1b[36m" : "";
    public static string Gray => _enabled ? "\x1b[90m" : "";

    private static bool CheckEnabled()
    {
        if (Environment.GetEnvironmentVariable("NO_COLOR") != null)
            return false;
        try
        {
            return !Console.IsOutputRedirected;
        }
        catch
        {
            return false;
        }
    }
}
