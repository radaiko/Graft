using Graft.Core.Git;
using Graft.Core.Tests.Helpers;
using Graft.Core.Worktree;

namespace Graft.Core.Tests.Worktree;

/// <summary>
/// Tests for worktree management operations.
/// Uses real git repos.
/// </summary>
public sealed class WorktreeManagerTests : IDisposable
{
    private readonly TempGitRepo _repo = new();
    private readonly GitRunner _git;
    private readonly List<string> _worktreesToCleanup = [];

    public WorktreeManagerTests()
    {
        _git = new GitRunner(_repo.Path);
    }

    public void Dispose()
    {
        // Clean up any worktrees created during tests
        foreach (var wtPath in _worktreesToCleanup)
        {
            try { _git.RunAsync("worktree", "remove", "--force", wtPath).GetAwaiter().GetResult(); }
            catch { /* best effort */ }
        }
        _repo.Dispose();
    }

    /// <summary>
    /// Computes expected worktree path using the new convention:
    /// ../{repoName}.wt.{safeBranch}/
    /// </summary>
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
    // graft wt <branch>
    // ========================

    [Fact]
    public async Task Add_ValidBranch_CreatesWorktree()
    {
        await _git.RunAsync("checkout", "-b", "feature-branch");
        await _git.RunAsync("checkout", "master");

        var branches = await _git.RunAsync("branch", "--list", "feature-branch");
        Assert.Contains("feature-branch", branches.Stdout);

        _ = ExpectedWorktreePath("feature-branch");
        await WorktreeManager.AddAsync("feature-branch", _repo.Path);

        var wtList = await _git.RunAsync("worktree", "list");
        Assert.Contains("feature-branch", wtList.Stdout);
    }

