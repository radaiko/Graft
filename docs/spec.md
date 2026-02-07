# Graft — Product Specification

> A cross-platform CLI tool for managing stacked branches and git worktrees.

---

## Overview

### Problem

Git's stacked branches and worktree features are powerful but painful to use:

- Stacked branches require manual tracking of dependencies and tedious cascade rebases
- Worktrees lack opinionated directory layouts and don't handle untracked config files (.env, IDE settings)
- Committing to a branch lower in a stack requires error-prone manual workflows

### Solution

Graft makes stacked branches and worktrees easy. It stores lightweight metadata alongside git and automates the tedious parts while staying close to native git concepts.

### Principles

1. **Git-native** — Enhance git, don't replace it. Every operation maps to git commands.
2. **Offline-first** — Core features work without network.
3. **Reversible** — Any operation is undoable or recoverable via git reflog.
4. **Cross-platform** — Windows, macOS, and Linux.

---

## Active Stack

Graft tracks which stack is "active". All stack operations (`push`, `pop`, `drop`, `shift`, `commit`, `sync`, `log`) act on the active stack — there is no `--stack` flag.

- **Stored as** plain text in `.git/graft/active-stack` (just the stack name, no TOML).
- **Auto-set on init**: `graft stack init <name>` sets the new stack as active.
- **Auto-select**: If no active-stack file exists and exactly one stack exists, that stack is automatically selected.
- **Cleared on delete**: If the active stack is deleted (`graft stack del`), the active-stack file is removed.
- **Switch with** `graft stack switch <name>`.

---

## Stacking Commands

### `graft stack init <name>`

Initialize a stack using the current branch as trunk and set it as active.

### `graft stack init <name> -b <branch>` / `--base <branch>`

Initialize a stack using the named branch as trunk and set it as active. The base branch must exist.

### `graft stack list`

List all stacks. The active stack is indicated.

### `graft stack switch <name>`

Switch the active stack. Fails if the named stack does not exist; the active stack stays untouched.

### `graft stack push <branch>`

Add an existing branch to the top of the active stack. The branch is checked out.

### `graft stack push -c <branch>` / `--create <branch>`

Create a new local branch and add it to the top of the active stack. Fails if the branch already exists. The branch is checked out.

### `graft stack pop`

Remove the topmost branch from the active stack. The git branch is kept.

### `graft stack drop <branch>`

Remove the named branch from the active stack regardless of position. The git branch is kept.

### `graft stack shift <branch>`

Insert an existing branch at the bottom of the active stack (directly above the trunk). The branch must exist in git and not already be in the stack.

### `graft stack commit -m "<message>"`

Commit git staged changes to the topmost branch of the active stack.

### `graft stack commit -m "<message>" -b <branch>`

Commit git staged changes to the named branch. Uses stash/checkout/pop/commit/return flow. Branches above the target become stale.

### `graft stack commit -m "<message>" --amend`

Amend the latest commit on the target branch instead of creating a new one. Can be combined with `-b`.

### `graft stack sync`

Sync all branches in the active stack:

1. `git fetch --quiet`
2. Merge each branch's parent into it, bottom-to-top (trunk into the first branch, previous branch into the rest)
3. Skip branches that are already up-to-date
4. On conflict: save operation state, print conflicting files, exit
5. After all merges succeed: `git push origin <branch>` for each updated branch

### `graft stack sync <branch>`

