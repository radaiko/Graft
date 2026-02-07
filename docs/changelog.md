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

- **VS Extension:** Visual Studio 2022/2026 extension project scaffolding (Phase 1)
- **VS Extension:** Stack Explorer tool window with TreeView showing stacks and branches
- **VS Extension:** Status bar integration displaying active stack and top branch
- **VS Extension:** Tools > Graft menu with Init, Push, Pop, Sync, Switch, Log, and Open Stack Explorer commands
- **VS Extension:** CLI wrapper service with binary detection and async command execution
- **VS Extension:** Direct TOML file reading for stack data (no CLI parsing for display)
- **VS Extension:** FileSystemWatcher with debouncing for live `.git/graft/` change detection
- **VS Extension:** Dedicated "Graft" output window pane for command logging
- **VS Extension:** Reusable input dialog with TextBox/ComboBox and optional checkbox

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
