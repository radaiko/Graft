using System.Text.Json.Serialization;

namespace Graft.Core.AutoUpdate;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(List<GitHubRelease>))]
internal partial class GitHubJsonContext : JsonSerializerContext;