Sync only the named branch. Determines the correct parent (trunk if it's the first branch, otherwise the branch below it), merges parent into the branch, then pushes.

### `graft stack log`

Show a visual graph of the active stack with commit counts and rebase status.

### `graft stack del <name>` / `graft stack del <name> -f` / `--force`

Delete a stack by name. The git branches are kept. If the deleted stack was active, the active-stack file is cleared.

---

## Worktree Commands

Worktree paths follow the convention `../<reponame>.wt.<safebranch>/` where slashes in branch names are replaced with hyphens (e.g. `feature/api` → `myrepo.wt.feature-api`).

### `graft wt <branch>`

Create a worktree from an existing branch.

### `graft wt <branch> -c` / `--create`

Create a new git branch and a worktree for it. Fails if the branch already exists.

### `graft wt del <branch>`

Delete the worktree for the named branch. Fails if the worktree has uncommitted changes.

### `graft wt del <branch> -f` / `--force`

Force-delete the worktree even if it has uncommitted changes.

### `graft wt list`

List all worktrees of this repo.

### `graft wt goto <branch>`

Print the worktree path for the named branch, suitable for use with `cd`. In a shell, use `cd $(graft wt goto <branch>)` to jump into a worktree directory.

---

## Nuke Commands

### `graft nuke`

Remove all worktrees, stacks, and gone branches (in that order). Worktrees with uncommitted changes are skipped unless `-f`/`--force` is used. Confirmation required.

### `graft nuke wt`

Remove all worktrees. Worktrees with uncommitted changes are skipped unless `-f`/`--force` is used. Confirmation required.

### `graft nuke stack`

Remove all stacks. The active-stack file is cleared. Confirmation required.

### `graft nuke branches`

Remove local branches whose remote tracking branch is gone (`git branch -vv` shows `[origin/...: gone]`). Uses `git branch -d` (safe delete — branch must be fully merged). The current branch is never deleted. Confirmation required.

---

## Common Commands

### `graft update`

Check for and apply updates.

### `graft version`

Print version.

### `graft ui`

Start the graft web UI (browser-based).

### `graft install`

Create `gt` symlink and `git gt` alias.

### `graft uninstall`

Remove aliases.

### `graft --continue`

Continue after resolving a sync conflict. Finishes the paused merge and continues to remaining branches.

### `graft --abort`

Abort an in-progress sync. Aborts the merge, restores the original branch, and deletes operation state.

---

## Detailed Specifications

| Spec | Description |
|------|-------------|
| [Data Storage](spec/data-storage.md) | All TOML schemas, file layouts, name validation rules. |
| [Error Handling](spec/error-handling.md) | Error format, conflict resolution flows, recovery scenarios. |
| [Installation and Updates](spec/installation-and-updates.md) | Alias implementation, auto-update lifecycle. |
| [Web UI](spec/ui.md) | Browser-based UI: architecture, API, views, state management. |

---

## Conflict Handling

### Design Principle

**`sync` is the only command that merges.** All other commands (`push`, `pop`, `drop`, `shift`, `commit`) only modify the stack definition or commit changes — they never trigger merges. This means conflicts can only occur during `sync`, and there is exactly one conflict flow to learn.

- `push`, `pop`, `drop`, `shift` — modify stack metadata only. Branches above a removed or inserted branch become "stale" until the next `sync`.
- `commit -b <branch>` — stashes staged changes, checks out target branch, pops stash, commits, returns to original branch. Branches above become stale.
- `sync` — the "make everything consistent" command. Fetches remote, merges each branch's parent into it bottom-to-top, pushes to remote. **This is where conflicts happen.**

### Sync Conflict Flow

When `graft stack sync` merges branch-by-branch (bottom-to-top) and hits a conflict:

1. The merge is left in progress on the conflicting branch.
2. Graft writes `operation.toml` to `.git/graft/` with the stack name, branch index, and original HEAD.
3. Graft prints the conflicting files and instructions, then exits.
4. The user resolves conflicts with standard git tools.
5. `graft --continue` finishes that merge and continues to remaining branches.
6. `graft --abort` aborts the merge, restores the original branch, and deletes operation state.

If another branch conflicts after `--continue`, the cycle repeats.

### Commit Stash Failure

`graft stack commit -b <branch>` uses stash/checkout/pop to commit to a non-top branch. If the stash won't apply cleanly on the target branch, the operation aborts immediately. The error message includes the stash SHA so the user can recover their changes with `git stash pop <sha>`.

### Examples

#### Example 1: Sync conflict on a single branch

You have a stack `feature` with base `main` (protected) and branches `feature/api` → `feature/ui`. A teammate merged a PR into `main` that changed `src/api/handler.cs` — the same file `feature/api` touches.

**Before sync — git state:**

```
main:        A --- B --- C (C is the teammate's merged PR)
                \
feature/api:     D --- E --- F
                              \
feature/ui:                    G --- H
```

`feature/api` was branched from `main` at commit B. Commit C on `main` is new.

```
$ graft stack sync
Syncing stack 'feature'...
  fetching origin...
  merging main into feature/api...
  ✗ conflict in src/api/handler.cs

Conflicting files:
  - src/api/handler.cs

Fix conflicts, then: graft --continue
Or abort:             graft --abort
```

**What happened in git:** Graft ran `git merge main` while on `feature/api`. Git is merging commit C from `main` into `feature/api`. The changes in C conflict with changes in D/E/F — git can't auto-merge, so it pauses the merge.

**Important: `main` is not modified.** The merge is INTO `feature/api`, not the other way around. Even if `main` is protected, this is fine — graft never pushes to `main`.

You open `src/api/handler.cs`, resolve the conflict markers, and stage:

```
$ vim src/api/handler.cs
$ git add src/api/handler.cs
```

`git add` does **not** create a commit. It tells git "I've resolved this file." The merge is still paused.

```
$ graft --continue
```

Internally, `graft --continue` runs `git merge --continue`. Git creates the merge commit on `feature/api`, incorporating both `main`'s changes and the conflict resolution. Then graft moves on to the next branch.

```
  continuing merge into feature/api...
  ✓ feature/api (merged)
  merging feature/api into feature/ui...
  ✓ feature/ui (merged)
  pushing feature/api...
  pushing feature/ui...
Done.
```

**After sync — git state:**

```
main:        A --- B --------- C
                \                \
feature/api:     D --- E --- F --- M1
                              \     \
feature/ui:                    G --- H --- M2
```

M1 merges `main` into `feature/api`. M2 merges `feature/api` (which now includes `main`'s changes) into `feature/ui`. All branches are up to date. No force push needed.

Since PRs are squash-merged, the merge commits (M1, M2) disappear when the PR lands on `main`.

`main` was never modified. The merge only added commits to `feature/api` and `feature/ui`.

#### Example 2: Conflicts on multiple branches in a row

Same stack, but both branches conflict during sync.

```
$ graft stack sync
Syncing stack 'feature'...
  fetching origin...
  merging main into feature/api...
  ✗ conflict in src/api/handler.cs

Conflicting files:
  - src/api/handler.cs

Fix conflicts, then: graft --continue
Or abort:             graft --abort
```

Resolve and continue:

```
$ vim src/api/handler.cs
$ git add src/api/handler.cs
$ graft --continue
  continuing merge into feature/api...
  ✓ feature/api (merged)
  merging feature/api into feature/ui...
  ✗ conflict in src/ui/dashboard.svelte

Conflicting files:
  - src/ui/dashboard.svelte

Fix conflicts, then: graft --continue
Or abort:             graft --abort
```

Resolve the second conflict and continue:

```
$ vim src/ui/dashboard.svelte
$ git add src/ui/dashboard.svelte
$ graft --continue
  continuing merge into feature/ui...
  ✓ feature/ui (merged)
  pushing feature/api...
  pushing feature/ui...
Done.
```

#### Example 3: Aborting a sync

You hit a conflict but decide you don't want to deal with it right now.

```
$ graft stack sync
Syncing stack 'feature'...
  fetching origin...
  merging main into feature/api...
  ✗ conflict in src/api/handler.cs

Conflicting files:
  - src/api/handler.cs

Fix conflicts, then: graft --continue
Or abort:             graft --abort

$ graft --abort
Aborting sync. Restored to branch 'feature/ui'.
```

Everything is back to how it was before the sync. You can retry later with `graft stack sync`.

#### Example 4: Stale branches after drop

You drop a branch from the middle of the stack. The branches above it are now stale (still based on the dropped branch's commits). Sync fixes it.

```
$ graft stack log
main
├── feature/api (3 commits)
├── feature/middleware (1 commit)
└── feature/ui (2 commits)  ← HEAD

$ graft stack drop feature/middleware
Dropped 'feature/middleware' from stack 'feature'.

$ graft stack log
main
├── feature/api (3 commits)
└── feature/ui (2 commits, stale)  ← HEAD

$ graft stack sync
Syncing stack 'feature'...
  fetching origin...
  ✓ feature/api (up to date)
  merging feature/api into feature/ui...
  ✓ feature/ui (merged)
  pushing feature/api...
  pushing feature/ui...
Done.
```

#### Example 5: Commit to a lower branch, then sync

You commit to a branch that isn't at the top. The branches above become stale until you sync.

```
$ graft stack log
main
├── feature/api (3 commits)
└── feature/ui (2 commits)  ← HEAD

$ git add src/api/types.cs
$ graft stack commit -m "Add request types" -b feature/api
Committed to feature/api (abc1234).
Branches above are stale. Run 'graft stack sync' to update.

$ graft stack sync
Syncing stack 'feature'...
  fetching origin...
  ✓ feature/api (up to date)
  merging feature/api into feature/ui...
  ✓ feature/ui (merged)
  pushing feature/api...
  pushing feature/ui...
Done.
```
