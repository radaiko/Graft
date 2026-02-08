---
layout: default
title: Worktree Commands
parent: CLI Reference
nav_order: 2
---

# Worktree Commands

Manage git worktrees with a fixed naming convention. Worktrees let you have multiple branches checked out in separate directories simultaneously — no need to stash and switch.

---

## Quick Reference

| Command | Description |
|---------|-------------|
| `graft wt <branch>` | Create worktree for existing branch |
| `graft wt <branch> -c` | Create new branch + worktree |
| `graft wt remove <branch> [-f]` | Remove a worktree |
| `graft wt list` | List all worktrees |

---

## Path Convention

Worktree paths follow a fixed convention:

```
../<repoName>.wt.<safeBranch>/
```

Slashes in branch names are replaced with hyphens. For example:

| Repo | Branch | Worktree Path |
|------|--------|--------------|
| `Graft` | `feature/api` | `../Graft.wt.feature-api/` |
| `Graft` | `bugfix/auth` | `../Graft.wt.bugfix-auth/` |
| `my-app` | `main` | `../my-app.wt.main/` |

Worktrees are always created as siblings of the main repo directory.

---

## `graft wt <branch> [-c/--create]`

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

New worktrees are automatically added to the [repo cache](scan) so they appear in `graft cd` and `graft status`.

---

## `graft wt remove <branch> [-f/--force]` (alias: `rm`)

Remove the worktree for the named branch. Prompts for confirmation. Fails if uncommitted changes exist unless `-f` is used to override dirty checks. Also removes the worktree from the repo cache.

```bash
$ graft wt remove feature/auth
Removed worktree at ../Graft.wt.feature-auth/

# Force-remove even with uncommitted changes
$ graft wt remove feature/auth -f
```

---

## `graft wt list` (alias: `ls`)

List all worktrees of this repository.

```bash
$ graft wt list
/Users/dev/Graft                          main
/Users/dev/Graft.wt.feature-auth          feature/auth
/Users/dev/Graft.wt.feature-api           feature/api
```

---

## Tips

- **Jump to worktrees**: Use `graft cd <branch>` to navigate to a worktree by branch name. See [Navigation](navigation).
- **Worktrees + stacks**: Worktrees are independent of stacks. You can create a worktree for any branch, whether or not it's part of a stack.
- **Bulk remove**: `graft nuke wt` removes all worktrees at once. See [Nuke Commands](nuke).

---

## See Also

- [Navigation](navigation) — Jump to worktrees with `graft cd`
- [Scan & Discovery](scan) — Worktrees are auto-registered in the repo cache
- [Nuke Commands](nuke) — Bulk remove all worktrees
