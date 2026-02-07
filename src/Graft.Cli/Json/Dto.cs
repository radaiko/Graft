using Graft.Core.Stack;

namespace Graft.Cli.Json;

public sealed class InitStackRequest
{
    public required string Name { get; set; }
    public string? BaseBranch { get; set; }
}

public sealed class PushBranchRequest
{
    public required string BranchName { get; set; }
    public bool CreateBranch { get; set; }
}

public sealed class CommitRequest
{
    public required string Message { get; set; }
    public string? Branch { get; set; }
    public bool Amend { get; set; }
}

public sealed class AddWorktreeRequest
{
    public required string Branch { get; set; }
    public bool CreateBranch { get; set; }
}

public sealed class DropBranchRequest
{
    public required string BranchName { get; set; }
}

public sealed class ShiftBranchRequest
{
    public required string BranchName { get; set; }
}

public sealed class SetActiveStackRequest
{
    public required string Name { get; set; }
}

public sealed class NukeRequest
{
    public bool Force { get; set; }
}

public sealed class ActiveStackResponse
{
    public string? Name { get; set; }
}

public sealed class PopBranchResponse
{
    public required string RemovedBranch { get; set; }
}

public sealed class ErrorResponse
{
    public required string Error { get; set; }
}

public sealed class GitStatusResponse
{
    public required string CurrentBranch { get; set; }
    public List<string> ModifiedFiles { get; set; } = [];
    public List<string> StagedFiles { get; set; } = [];
    public List<string> UntrackedFiles { get; set; } = [];
}

public sealed class StackDetailResponse
{
    public required string Name { get; set; }
    public required string Trunk { get; set; }
    public List<BranchDetail> Branches { get; set; } = [];
    public bool HasConflict { get; set; }
    public string? CurrentBranch { get; set; }
    public bool IsActive { get; set; }
}

public sealed class BranchDetail
{
    public required string Name { get; set; }
    public int CommitCount { get; set; }
    public bool NeedsMerge { get; set; }
    public bool IsHead { get; set; }
    public PullRequestRef? Pr { get; set; }
}
