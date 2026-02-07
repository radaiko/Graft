using Graft.Core.Config;
using Graft.Core.Git;
using Graft.Core.Nuke;
using Graft.Core.Stack;
using Graft.Core.Tests.Helpers;
using Graft.Core.Worktree;

namespace Graft.Core.Tests.Nuke;

/// <summary>
/// Tests for nuke operations.
/// Uses real git repos via TempGitRepo.
/// </summary>
public sealed class NukeManagerTests : IDisposable
{
    private readonly TempGitRepo _repo = new();
    private readonly GitRunner _git;
    private readonly List<string> _worktreesToCleanup = [];

    public NukeManagerTests()
    {
        _git = new GitRunner(_repo.Path);
    }

    public void Dispose()
    {
        foreach (var wtPath in _worktreesToCleanup)
        {
            try { _git.RunAsync("worktree", "remove", "--force", wtPath).GetAwaiter().GetResult(); }
            catch { /* best effort */ }
        }
        _repo.Dispose();
    }

    private string ExpectedWorktreePath(string branch)
    {
        var repoName = Path.GetFileName(Path.GetFullPath(_repo.Path));
        var parentDir = Path.GetDirectoryName(Path.GetFullPath(_repo.Path))!;
        var safeBranch = branch.Replace('/', '-');
        var path = Path.Combine(parentDir, $"{repoName}.wt.{safeBranch}");
        _worktreesToCleanup.Add(path);
        return path;
    }

    // ========================
    // NukeStacksAsync
    // ========================

    [Fact]
    public async Task NukeStacks_RemovesAllStacks()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("stack-a", _repo.Path);
        await StackManager.InitAsync("stack-b", _repo.Path);

        var stacks = ConfigLoader.ListStacks(_repo.Path);
        Assert.Equal(2, stacks.Length);

        var result = await NukeManager.NukeStacksAsync(_repo.Path);

        Assert.Equal(2, result.Removed.Count);
        Assert.Contains("stack-a", result.Removed);
        Assert.Contains("stack-b", result.Removed);
        Assert.Empty(result.Errors);

        var remaining = ConfigLoader.ListStacks(_repo.Path);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task NukeStacks_NoStacks_ReturnsEmptyResult()
    {
        _repo.InitGraftDir();

        var result = await NukeManager.NukeStacksAsync(_repo.Path);

        Assert.Empty(result.Removed);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task NukeStacks_ClearsActiveStack()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("active-stack", _repo.Path);
        Assert.Equal("active-stack", ConfigLoader.LoadActiveStack(_repo.Path));

        await NukeManager.NukeStacksAsync(_repo.Path);

        Assert.Null(ConfigLoader.LoadActiveStack(_repo.Path));
    }

    // ========================
    // NukeWorktreesAsync
    // ========================

