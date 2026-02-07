namespace Graft.Core.Config;

public sealed class UpdateState
{
    public DateTime LastChecked { get; set; }
    public string CurrentVersion { get; set; } = "";
    public PendingUpdate? PendingUpdate { get; set; }
}

public sealed class PendingUpdate
{
    public required string Version { get; set; }
    public required string BinaryPath { get; set; }
    public required string Checksum { get; set; }
    public DateTime DownloadedAt { get; set; }
}
