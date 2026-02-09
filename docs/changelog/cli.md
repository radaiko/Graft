---
title: CLI
---

# CLI Changelog

All Graft CLI releases. Download binaries from the [GitHub Releases](https://github.com/radaiko/Graft/releases) page.

---

## [0.3.2]

### Added
- **CLI:** `graft install` now writes shell wrapper functions to the user's profile (`~/.zshrc`, `~/.bashrc`, `config.fish`, `$PROFILE`) so `graft cd` / `gt cd` actually changes the shell's working directory (#42)
- **CLI:** `graft uninstall` cleanly removes the injected shell wrapper block (#42)
- **CLI:** ANSI color output throughout `graft status` — bold names, cyan branches, green/red sync indicators, yellow changes, magenta stacks, blue worktrees (#43)

### Changed
- **CLI:** `graft status` overview condensed to 1 line per repo with colored badges for at-a-glance scanning (#43)
- **CLI:** `graft status` respects `NO_COLOR` env var and disables colors when output is piped (#43)

## [0.3.1]

### Added
- **CLI:** `graft scan update` — fetch all cached repos once, regardless of auto-fetch settings (#35)
- **CLI:** `graft scan add` now triggers an immediate scan so repos are discoverable right away (#36)

### Fixed
- **TUI:** Fix interactive picker jumping higher on each keystroke due to incorrect cursor-up at start of render (#37)
- **Tests:** Fix scan integration tests polluting global `~/.config/graft/config.toml` with temp directories (#40)

## [0.3.0]

### Added
- **Core:** Repo scanner — register directories for automatic git repo discovery (`graft scan add/remove/list`) (#18)
- **Core:** Repo cache — discovered repos cached in `~/.config/graft/repo-cache.toml` with background scanning on every invocation
- **CLI:** `graft cd <name>` — unified navigation to repos and worktrees by name or branch
- **CLI:** `graft cd` (no args) — interactive fuzzy-search picker for all cached repos
- **Core:** Fuzzy matcher for interactive filtering with subsequence scoring (consecutive, word-boundary, case bonuses)
- **CLI:** Worktree create/remove now automatically updates the repo cache for `graft cd`
- **CLI:** `graft status` — cross-repo status overview (branch, ahead/behind, changed files, stacks, worktrees) (#20)
- **CLI:** `graft status <reponame>` — detailed status for a single repo with stack branch graph (#20)
- **Core:** Auto-fetch — background `git fetch --all` for repos with auto-fetch enabled, rate-limited to 15-minute intervals per repo (#19)
- **CLI:** `graft scan auto-fetch enable [<name>]` — enable auto-fetch for a repo (by name or current directory)
- **CLI:** `graft scan auto-fetch disable [<name>]` — disable auto-fetch for a repo
- **CLI:** `graft scan auto-fetch list` — list all repos with their auto-fetch status and last fetch time

### Changed
- **CLI:** Renamed `graft stack del` → `graft stack remove` with hidden `rm` alias
- **CLI:** Renamed `graft wt del` → `graft wt remove` with hidden `rm` alias
- **CLI:** Added hidden short aliases: `stack ls`, `stack sw`, `stack ci`, `wt ls`
- **CLI:** `graft stack commit` now accepts `--message`/`-m` (previously `-m` only)

### Deprecated
- **CLI:** `graft stack del` — use `graft stack remove` instead (command is now hidden, prints deprecation warning)
- **CLI:** `graft wt del` — use `graft wt remove` instead (command is now hidden, prints deprecation warning)
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
