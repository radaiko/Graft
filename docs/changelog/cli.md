---
layout: default
title: CLI
parent: Changelog
nav_order: 1
---

# CLI Changelog

All Graft CLI releases. Download binaries from the [GitHub Releases](https://github.com/radaiko/Graft/releases) page.

---

## [0.3.0]

### Added
- **Core:** Repo scanner — register directories for automatic git repo discovery (`graft scan add/remove/list`) (#18)
- **Core:** Repo cache — discovered repos cached in `~/.config/graft/repo-cache.toml` with background scanning on every invocation
- **CLI:** `graft cd <name>` — unified navigation to repos and worktrees by name or branch
- **CLI:** `graft cd` (no args) — interactive fuzzy-search picker for all cached repos
- **Core:** Fuzzy matcher for interactive filtering with subsequence scoring (consecutive, word-boundary, case bonuses)
- **CLI:** Worktree create/remove now automatically updates the repo cache for `graft cd`

### Deprecated
- **CLI:** `graft wt goto` — use `graft cd` instead (command is now hidden, prints deprecation warning)

## [0.2.2]

### Fixed
- **Core:** `graft nuke branches` now uses force delete (`git branch -D`) so branches whose upstream is gone are actually removed (#13)
- **Core:** `graft nuke branches` correctly filters out branches checked out in worktrees (git 2.36+ `+` prefix)

## [0.2.1]

### Changed
- **CLI:** `--force` now only overrides dirty checks, never skips confirmation prompts
- **CLI:** All destructive commands (`nuke`, `wt del`, `stack del`) always prompt for confirmation

### Fixed
- **Core:** `graft stack sync` no longer fails when a stack branch has an active worktree (#3)

### Removed
- **CLI:** Removed unused `--force` flag from `graft stack del` (no dirty checks to override)
- **Web UI:** Remove unused settings page


## [0.2.0] -- Initial public release

### Added

- **Core:** Stack management -- `graft stack init`, `push`, `pop`, `drop`, `shift`, `commit`, `sync`, `log`, `del`
- **Core:** Active stack tracking -- set once with `graft stack init` or `graft stack switch`, all commands use it automatically
- **Core:** Worktree management -- `graft wt` to create, delete, list, and jump into worktrees
- **Core:** Nuke operations -- bulk cleanup of worktrees, stacks, and gone branches
- **Core:** Auto-update with background checking and staged binary replacement
- **Build:** Cross-platform native AOT binaries for Linux, macOS, and Windows (x64 + arm64)
- **Web UI:** Browser-based web UI for stacks, worktrees, and nuke operations
