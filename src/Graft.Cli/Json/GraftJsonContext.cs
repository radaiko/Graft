using System.Text.Json;
using System.Text.Json.Serialization;
using Graft.Core.Commit;
using Graft.Core.Nuke;
using Graft.Core.Stack;
using Graft.Core.Worktree;

namespace Graft.Cli.Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(JsonStringEnumConverter<SyncStatus>),
                  typeof(JsonStringEnumConverter<PrState>),
]
)]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(InitStackRequest))]
[JsonSerializable(typeof(PushBranchRequest))]
[JsonSerializable(typeof(CommitRequest))]
[JsonSerializable(typeof(AddWorktreeRequest))]
[JsonSerializable(typeof(DropBranchRequest))]
[JsonSerializable(typeof(ShiftBranchRequest))]
[JsonSerializable(typeof(SetActiveStackRequest))]
[JsonSerializable(typeof(NukeRequest))]
[JsonSerializable(typeof(ActiveStackResponse))]
[JsonSerializable(typeof(PopBranchResponse))]
[JsonSerializable(typeof(StackDetailResponse))]
[JsonSerializable(typeof(GitStatusResponse))]
[JsonSerializable(typeof(SyncResult))]
[JsonSerializable(typeof(CommitResult))]
[JsonSerializable(typeof(NukeResult))]
[JsonSerializable(typeof(List<WorktreeInfo>))]
public partial class GraftJsonContext : JsonSerializerContext
{
}
