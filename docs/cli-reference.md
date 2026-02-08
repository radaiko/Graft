---
layout: default
title: CLI Reference
nav_order: 3
---

# CLI Reference

Complete command reference for the Graft CLI.

---

## Stack Commands

All stack commands operate on the **active stack**. Use `graft stack switch` to change which stack is active.

### `graft stack init <name> [-b/--base <branch>]`

Create a new stack. Uses the current branch as trunk, or the branch specified with `-b`. The new stack becomes the active stack.

```bash
$ graft stack init my-feature
Created stack 'my-feature' with trunk 'main'.
Active stack: my-feature

$ graft stack init hotfix -b release/2.0
Created stack 'hotfix' with trunk 'release/2.0'.
Active stack: hotfix
```

### `graft stack list` (alias: `ls`)

List all stacks. The active stack is marked with `*`.

```bash
$ graft stack list
* my-feature  (trunk: main, 3 branches)
  hotfix      (trunk: release/2.0, 1 branch)
```

### `graft stack switch <name>` (alias: `sw`)

Switch the active stack. Fails if the named stack does not exist.

```bash
$ graft stack switch hotfix
Active stack: hotfix
```

### `graft stack push <branch> [-c/--create]`

Add a branch to the top of the active stack. With `-c`, creates the branch first. The branch is checked out.

```bash
# Add an existing branch
$ graft stack push feature/auth
Pushed 'feature/auth' to stack 'my-feature'.

# Create a new branch and add it
$ graft stack push -c feature/api
Created branch 'feature/api'.
Pushed 'feature/api' to stack 'my-feature'.
```

### `graft stack pop`

Remove the topmost branch from the active stack. The git branch is kept.

```bash
$ graft stack pop
Popped 'feature/api' from stack 'my-feature'.
```

### `graft stack drop <branch>`

Remove the named branch from the stack regardless of position. The git branch is kept. Use this after a branch's PR is squash-merged.

```bash
$ graft stack drop feature/auth
Dropped 'feature/auth' from stack 'my-feature'.
```

### `graft stack shift <branch>`

Insert an existing branch at the bottom of the stack (directly above the trunk). Useful when you need to add a base branch that other branches depend on.

```bash
$ graft stack shift feature/config
Shifted 'feature/config' to bottom of stack 'my-feature'.
```

### `graft stack commit --message "<message>" [-b <branch>] [--amend]` (alias: `ci`)

Commit staged changes to the active stack. Defaults to the topmost branch. Use `-b` to target a specific branch. Use `--amend` to amend the latest commit. Short form: `-m`.

```bash
# Commit to the topmost branch
$ git add src/api.cs
$ graft stack commit -m "add API endpoint"

# Commit to a specific branch (stash/checkout/commit/return)
$ git add src/auth.cs
$ graft stack commit -m "fix auth bug" -b feature/auth

# Amend the latest commit on a branch
$ git add src/auth.cs
$ graft stack commit -m "fix auth bug" --amend -b feature/auth
```

### `graft stack sync [<branch>]`

Sync branches: fetch, merge each branch's parent into it (bottom-to-top), then push all updated branches. Optionally sync only the named branch.

```bash
# Sync entire stack
$ graft stack sync
Fetching...
Merging main → feature/auth... up-to-date.
Merging feature/auth → feature/api... done.
Pushing feature/api... done.

# Sync a single branch
$ graft stack sync feature/auth
```

### `graft stack log`

Show a visual graph of the active stack with branch relationships.

```bash
$ graft stack log
Stack: my-feature (trunk: main)
  main
   └── feature/auth    (2 commits)
        └── feature/api (1 commit)
```

### `graft stack remove <name>` (alias: `rm`)

Remove a stack. Git branches are kept. If the removed stack was active, the active stack is cleared.

```bash
$ graft stack remove my-feature
Removed stack 'my-feature'.
```

---

## Worktree Commands

Worktree paths follow the convention `../<reponame>.wt.<safebranch>/` where slashes in branch names are replaced with hyphens.

Example: branch `feature/api` in repo `Graft` → `../Graft.wt.feature-api/`

### `graft wt <branch> [-c/--create]`

Create a worktree for an existing branch. With `-c`, creates the branch first.

```bash
# Worktree for an existing branch
$ graft wt feature/auth
Created worktree at ../Graft.wt.feature-auth/

# Create new branch + worktree
$ graft wt feature/new-thing -c
Created branch 'feature/new-thing'.
Created worktree at ../Graft.wt.feature-new-thing/
```

### `graft wt remove <branch> [-f/--force]` (alias: `rm`)

Remove the worktree for the named branch. Fails if uncommitted changes exist unless `-f` is used. Also removes the worktree from the repo cache.

```bash
$ graft wt remove feature/auth
Removed worktree at ../Graft.wt.feature-auth/

# Force-remove even with uncommitted changes
$ graft wt remove feature/auth -f
```

### `graft wt list` (alias: `ls`)

List all worktrees of this repository.

```bash
$ graft wt list
/Users/dev/Graft                          main
/Users/dev/Graft.wt.feature-auth          feature/auth
/Users/dev/Graft.wt.feature-api           feature/api
```

