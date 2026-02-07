---
layout: default
title: CLI
parent: Changelog
nav_order: 1
---

# CLI Changelog

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

- Stack management -- `graft stack init`, `push`, `pop`, `drop`, `shift`, `commit`, `sync`, `log`, `del`
- Active stack tracking -- set once with `graft stack init` or `graft stack switch`, all commands use it automatically
- Worktree management -- `graft wt` to create, delete, list, and jump into worktrees
- Nuke operations -- bulk cleanup of worktrees, stacks, and gone branches
- Auto-update with background checking and staged binary replacement
- Cross-platform native AOT binaries for Linux, macOS, and Windows (x64 + arm64)
- Browser-based web UI for stacks, worktrees, and nuke operations
