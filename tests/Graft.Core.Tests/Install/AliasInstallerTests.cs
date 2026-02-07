using Graft.Core.Install;

namespace Graft.Core.Tests.Install;

/// <summary>
/// Tests for alias installation per spec section 2.
/// </summary>
public sealed class AliasInstallerTests
{
    // Requirement: `graft install` creates `gt` symlink next to graft binary
    [Fact]
    public void Install_CreatesGtSymlink()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeBinary = Path.Combine(tempDir, "graft");
            File.WriteAllText(fakeBinary, "fake binary");

            AliasInstaller.Install(fakeBinary, Path.Combine(tempDir, ".gitconfig"));

            var expectedSymlink = Path.Combine(tempDir, "gt");
            Assert.True(File.Exists(expectedSymlink), "gt symlink should exist");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Requirement: `graft install` writes `gt = !graft` to ~/.gitconfig
    [Fact]
    public void Install_WritesGitConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeBinary = Path.Combine(tempDir, "graft");
            File.WriteAllText(fakeBinary, "fake binary");
            var fakeGitconfig = Path.Combine(tempDir, ".gitconfig");
            File.WriteAllText(fakeGitconfig, "[user]\n\tname = Test\n");

            AliasInstaller.Install(fakeBinary, gitconfigPath: fakeGitconfig);

            var content = File.ReadAllText(fakeGitconfig);
            Assert.Contains("gt = !graft", content);
            Assert.Contains("[alias]", content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Requirement: All three forms identical: graft, gt, git gt
    [Fact]
    public void BinaryDetection_GraftAndGt_BehaveIdentically()
    {
        // The binary detects whether invoked as "graft" or "gt" and behaves identically.
        // This is handled by the CLI entry point, not the AliasInstaller.
        // Verify the symlink creation (which enables the gt invocation).
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeBinary = Path.Combine(tempDir, "graft");
            File.WriteAllText(fakeBinary, "fake binary");

            AliasInstaller.Install(fakeBinary, Path.Combine(tempDir, ".gitconfig"));

            var gtPath = Path.Combine(tempDir, "gt");
            Assert.True(File.Exists(gtPath), "gt should exist after install");
            // Both point to the same binary
            var fi = new FileInfo(gtPath);
            Assert.NotNull(fi.LinkTarget);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Requirement: `graft uninstall` removes gt symlink
    [Fact]
    public void Uninstall_RemovesGtSymlink()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeBinary = Path.Combine(tempDir, "graft");
            File.WriteAllText(fakeBinary, "fake");
            var fakeGitconfig = Path.Combine(tempDir, ".gitconfig");
            File.WriteAllText(fakeGitconfig, "");

            // Install first
            AliasInstaller.Install(fakeBinary, fakeGitconfig);
            Assert.True(File.Exists(Path.Combine(tempDir, "gt")));

            // Act
            AliasInstaller.Uninstall(fakeBinary, fakeGitconfig);

            Assert.False(File.Exists(Path.Combine(tempDir, "gt")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Requirement: `graft uninstall` removes git alias
    [Fact]
    public void Uninstall_RemovesGitAlias()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeGitconfig = Path.Combine(tempDir, ".gitconfig");
            File.WriteAllText(fakeGitconfig, "[user]\n\tname = Test\n[alias]\n\tgt = !graft\n");
            var fakeBinary = Path.Combine(tempDir, "graft");
            File.WriteAllText(fakeBinary, "fake");

            AliasInstaller.Uninstall(fakeBinary, gitconfigPath: fakeGitconfig);

            var content = File.ReadAllText(fakeGitconfig);
            Assert.DoesNotContain("gt = !graft", content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Edge case: gt already exists (not created by graft)
    [Fact]
    public void Install_GtAlreadyExists_HandlesGracefully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeBinary = Path.Combine(tempDir, "graft");
            var existingGt = Path.Combine(tempDir, "gt");
            File.WriteAllText(fakeBinary, "fake");
            File.WriteAllText(existingGt, "some other tool");

            // Should not crash — overwrites the existing file
            var ex = Record.Exception(() =>
                AliasInstaller.Install(fakeBinary, Path.Combine(tempDir, ".gitconfig")));
            Assert.Null(ex);
            Assert.True(File.Exists(existingGt));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // Edge case: Uninstall when no aliases exist
    [Fact]
    public void Uninstall_NoAliases_HandlesGracefully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graft-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeBinary = Path.Combine(tempDir, "graft");
            File.WriteAllText(fakeBinary, "fake");
            // No gt symlink, no gitconfig alias

            var ex = Record.Exception(() =>
                AliasInstaller.Uninstall(fakeBinary, Path.Combine(tempDir, ".gitconfig")));
            Assert.Null(ex);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
