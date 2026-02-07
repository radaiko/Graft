# Error Handling

All errors tell the user: **what** went wrong, **why**, and **how to fix it**.

---

## Error Message Format

Every error includes three parts:

```
Error: <what happened>

<context / why>

To resolve:
  <numbered steps>
```

### Example: Rebase Conflict

```
Error: Rebase conflict in 'auth/session-manager'

Conflicting files:
  - src/session.cs

To resolve:
  1. Fix conflicts in the files above
  2. Stage resolved files: git add <file>
  3. Continue: graft --continue

To abort: graft --abort
```

### Example: Missing Branch

```
Error: Branch 'auth/base-types' in stack 'auth-refactor' no longer exists.

Restore it with 'git branch auth/base-types <commit>', or remove the stack
with 'graft stack del auth-refactor' and recreate it.
```

### Example: No Staged Changes

```
Error: No staged changes to commit
```

### Example: No Active Stack

```
Error: No active stack set. Run 'graft stack switch <name>' to select one.
```

---

## Conflict Resolution

Conflicts can occur during `graft stack sync` when rebasing branches.

### During Sync

When `graft stack sync` encounters a conflict:

1. The rebase is left in progress on the conflicting branch.
2. An `operation.toml` file is written recording the stack name, branch index, and original branch (see [Data Storage](data-storage.md)).
3. Graft reports the conflicting files and exits.
4. The user resolves conflicts manually, stages files with `git add`.
5. `graft --continue` finishes the rebase and continues cascading to remaining branches.
6. If another conflict occurs, the process repeats.
7. `graft --abort` aborts the rebase and returns to the original branch.

### During Commit

When `graft stack commit` targets a branch that is not the topmost in the stack, the commit succeeds but branches above become stale. Run `graft stack sync` to rebase them.

---

## Recovery Scenarios

### Stash Recovery

If `graft stack commit` fails after stashing staged changes (e.g., checkout fails), the error message includes the stash reference:

```
Commit failed. Your staged changes are preserved in git stash (abc123).
Run 'git stash pop abc123' to recover them.
```

The stash reference is resolved to a SHA (not a positional index like `stash@{0}`) so it remains stable even if other stash operations occur.

### Branch Restoration After Abort

`graft --abort` restores the user to their original branch:

1. Aborts any in-progress rebase (`git rebase --abort`).
2. Reads the original branch from `operation.toml`.
3. Checks out the original branch.
4. Deletes `operation.toml`.

If the checkout fails, the error reports which branch the user is on and where they intended to be.

### Update Rollback

If the auto-update binary replacement fails mid-operation:

1. The old binary was renamed to `<path>.bak` before replacement.
2. If the new binary fails to move into place, the backup is restored automatically.
3. If both the move and the rollback fail, the error tells the user where the backup file is and how to manually restore it:

```
Update failed and rollback also failed. Backup is at '/usr/local/bin/graft.bak'.
Manually rename it to '/usr/local/bin/graft' to recover.
```

### Corrupt Operation State

If `operation.toml` has invalid TOML or missing fields:

```
Operation state file is corrupt: .git/graft/operation.toml
Delete it and run 'graft --abort' to clean up.
```

### Checksum Mismatch

If a staged update binary fails checksum verification:

1. The staged binary is deleted.
2. The pending update is cleared from `update-state.toml`.
3. An error is raised. The next invocation will re-check for updates.

---

## Operation Safety

### Atomicity

- All TOML file writes use atomic rename (write to temp file, then `File.Move` with overwrite).
- Sync saves operation state before starting, enabling recovery via `--continue` or `--abort`.

### Head Restoration

Sync and commit record the user's original branch (resolved to SHA if in detached HEAD state) before switching branches. On success or abort, Graft returns the user to that branch.

### Staged Binary Validation

Before applying an update, the binary path is validated to be within the expected `staging/` directory. Paths outside this directory are rejected and the pending update is cleared.
