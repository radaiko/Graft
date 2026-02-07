using Graft.Core.Config;

namespace Graft.Core.Tests.Config;

public sealed class GraftConfigTests
{
    // Requirement: Default trunk is "main"
    [Fact]
    public void DefaultsConfig_Trunk_IsMain()
    {
        var config = new GraftConfig();

        Assert.Equal("main", config.Defaults.Trunk);
    }

    // Requirement: Default PR strategy is "chain"
    [Fact]
    public void DefaultsConfig_StackPrStrategy_IsChain()
    {
        var config = new GraftConfig();

        Assert.Equal("chain", config.Defaults.StackPrStrategy);
    }

    // Requirement: GraftConfig defaults section is not null
    [Fact]
    public void GraftConfig_DefaultsSection_IsNotNull()
    {
        var config = new GraftConfig();

        Assert.NotNull(config.Defaults);
    }
}
