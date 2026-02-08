---
layout: default
title: Home
nav_order: 1
---

# Graft

**A CLI tool for managing stacked branches and git worktrees.**

Built for **squash-merge PR workflows**. Split large changes into dependent branches that stay in sync automatically — using merge commits, never rebase. Your history is never rewritten, no force push needed.

```
main
 └── feature/auth-types         ← PR #1 → main
      └── feature/auth-session   ← PR #2 → feature/auth-types
           └── feature/auth-api   ← PR #3 → feature/auth-session
```

Each branch is its own PR. Each PR shows only its own changes. `graft stack sync` keeps them all up to date in one command.

---

## Install

### macOS / Linux

```bash
curl -fsSL https://raw.githubusercontent.com/radaiko/Graft/main/install.sh | bash
```

### Windows (PowerShell)

```powershell
irm https://raw.githubusercontent.com/radaiko/Graft/main/install.ps1 | iex
```

The installer places `graft` in `~/.local/bin` and sets up two shortcuts: `gt` (symlink) and `git gt` (git alias). All three forms are interchangeable.

Graft is a single native binary — no runtime, no dependencies.

---

## Quick Start

```bash
# Create a stack on your current branch (e.g. main)
gt stack init my-feature

# Create the first branch in the stack
gt stack push -c feature/base-types
# ... write code, stage with git add ...
gt stack commit -m "add base types"

# Create a second branch on top
gt stack push -c feature/api-layer
# ... write code, stage with git add ...
gt stack commit -m "add API layer"

# See the stack
gt stack log
#  main
#   └── feature/base-types  (1 commit)
#        └── feature/api-layer  (1 commit)

# Sync: merge main → base-types → api-layer, then push all
gt stack sync
```

When `feature/base-types` gets squash-merged into `main`:

```bash
gt stack drop feature/base-types
gt stack sync    # merges main (now containing base-types) into api-layer
```

Update `feature/api-layer`'s PR to target `main`. Done.

---

## Why Graft?

When feature B depends on feature A (still in review), most teams hit the same wall:

| Approach | Problem |
|----------|---------|
| Wait for A to merge | Blocks your work |
| Branch B from A | PR shows both A and B's changes |
| Merge main into A, then branch | Painful conflicts after squash merge |

Graft fixes this. Each branch targets the one below it, so each PR shows only its own diff. `sync` propagates changes through the stack automatically.

**Merge, not rebase.** Graft uses merge commits to keep branches in sync. Since your team squash-merges PRs anyway, those merge commits disappear from `main`. No force push, no rewritten history, safe for teams.

---

## What Can Graft Do?

### Stack Management

| Command | Description |
|---------|-------------|
| `gt stack init <name>` | Create a new stack on the current branch |
| `gt stack push -c <branch>` | Create a branch and add it to the top of the stack |
| `gt stack log` | Display a visual graph of the stack |
| `gt stack sync` | Merge the entire stack bottom-to-top, then push |
| `gt stack commit -m <msg> [-b <branch>]` | Commit staged changes to any branch in the stack |
| `gt stack remove <name>` | Remove a stack definition (git branches are kept) |

### Worktree Management

| Command | Description |
|---------|-------------|
| `gt wt <branch>` | Create a worktree for an existing branch |
| `gt wt <branch> -c` | Create a new branch + worktree |
| `gt wt remove <branch>` | Remove a worktree |
| `gt wt list` | List all worktrees |

### Setup

| Command | Description |
|---------|-------------|
| `gt install` | Create `gt` symlink and `git gt` alias |
| `gt update` | Check for and apply updates |
| `gt version` | Print current version |
| `gt ui` | Open the browser-based [web UI](web-ui) |

---

## Supported Platforms

| OS | Architectures | Format |
|----|---------------|--------|
| macOS | Intel (x64), Apple Silicon (arm64) | Single native binary |
| Linux | x64, arm64 | Single native binary |
| Windows | x64, arm64 | Single native binary |

No .NET runtime required — Graft compiles to a native AOT binary.

---

## Links

- [Workflow Guide](workflow) — Full stacked branches walkthrough
- [CLI Reference](cli-reference) — Complete command reference
- [Web UI](web-ui) — Browser-based interface
- [FAQ](faq) — Common questions
- [Changelog](changelog) — What changed in each version
- [Source Code](https://github.com/radaiko/Graft)
