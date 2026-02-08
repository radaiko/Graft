using Graft.Core.Worktree;

namespace Graft.Core.Status;

public sealed class RepoStatus
{
    public required string Name { get; set; }
    public required string Path { get; set; }
    public string? Branch { get; set; }
    public string? Upstream { get; set; }
    public int Ahead { get; set; }
    public int Behind { get; set; }
    public int ChangedFiles { get; set; }
    public int UntrackedFiles { get; set; }
    public string? ActiveStackName { get; set; }
    public int ActiveStackBranchCount { get; set; }
    public List<StackSummary> Stacks { get; set; } = [];
    public List<WorktreeInfo> Worktrees { get; set; } = [];
    public bool IsAccessible { get; set; } = true;
    public string? Error { get; set; }
}

public sealed class StackSummary
{
    public required string Name { get; set; }
    public required string Trunk { get; set; }
    public List<StackBranchSummary> Branches { get; set; } = [];
    public bool IsActive { get; set; }
}

public sealed class StackBranchSummary
{
    public required string Name { get; set; }
    public int Ahead { get; set; }
    public int Behind { get; set; }
}
