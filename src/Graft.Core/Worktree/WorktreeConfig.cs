namespace Graft.Core.Worktree;

public sealed class WorktreeConfig
{
    public LayoutConfig Layout { get; set; } = new();
    public TemplateConfig Templates { get; set; } = new();
}

public sealed class LayoutConfig
{
    public string Pattern { get; set; } = "../{name}";
}

public sealed class TemplateConfig
{
    public List<TemplateFile> Files { get; set; } = [];
}

public sealed class TemplateFile
{
    public required string Src { get; set; }
    public required string Dst { get; set; }
    public TemplateMode Mode { get; set; } = TemplateMode.Copy;
}

public enum TemplateMode
{
    Copy,
    Symlink,
}