    [Fact]
    public async Task NukeWorktrees_RemovesAllLinkedWorktrees()
    {
        await _git.RunAsync("checkout", "-b", "wt-branch1");
        await _git.RunAsync("checkout", "master");
        await _git.RunAsync("checkout", "-b", "wt-branch2");
        await _git.RunAsync("checkout", "master");

        _ = ExpectedWorktreePath("wt-branch1");
        _ = ExpectedWorktreePath("wt-branch2");
        await WorktreeManager.AddAsync("wt-branch1", _repo.Path);
        await WorktreeManager.AddAsync("wt-branch2", _repo.Path);

        var worktrees = await WorktreeManager.ListAsync(_repo.Path);
        Assert.True(worktrees.Count >= 3); // main + 2 linked

        var result = await NukeManager.NukeWorktreesAsync(_repo.Path);

        Assert.Equal(2, result.Removed.Count);
        Assert.Contains("wt-branch1", result.Removed);
        Assert.Contains("wt-branch2", result.Removed);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task NukeWorktrees_NoWorktrees_RemovesNothing()
    {
        var result = await NukeManager.NukeWorktreesAsync(_repo.Path);

        // No linked worktrees should be removed
        Assert.Empty(result.Removed);
    }

    [Fact]
    public async Task NukeWorktrees_DirtyWorktree_WithoutForce_SkipsDirty()
    {
        await _git.RunAsync("checkout", "-b", "dirty-nuke-branch");
        await _git.RunAsync("checkout", "master");

        _ = ExpectedWorktreePath("dirty-nuke-branch");
        await WorktreeManager.AddAsync("dirty-nuke-branch", _repo.Path);

        // Make the worktree dirty
        var wtPath = ExpectedWorktreePath("dirty-nuke-branch").TrimEnd(Path.DirectorySeparatorChar);
        // Re-derive the actual path since ExpectedWorktreePath adds to cleanup
        var repoName = Path.GetFileName(Path.GetFullPath(_repo.Path));
        var parentDir = Path.GetDirectoryName(Path.GetFullPath(_repo.Path))!;
        var actualWtPath = Path.Combine(parentDir, $"{repoName}.wt.dirty-nuke-branch");
        File.WriteAllText(Path.Combine(actualWtPath, "untracked.txt"), "dirty");
        var gitCwd = new GitRunner(actualWtPath);
        await gitCwd.RunAsync("add", "untracked.txt");

        var result = await NukeManager.NukeWorktreesAsync(_repo.Path, force: false);

        Assert.Contains(result.Skipped, s => s.Contains("dirty-nuke-branch"));
        Assert.Empty(result.Removed);
    }

    [Fact]
    public async Task NukeWorktrees_DirtyWorktree_WithForce_RemovesDirty()
    {
        await _git.RunAsync("checkout", "-b", "force-nuke-branch");
        await _git.RunAsync("checkout", "master");

        _ = ExpectedWorktreePath("force-nuke-branch");
        await WorktreeManager.AddAsync("force-nuke-branch", _repo.Path);

        // Make the worktree dirty
        var repoName = Path.GetFileName(Path.GetFullPath(_repo.Path));
        var parentDir = Path.GetDirectoryName(Path.GetFullPath(_repo.Path))!;
        var actualWtPath = Path.Combine(parentDir, $"{repoName}.wt.force-nuke-branch");
        File.WriteAllText(Path.Combine(actualWtPath, "untracked.txt"), "dirty");
        var gitCwd = new GitRunner(actualWtPath);
        await gitCwd.RunAsync("add", "untracked.txt");

        var result = await NukeManager.NukeWorktreesAsync(_repo.Path, force: true);

        Assert.Contains("force-nuke-branch", result.Removed);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public async Task NukeWorktrees_DoesNotRemoveMainWorktree()
    {
        // Even with force, the main worktree (same path as repo) should not be removed
        var result = await NukeManager.NukeWorktreesAsync(_repo.Path, force: true);

        Assert.Empty(result.Removed);

        // Main worktree should still exist
        var worktrees = await WorktreeManager.ListAsync(_repo.Path);
        Assert.True(worktrees.Count >= 1);
    }

    // ========================
    // NukeBranchesAsync
    // ========================

    [Fact]
    public async Task NukeBranches_NoGoneBranches_ReturnsEmptyResult()
    {
        // No remote configured, so no branches can be "gone"
        var result = await NukeManager.NukeBranchesAsync(_repo.Path);

        Assert.Empty(result.Removed);
        Assert.Empty(result.Errors);
    }

    // ========================
    // NukeAllAsync
    // ========================

    [Fact]
    public async Task NukeAll_RemovesWorktreesAndStacks()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("nuke-stack", _repo.Path);

        await _git.RunAsync("checkout", "-b", "nuke-wt-branch");
        await _git.RunAsync("checkout", "master");
        _ = ExpectedWorktreePath("nuke-wt-branch");
        await WorktreeManager.AddAsync("nuke-wt-branch", _repo.Path);

        var result = await NukeManager.NukeAllAsync(_repo.Path);

        Assert.Contains(result.Removed, r => r == "worktree:nuke-wt-branch");
        Assert.Contains(result.Removed, r => r == "stack:nuke-stack");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task NukeAll_EmptyRepo_ReturnsEmptyResult()
    {
        _repo.InitGraftDir();

        var result = await NukeManager.NukeAllAsync(_repo.Path);

        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task NukeAll_PrefixesResultsWithType()
    {
        _repo.InitGraftDir();
        await StackManager.InitAsync("typed-stack", _repo.Path);

        var result = await NukeManager.NukeAllAsync(_repo.Path);

        Assert.All(result.Removed, r =>
            Assert.True(r.StartsWith("worktree:") || r.StartsWith("stack:") || r.StartsWith("branch:")));
    }
}
