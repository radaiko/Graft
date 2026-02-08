namespace Graft.Cli;

public static class CliPaths
{
    public static string GetConfigDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "graft");
    }
}
