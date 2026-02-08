using Graft.Cli.Tests.Helpers;

namespace Graft.Cli.Tests.Commands;

/// <summary>
/// Tests for the `graft stack` CLI command and its subcommands.
/// </summary>
public sealed class StackCommandTests
{
    // Requirement: `graft stack` command exists
    [Fact]
    public void Stack_IsRegisteredCommand()
    {
        var root = CliTestHelper.BuildRootCommand();

        var stackCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .FirstOrDefault(c => c.Name == "stack");

        Assert.NotNull(stackCommand);
    }

    // Requirement: `graft stack init <name>` parses correctly
    [Fact]
    public void StackInit_WithName_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack init auth-refactor");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void StackInit_WithoutName_HasParseError()
    {
        var result = CliTestHelper.Parse("stack init");
        Assert.NotEmpty(result.Errors);
    }

    // Requirement: `graft stack init <name> --base <branch>` parses correctly
    [Fact]
    public void StackInit_WithBase_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack init my-stack --base develop");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void StackInit_WithBaseAlias_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack init my-stack -b develop");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack list` parses correctly
    [Fact]
    public void StackList_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack list");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack ls` (alias) parses correctly
    [Fact]
    public void StackLs_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack ls");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack switch <name>` parses correctly
    [Fact]
    public void StackSwitch_WithName_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack switch my-stack");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack sw <name>` (alias) parses correctly
    [Fact]
    public void StackSw_WithName_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack sw my-stack");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack push <branch>` parses correctly
    [Fact]
    public void StackPush_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack push feature-branch");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void StackPush_WithCreate_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack push feature-branch --create");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void StackPush_WithCreateAlias_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack push feature-branch -c");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack pop` parses correctly
    [Fact]
    public void StackPop_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack pop");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack drop <branch>` parses correctly
    [Fact]
    public void StackDrop_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack drop feature-branch");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack shift <branch>` parses correctly
    [Fact]
    public void StackShift_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack shift feature-branch");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack commit -m <msg>` parses correctly
    [Fact]
    public void StackCommit_WithMessage_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack commit -m \"test message\"");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack commit --message <msg>` parses correctly
    [Fact]
    public void StackCommit_WithMessageLong_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack commit --message \"test message\"");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void StackCommit_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack commit -m \"test\" -b feature-branch");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack ci -m <msg>` (alias) parses correctly
    [Fact]
    public void StackCi_WithMessage_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack ci -m \"test message\"");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack sync` parses correctly
    [Fact]
    public void StackSync_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack sync");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void StackSync_WithBranch_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack sync feature-branch");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack log` parses correctly
    [Fact]
    public void StackLog_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack log");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack remove <name>` parses correctly
    [Fact]
    public void StackRemove_WithName_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack remove my-stack");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void StackRemove_WithoutName_HasParseError()
    {
        var result = CliTestHelper.Parse("stack remove");
        Assert.NotEmpty(result.Errors);
    }

    // Requirement: `graft stack remove --force` parses correctly
    [Fact]
    public void StackRemove_WithForce_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack remove my-stack --force");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void StackRemove_WithForceAlias_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack remove my-stack -f");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack rm <name>` (alias) parses correctly
    [Fact]
    public void StackRm_WithName_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack rm my-stack");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void StackRm_WithForce_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack rm my-stack -f");
        Assert.Empty(result.Errors);
    }

    // Requirement: `graft stack del` (deprecated alias) still parses
    [Fact]
    public void StackDel_Deprecated_ParsesWithoutErrors()
    {
        var result = CliTestHelper.Parse("stack del my-stack");
        Assert.Empty(result.Errors);
    }

    // Requirement: Subcommands exist under stack
    [Fact]
    public void Stack_HasExpectedSubcommands()
    {
        var root = CliTestHelper.BuildRootCommand();
        var stackCommand = root.Children
            .OfType<System.CommandLine.Command>()
            .First(c => c.Name == "stack");

        var subcommands = stackCommand.Children
            .OfType<System.CommandLine.Command>()
            .Select(c => c.Name)
            .ToList();

        Assert.Contains("init", subcommands);
        Assert.Contains("list", subcommands);
        Assert.Contains("ls", subcommands);
        Assert.Contains("switch", subcommands);
        Assert.Contains("sw", subcommands);
        Assert.Contains("push", subcommands);
        Assert.Contains("pop", subcommands);
        Assert.Contains("drop", subcommands);
        Assert.Contains("shift", subcommands);
        Assert.Contains("commit", subcommands);
        Assert.Contains("ci", subcommands);
        Assert.Contains("sync", subcommands);
        Assert.Contains("log", subcommands);
        Assert.Contains("remove", subcommands);
        Assert.Contains("rm", subcommands);
        Assert.Contains("del", subcommands);
    }
}
