using Graft.Core.Worktree;

namespace Graft.Core.Tests.Worktree;

public sealed class WorktreeConfigTests
{
    // Requirement: LayoutConfig default pattern is "../{name}"
    [Fact]
    public void LayoutConfig_DefaultPattern_IsRelativeName()
    {
        var config = new LayoutConfig();

        Assert.Equal("../{name}", config.Pattern);
    }

    // Requirement: WorktreeConfig has layout and templates
    [Fact]
    public void WorktreeConfig_DefaultsAreNotNull()
    {
        var config = new WorktreeConfig();

        Assert.NotNull(config.Layout);
        Assert.NotNull(config.Templates);
    }

    // Requirement: TemplateFile has Src, Dst, Mode
    [Fact]
    public void TemplateFile_CopyMode_IsDefault()
    {
        var file = new TemplateFile { Src = ".env.template", Dst = ".env" };

        Assert.Equal(TemplateMode.Copy, file.Mode);
    }

    // Requirement: TemplateFile supports Symlink mode
    [Fact]
    public void TemplateFile_SymlinkMode_CanBeSet()
    {
        var file = new TemplateFile
        {
            Src = ".vscode/settings.json",
            Dst = ".vscode/settings.json",
            Mode = TemplateMode.Symlink,
        };

        Assert.Equal(TemplateMode.Symlink, file.Mode);
    }
}
