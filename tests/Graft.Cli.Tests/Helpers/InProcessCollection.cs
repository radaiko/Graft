namespace Graft.Cli.Tests.Helpers;

/// <summary>
/// Collection definition to run in-process CLI tests serially.
/// Tests that change CWD or redirect Console must not run in parallel.
/// </summary>
[CollectionDefinition("InProcess", DisableParallelization = true)]
public class InProcessCollection;
