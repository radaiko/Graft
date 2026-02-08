---
layout: default
title: CLI Reference
nav_order: 3
has_children: true
---

# CLI Reference

Complete command reference for the Graft CLI. Commands are grouped by function — click through to each page for detailed usage, examples, and tips.

---

## Command Groups

| Group | Commands | Description |
|-------|----------|-------------|
| [Stack](cli/stack) | `init`, `list`, `switch`, `push`, `pop`, `drop`, `shift`, `commit`, `sync`, `log`, `remove` | Create, manage, and sync stacked branches |
| [Worktree](cli/worktree) | `wt`, `wt remove`, `wt list` | Manage parallel checkouts with fixed naming |
| [Scan & Discovery](cli/scan) | `scan add`, `scan remove`, `scan list`, `scan auto-fetch` | Register directories, discover repos, background fetch |
| [Navigation](cli/navigation) | `cd` | Jump to repos and worktrees by name |
| [Status](cli/status) | `status` | Cross-repo overview of all discovered repos |
| [Nuke](cli/nuke) | `nuke`, `nuke wt`, `nuke stack`, `nuke branches` | Bulk cleanup operations |
| [Conflict Resolution](cli/conflict) | `--continue`, `--abort` | Handle merge conflicts during sync |
| [Setup](cli/setup) | `install`, `uninstall`, `update`, `version`, `ui` | Installation, aliases, and updates |

---

## Naming Conventions

Commands use **full names** as primary, with **short aliases** registered as hidden alternatives:

| Action | Full (primary) | Short (alias) |
|--------|---------------|---------------|
| Remove | `remove` | `rm` |
| List | `list` | `ls` |
| Switch | `switch` | `sw` |
| Commit | `commit` | `ci` |
| Status | `status` | `st` |

Options always have a long form primary with a short alias: `--force`/`-f`, `--create`/`-c`, `--message`/`-m`, `--base`/`-b`.

The binary name is `graft`. After running `graft install`, you can also use `gt` (symlink) or `git gt` (git alias). All three forms are interchangeable.

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
