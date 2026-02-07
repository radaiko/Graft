---
layout: default
title: Changelog
nav_order: 6
---

# Changelog

All Graft CLI releases. Download binaries from the [GitHub Releases](https://github.com/radaiko/Graft/releases) page.

---

## [Unreleased] -- Next

### Added
- **VS Code Extension:** Activity bar with tree view showing all stacks, trunk, and branches with sync status icons
- **VS Code Extension:** 13 command palette commands -- init, switch, push, pop, drop, sync, commit, delete, continue, abort, checkout, open PR, refresh
- **VS Code Extension:** Status bar showing active stack name and sync status (synced/stale/conflict)
- **VS Code Extension:** File watcher on `.git/graft/` with 300ms debounce for automatic tree refresh
- **VS Code Extension:** Context menus on stacks (push, pop, sync, commit, delete) and branches (drop, checkout, open PR)
- **VS Code Extension:** `graft.cliPath` setting for custom CLI binary location

### Changed

### Removed
- **Web UI:** Remove unused settings page

### Fixed

## [0.2.0] -- Initial public release

### Added

- **CLI:** Stack management -- `graft stack init`, `push`, `pop`, `drop`, `shift`, `commit`, `sync`, `log`, `del`
- **CLI:** Active stack tracking -- set once with `graft stack init` or `graft stack switch`, all commands use it automatically
- **CLI:** Worktree management -- `graft wt` to create, delete, list, and jump into worktrees
- **CLI:** Nuke operations -- bulk cleanup of worktrees, stacks, and gone branches
- **CLI:** Auto-update with background checking and staged binary replacement
- **CLI:** Cross-platform native AOT binaries for Linux, macOS, and Windows (x64 + arm64)
- **Web UI:** Browser-based interface for stacks, worktrees, and nuke operations
