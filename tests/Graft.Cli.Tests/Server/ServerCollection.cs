namespace Graft.Cli.Tests.Server;

/// <summary>
/// Collection definition to run server integration tests serially.
/// Tests that start HttpListener-based servers must not run in parallel
/// to avoid port allocation races.
/// </summary>
[CollectionDefinition("Server", DisableParallelization = true)]
public class ServerCollection;
