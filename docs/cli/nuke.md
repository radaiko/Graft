---
layout: default
title: Nuke Commands
parent: CLI Reference
nav_order: 6
---

# Nuke Commands

Bulk cleanup operations for worktrees, stacks, and stale branches.

---

## Quick Reference

| Command | Description |
|---------|-------------|
| `graft nuke [-f]` | Remove all worktrees, stacks, and gone branches |
| `graft nuke wt [-f]` | Remove all worktrees |
| `graft nuke stack` | Remove all stacks |
| `graft nuke branches` | Remove branches whose upstream is gone |

---

## `graft nuke [-f/--force]`

Remove all worktrees, stacks, and gone branches. Always prompts for confirmation. Use `-f` to override dirty checks on worktree removal (confirmation is still required).

---

## `graft nuke wt [-f/--force]`

Remove all worktrees. Always prompts for confirmation. Fails if any worktree has uncommitted changes unless `-f` is used.

---

## `graft nuke stack`

Remove all stacks. Git branches are kept — only the stack metadata in `.git/graft/stacks/` is removed.

---

## `graft nuke branches`

Remove local branches whose remote tracking branch is gone. Uses `git branch -d` (safe delete — won't delete unmerged branches).

```bash
$ graft nuke branches
Deleted branch 'feature/old-thing' (was abc1234).
Deleted branch 'bugfix/resolved' (was def5678).
Removed 2 branches.
```

This is useful after squash-merging PRs on GitHub/GitLab, which deletes the remote branch but leaves the local one behind.

---

## Safety

- **All nuke commands prompt for confirmation.** The `--force` flag only overrides dirty checks (uncommitted changes in worktrees), not the confirmation prompt.
- **Stacks are metadata only.** `graft nuke stack` removes stack definitions but never deletes git branches.
- **Branch deletion is safe.** `graft nuke branches` uses `git branch -d`, which refuses to delete branches with unmerged commits.
- **Reversibility**: Worktree removal is reversible (re-create with `graft wt`). Stack removal is reversible (re-create with `graft stack init` + `push`). Branch deletion via `-d` is safe — unmerged branches are protected.

---

## See Also

- [Stack Commands](stack) — Manage individual stacks
- [Worktree Commands](worktree) — Manage individual worktrees