    [Fact]
    public async Task Add_NonExistentBranch_ThrowsError()
    {
        var branches = await _git.RunAsync("branch", "--list", "nonexistent");
        Assert.True(string.IsNullOrWhiteSpace(branches.Stdout));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            WorktreeManager.AddAsync("nonexistent", _repo.Path));
    }

    [Fact]
    public async Task Add_CreateBranch_CreatesAndAddsWorktree()
    {
        _ = ExpectedWorktreePath("new-wt-branch");
        await WorktreeManager.AddAsync("new-wt-branch", _repo.Path, createBranch: true);

        var wtList = await _git.RunAsync("worktree", "list");
        Assert.Contains("new-wt-branch", wtList.Stdout);
    }

    [Fact]
    public async Task Add_CreateBranch_ExistingBranch_ThrowsError()
    {
        await _git.RunAsync("checkout", "-b", "existing-wt");
        await _git.RunAsync("checkout", "master");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            WorktreeManager.AddAsync("existing-wt", _repo.Path, createBranch: true));
    }

    [Fact]
    public async Task Add_WorktreeAlreadyExists_ThrowsError()
    {
        await _git.RunAsync("checkout", "-b", "wt-branch");
        await _git.RunAsync("checkout", "master");

        var wtPath = ExpectedWorktreePath("wt-branch");
        await _git.RunAsync("worktree", "add", wtPath, "wt-branch");

        var wtList = await _git.RunAsync("worktree", "list");
        Assert.Contains("wt-branch", wtList.Stdout);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            WorktreeManager.AddAsync("wt-branch", _repo.Path));
    }

    [Fact]
    public async Task Add_UsesNewPathConvention()
    {
        await _git.RunAsync("checkout", "-b", "layout-test");
        await _git.RunAsync("checkout", "master");

        var expectedDir = ExpectedWorktreePath("layout-test");
        await WorktreeManager.AddAsync("layout-test", _repo.Path);

        Assert.True(Directory.Exists(expectedDir));
    }

    [Fact]
    public async Task Add_SlashBranch_ReplacesSlashWithHyphen()
    {
        await _git.RunAsync("checkout", "-b", "feature/test");
        await _git.RunAsync("checkout", "master");

        var expectedDir = ExpectedWorktreePath("feature/test");
        // Verify the path uses hyphen, not slash
        Assert.Contains(".wt.feature-test", expectedDir);

        await WorktreeManager.AddAsync("feature/test", _repo.Path);
        Assert.True(Directory.Exists(expectedDir));
    }

    // ========================
    // graft wt del <branch>
    // ========================

    [Fact]
    public async Task Remove_ExistingWorktree_RemovesIt()
    {
        await _git.RunAsync("checkout", "-b", "rm-branch");
        await _git.RunAsync("checkout", "master");
        var wtPath = ExpectedWorktreePath("rm-branch");
        await _git.RunAsync("worktree", "add", wtPath, "rm-branch");

        var wtList = await _git.RunAsync("worktree", "list");
        Assert.Contains("rm-branch", wtList.Stdout);

        await WorktreeManager.RemoveAsync("rm-branch", _repo.Path);

        wtList = await _git.RunAsync("worktree", "list");
        Assert.DoesNotContain("rm-branch", wtList.Stdout);
    }

    [Fact]
    public async Task Remove_DirtyWorktree_WithoutForce_ThrowsError()
    {
        await _git.RunAsync("checkout", "-b", "dirty-branch");
        await _git.RunAsync("checkout", "master");
        var wtPath = ExpectedWorktreePath("dirty-branch");
        await _git.RunAsync("worktree", "add", wtPath, "dirty-branch");

        // Create a file and stage it to make the worktree dirty
        File.WriteAllText(Path.Combine(wtPath, "untracked.txt"), "dirty");
        var gitCwd = new GitRunner(wtPath);
        await gitCwd.RunAsync("add", "untracked.txt");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            WorktreeManager.RemoveAsync("dirty-branch", _repo.Path));
    }

    [Fact]
    public async Task Remove_DirtyWorktree_WithForce_Succeeds()
    {
        await _git.RunAsync("checkout", "-b", "force-branch");
        await _git.RunAsync("checkout", "master");
        var wtPath = ExpectedWorktreePath("force-branch");
        await _git.RunAsync("worktree", "add", wtPath, "force-branch");

        // Make it dirty
        File.WriteAllText(Path.Combine(wtPath, "untracked.txt"), "dirty");
        var gitCwd = new GitRunner(wtPath);
        await gitCwd.RunAsync("add", "untracked.txt");

        await WorktreeManager.RemoveAsync("force-branch", _repo.Path, force: true);

        var wtList = await _git.RunAsync("worktree", "list");
        Assert.DoesNotContain("force-branch", wtList.Stdout);
    }

    [Fact]
    public async Task Remove_NoWorktreeForBranch_ThrowsError()
    {
        await Assert.ThrowsAnyAsync<Exception>(() =>
            WorktreeManager.RemoveAsync("no-worktree", _repo.Path));
    }

    // ========================
    // graft wt list
    // ========================

    [Fact]
    public async Task List_WithWorktrees_ReturnsWorktreesAndStatus()
    {
        await _git.RunAsync("checkout", "-b", "list-branch1");
        await _git.RunAsync("checkout", "master");

        var wtPath = ExpectedWorktreePath("list-branch1");
        await _git.RunAsync("worktree", "add", wtPath, "list-branch1");

        var worktrees = await WorktreeManager.ListAsync(_repo.Path);

        Assert.True(worktrees.Count >= 2); // main + list-branch1
        Assert.Contains(worktrees, w => w.Branch == "list-branch1");
    }

    [Fact]
    public async Task List_NoWorktrees_ReturnsOnlyMainWorkingTree()
    {
        var wtList = await _git.RunAsync("worktree", "list");
        Assert.True(wtList.Success);

        var worktrees = await WorktreeManager.ListAsync(_repo.Path);

        Assert.Single(worktrees);
    }
}
