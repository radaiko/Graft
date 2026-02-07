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

### Changed

### Removed
- **Web UI:** Remove unused settings page

### Fixed
- **Core:** `graft stack sync` no longer fails when a stack branch has an active worktree (#3)

## [0.2.0] -- Initial public release

### Added

- **CLI:** Stack management -- `graft stack init`, `push`, `pop`, `drop`, `shift`, `commit`, `sync`, `log`, `del`
- **CLI:** Active stack tracking -- set once with `graft stack init` or `graft stack switch`, all commands use it automatically
- **CLI:** Worktree management -- `graft wt` to create, delete, list, and jump into worktrees
- **CLI:** Nuke operations -- bulk cleanup of worktrees, stacks, and gone branches
- **CLI:** Auto-update with background checking and staged binary replacement
- **CLI:** Cross-platform native AOT binaries for Linux, macOS, and Windows (x64 + arm64)
- **Web UI:** Browser-based interface for stacks, worktrees, and nuke operations
