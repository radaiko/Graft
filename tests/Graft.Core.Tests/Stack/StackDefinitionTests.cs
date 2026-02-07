using Graft.Core.Stack;

namespace Graft.Core.Tests.Stack;

public sealed class StackDefinitionTests
{
    // Requirement: StackDefinition has required Name and Trunk
    [Fact]
    public void Constructor_ValidNameAndTrunk_CreatesDefinition()
    {
        var stack = new StackDefinition { Name = "auth-refactor", Trunk = "main" };

        Assert.Equal("auth-refactor", stack.Name);
        Assert.Equal("main", stack.Trunk);
    }

    // Requirement: Branches ordered bottom-to-top (index 0 closest to trunk)
    [Fact]
    public void Branches_DefaultsToEmptyList()
    {
        var stack = new StackDefinition { Name = "test", Trunk = "main" };

        Assert.NotNull(stack.Branches);
        Assert.Empty(stack.Branches);
    }

    // Requirement: Branches ordered bottom-to-top
    [Fact]
    public void Branches_OrderedBottomToTop_IndexZeroClosestToTrunk()
    {
        var stack = new StackDefinition { Name = "test", Trunk = "main" };
        stack.Branches.Add(new StackBranch { Name = "base-types" });
        stack.Branches.Add(new StackBranch { Name = "session-manager" });
        stack.Branches.Add(new StackBranch { Name = "oauth-provider" });

        Assert.Equal("base-types", stack.Branches[0].Name);
        Assert.Equal("session-manager", stack.Branches[1].Name);
        Assert.Equal("oauth-provider", stack.Branches[2].Name);
    }

    // Requirement: CreatedAt/UpdatedAt default to UTC now
    [Fact]
    public void CreatedAt_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;
        var stack = new StackDefinition { Name = "test", Trunk = "main" };
        var after = DateTime.UtcNow;

        Assert.InRange(stack.CreatedAt, before, after);
        Assert.InRange(stack.UpdatedAt, before, after);
    }

    // Requirement: StackBranch has optional PR
    [Fact]
    public void StackBranch_PrDefaultsToNull()
    {
        var branch = new StackBranch { Name = "feature" };

        Assert.Null(branch.Pr);
    }

    // Requirement: StackBranch has pr_number and pr_url
    [Fact]
    public void StackBranch_WithPr_StoresPrRef()
    {
        var branch = new StackBranch
        {
            Name = "feature",
            Pr = new PullRequestRef
            {
                Number = 123,
                Url = "https://github.com/org/repo/pull/123",
            }
        };

        Assert.NotNull(branch.Pr);
        Assert.Equal(123UL, branch.Pr.Number);
        Assert.Equal("https://github.com/org/repo/pull/123", branch.Pr.Url);
    }

    // Requirement: PrState enum has Open, Merged, Closed
    [Theory]
    [InlineData(PrState.Open)]
    [InlineData(PrState.Merged)]
    [InlineData(PrState.Closed)]
    public void PrState_AllValuesExist(PrState state)
    {
        Assert.True(Enum.IsDefined(state));
    }

    // Requirement: PrState defaults to Open
    [Fact]
    public void PullRequestRef_StateDefaultsToOpen()
    {
        var pr = new PullRequestRef { Number = 1, Url = "https://example.com" };

        Assert.Equal(PrState.Open, pr.State);
    }
}
