namespace Graft.Core.AutoUpdate;

public sealed class GitHubRelease
{
    public string TagName { get; set; } = "";
    public bool Draft { get; set; }
    public bool Prerelease { get; set; }
    public List<GitHubAsset> Assets { get; set; } = [];
}

public sealed class GitHubAsset
{
    public string Name { get; set; } = "";
    public string BrowserDownloadUrl { get; set; } = "";
}
