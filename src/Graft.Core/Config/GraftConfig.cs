namespace Graft.Core.Config;

public sealed class GraftConfig
{
    public DefaultsConfig Defaults { get; set; } = new();
}

public sealed class DefaultsConfig
{
    public string Trunk { get; set; } = "main";
    public string StackPrStrategy { get; set; } = "chain";
}
