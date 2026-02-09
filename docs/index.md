---
title: Home
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

## Install

### macOS / Linux

```bash
curl -fsSL https://raw.githubusercontent.com/radaiko/Graft/main/install.sh | bash
```

### Windows (PowerShell)

```powershell
irm https://raw.githubusercontent.com/radaiko/Graft/main/install.ps1 | iex
```

The installer places `graft` in `~/.local/bin` and runs `graft install`, which sets up shortcuts (`gt`, `git gt`) and shell integration for [`graft cd`](./cli/navigation.md).

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

## Feature Overview

### Stack Management

| Command | Description |
|---------|-------------|
| [`gt stack init`](./cli/stack.md#graft-stack-init-name--b--base-branch) | Create a new stack on the current branch |
| [`gt stack push -c`](./cli/stack.md#graft-stack-push-branch--c--create) | Create a branch and add it to the top of the stack |
| [`gt stack log`](./cli/stack.md#graft-stack-log) | Display a visual graph of the stack |
| [`gt stack sync`](./cli/stack.md#graft-stack-sync-branch) | Merge the entire stack bottom-to-top, then push |
| [`gt stack commit`](./cli/stack.md#graft-stack-commit---message-message--b-branch---amend-alias-ci) | Commit staged changes to any branch in the stack |
| [`gt stack remove`](./cli/stack.md#graft-stack-remove-name--f--force-alias-rm) | Remove a stack definition (git branches are kept) |

### Worktree Management

| Command | Description |
|---------|-------------|
| [`gt wt <branch>`](./cli/worktree.md#graft-wt-branch--c--create) | Create a worktree for an existing branch |
| [`gt wt <branch> -c`](./cli/worktree.md#graft-wt-branch--c--create) | Create a new branch + worktree |
| [`gt wt remove`](./cli/worktree.md#graft-wt-remove-branch--f--force-alias-rm) | Remove a worktree |
| [`gt wt list`](./cli/worktree.md#graft-wt-list-alias-ls) | List all worktrees |

### Repo Discovery & Navigation

| Command | Description |
|---------|-------------|
| [`gt scan add`](./cli/scan.md#graft-scan-add-directory) | Register a directory for repo scanning |
| [`gt scan auto-fetch`](./cli/scan.md#auto-fetch-commands) | Enable/disable background git fetch per repo |
| [`gt cd <name>`](./cli/navigation.md#graft-cd-name) | Jump to any repo or worktree by name |
| [`gt status`](./cli/status.md#graft-status-alias-st) | Cross-repo overview of all discovered repos |

### Setup

| Command | Description |
|---------|-------------|
| [`gt install`](./cli/setup.md#graft-install) | Create `gt` symlink and `git gt` alias |
| [`gt update`](./cli/setup.md#graft-update) | Check for and apply updates |
| [`gt version`](./cli/setup.md#graft-version) | Print current version |
| [`gt ui`](./cli/setup.md#graft-ui) | Open the browser-based [web UI](./web-ui.md) |

---

## Supported Platforms

| OS | Architectures | Format |
|----|---------------|--------|
| macOS | Intel (x64), Apple Silicon (arm64) | Single native binary |
| Linux | x64, arm64 | Single native binary |
| Windows | x64, arm64 | Single native binary |

No .NET runtime required — Graft compiles to a native AOT binary.

---

## Learn More

- [Workflow Guide](./workflow.md) — Full stacked branches walkthrough
- [CLI Reference](./cli-reference.md) — Complete command reference
  - [Stack Commands](./cli/stack.md) — Create, manage, and sync stacked branches
  - [Worktree Commands](./cli/worktree.md) — Parallel checkouts with fixed naming
  - [Scan & Discovery](./cli/scan.md) — Repo scanning and auto-fetch
  - [Navigation](./cli/navigation.md) — Jump to repos and worktrees
  - [Status](./cli/status.md) — Cross-repo status overview
  - [Nuke Commands](./cli/nuke.md) — Bulk cleanup operations
  - [Conflict Resolution](./cli/conflict.md) — Handle merge conflicts during sync
  - [Setup Commands](./cli/setup.md) — Install, update, and aliases
- [Web UI](./web-ui.md) — Browser-based interface
- [FAQ](./faq.md) — Common questions
- [Changelog](./changelog.md) — What changed in each version
- [Source Code](https://github.com/radaiko/Graft)
