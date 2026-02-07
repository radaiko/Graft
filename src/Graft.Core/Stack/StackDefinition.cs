namespace Graft.Core.Stack;

public sealed class StackDefinition
{
    public required string Name { get; set; }
    public required string Trunk { get; set; }
    public List<StackBranch> Branches { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class StackBranch
{
    public required string Name { get; set; }
    public PullRequestRef? Pr { get; set; }
}

public sealed class PullRequestRef
{
    public required ulong Number { get; set; }
    public required string Url { get; set; }
    public PrState State { get; set; } = PrState.Open;
}

public enum PrState
{
    Open,
    Merged,
    Closed,
}