---

## Scan Commands

Manage directories that Graft scans for git repositories. Discovered repos power `graft cd` and `graft status`.

### `graft scan add <directory>`

Register a directory to scan for git repositories.

```bash
$ graft scan add ~/dev/projects
Added scan path: /Users/dev/projects
```

### `graft scan remove <directory>` (alias: `rm`)

Remove a directory from the scan list.

```bash
$ graft scan remove ~/dev/projects
Removed scan path: /Users/dev/projects
```

### `graft scan list` (alias: `ls`)

List all registered scan paths.

```bash
$ graft scan list
/Users/dev/projects
/Users/dev/work
```

---

## Navigation

### `graft cd <name>`

Navigate to a discovered repo or worktree. Matches repo names first, then branch names for worktrees.

```bash
# Jump to a repo by name
$ graft cd my-app

# Jump to a worktree by branch name
$ graft cd feature/auth
# → ../Graft.wt.feature-auth/
```

If multiple matches exist, all are shown and you pick one. Works via a shell function since a child process can't change the parent's working directory.

---

## Status

### `graft status` (alias: `st`)

Show a compact overview of all discovered repos.

```bash
$ graft status
Graft  ~/dev/projects/Graft
  branch   main
  status   ✓ clean
  stack    auth-refactor (3 branches)
  worktrees  2 active

my-app  ~/dev/projects/my-app
  branch   feature/api
  status   ↑2 ↓1  3 changed
```

### `graft status <reponame>` (alias: `st`)

Show detailed status for a specific repo including stack graph and worktree list.

---

## Nuke Commands

Bulk cleanup operations. All require confirmation. Use `-f`/`--force` to override dirty checks on worktree removal.

### `graft nuke [-f/--force]`

Remove all worktrees, stacks, and gone branches.

### `graft nuke wt [-f/--force]`

Remove all worktrees.

### `graft nuke stack [-f/--force]`

Remove all stacks. Git branches are kept.

### `graft nuke branches`

Remove local branches whose remote tracking branch is gone. Uses `git branch -d` (safe delete — won't delete unmerged branches).

```bash
$ graft nuke branches
Deleted branch 'feature/old-thing' (was abc1234).
Deleted branch 'bugfix/resolved' (was def5678).
Removed 2 branches.
```

---

## Conflict Resolution

### `graft --continue`

Continue after resolving conflicts during a sync operation. Finishes the current merge and proceeds to remaining branches.

```bash
# After resolving conflicts:
$ git add src/conflicted-file.cs
$ graft --continue
Merge complete.
Merging feature/auth → feature/api... done.
Pushing feature/auth... done.
Pushing feature/api... done.
```

### `graft --abort`

Abort an in-progress sync operation. Aborts the merge and restores the original branch.

```bash
$ graft --abort
Aborted. Restored to 'feature/api'.
```

---

## Setup Commands

### `graft install`

Create the `gt` shortcut next to the `graft` binary and add `git gt` alias to your global git config.

```bash
$ graft install
Created symlink: gt → /usr/local/bin/graft
Added git alias: git gt → graft
```

### `graft uninstall`

Remove the `gt` shortcut and `git gt` alias.

### `graft update`

Check for updates and apply if available.

```bash
$ graft update
Checking for updates...
New version available: v0.3.0 (current: v0.2.0)
Downloading... done.
Update will be applied on next run.
```

### `graft version`

Print the current version.

```bash
$ graft version
graft 0.2.0
```

### `graft ui`

Start the web-based UI. See [Web UI](web-ui).

---

## Data Storage

### Per-Repository (`.git/graft/`)

| File | Purpose |
|------|---------|
| `active-stack` | Name of the active stack (plain text) |
| `stacks/<name>.toml` | Stack definition (branches, trunk, metadata) |
| `config.toml` | Repo-level settings |
| `operation.toml` | In-progress operation state (sync conflicts) |

Stack definition format:

```toml
name = "my-feature"
created_at = "2026-02-01T10:00:00Z"
trunk = "main"

[[branches]]
name = "feature/auth"

[[branches]]
name = "feature/api"
```

Branches are ordered bottom-to-top (index 0 is closest to trunk).

### Global (`~/.config/graft/`)

| File | Purpose |
|------|---------|
| `config.toml` | Global settings (scan paths) |
| `repo-cache.toml` | Discovered repos and worktrees cache |
| `update-state.toml` | Auto-update state (last check, pending update) |
| `staging/` | Downloaded update binaries |

---

## Error Handling

All errors include three parts:

1. **What** went wrong
2. **Why** (context)
3. **How to fix it** (numbered steps)

```
Error: Cannot push branch 'feature/api' — branch does not exist.
The branch must exist in git before adding it to a stack.
To resolve:
  1. Create the branch: git branch feature/api
  2. Or use: graft stack push -c feature/api
```

```
Error: No active stack.
No stack is currently selected, and there are 2 stacks in this repo.
To resolve:
  1. List stacks: graft stack list
  2. Switch: graft stack switch <name>
```
