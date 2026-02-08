# Graft — Project Guide

## What Is This

Graft is a cross-platform CLI tool (C# / .NET 10 / Native AOT) for managing stacked branches and git worktrees. Product requirements live in `docs/spec.md`. This file covers how the project is built.

## Architecture

Solution file at `src/Graft.sln`. Shared build properties in `Directory.Build.props` at repo root.

- `src/Graft.Core/` — Core library. Stack management, worktree management, commit routing, nuke operations, git abstraction. No CLI dependencies. Marked `IsAotCompatible`.
- `src/Graft.Cli/` — CLI binary (`graft`). Uses System.CommandLine. Grouped commands: `stack` (init, list, switch, push, pop, drop, shift, commit, sync, log, remove), `wt` (add, remove, list), `scan` (add, remove, list), `nuke` (wt, stack, branches). Top-level: `cd`, `status`, `install`, `uninstall`, `update`, `version`, `ui`. Includes web UI: `Server/` (HTTP API), `Json/` (DTOs), `frontend/` (Svelte SPA, built to `wwwroot/` and embedded as assembly resources). Published with `PublishAot=true`.
- `src/Graft.VSCodeExtension/` — VS Code extension (planned).
- `src/Graft.VS2026Extension/` — Visual Studio 2026 extension (planned).
- `tests/Graft.Core.Tests/` — Unit tests for core library (xUnit).
- `tests/Graft.Cli.Tests/` — Integration/CLI tests (xUnit).
- `docs/` — Documentation, FAQ, spec (published via GitHub Pages).

## Releases

Releases are published to this repo's [GitHub Releases](https://github.com/radaiko/Graft/releases). The CI workflow in `.github/workflows/release-cli.yml` builds and uploads assets, triggered by `cli/v*` tags. The CLI ships as tar.gz/zip archives with the web UI assets (`wwwroot/`) bundled alongside the binary.

## Key Design Decisions

- **Git-native**: All operations map to git commands.
- **Offline-first**: Core works without network. Hosting integration is optional.
- **Git CLI via Process**: Shell out to `git` CLI (LibGit2Sharp is not AOT-compatible). See `GitRunner` class.
- **Native AOT**: Single native binary, no runtime dependency. All code must be AOT-safe.
- **Binary names**: Ships as `graft`. `graft install` creates a `gt` symlink next to it and writes a `git gt` alias to `~/.gitconfig`.
- **Storage**: Repo-local metadata in `.git/graft/`, global config in `~/.config/graft/`.
- **Active stack**: Stored as plain text in `.git/graft/active-stack`. Auto-set on `stack init`. Auto-migrated if exactly one stack exists. All operations (push, pop, drop, shift, commit, sync) resolve from active stack.
- **Worktree paths**: Fixed convention `../<repoName>.wt.<safeBranch>/` (slashes replaced with hyphens).
- **No cascade merge on commit**: `commit` only commits to the target branch; branches above become stale. Use `sync` to merge.
- **Sync merges + pushes**: `sync` merges each branch's parent into it (bottom-to-top), then pushes each updated branch (regular push, no force).
- **Repo scanner**: Background thread scans registered directories for git repos on every invocation (non-blocking). Results cached in `~/.config/graft/repo-cache.toml`. Worktrees auto-added/removed from cache. Stale entries pruned automatically.
- **Unified navigation**: `graft cd` is the single navigation command — matches repo names first, then branch names for worktrees. Replaces `graft wt goto`.
- **Auto-fetch** (planned): Opt-in `git fetch --all` for tracked repos, rate-limited, runs in background thread.

## Tech Stack

| What | Package |
|------|---------|
| Runtime | .NET 10, Native AOT |
| Git | `git` CLI via `System.Diagnostics.Process` |
| CLI | `System.CommandLine` 2.0.2 |
| Config (TOML) | `Tomlyn` 0.20.0 |
| Frontend | Svelte 5, Vite 6 (bundled as `wwwroot/` alongside binary) |
| Testing | `xunit` |

### System.CommandLine 2.0 API Notes

The stable 2.0 API differs from betas:
- Add subcommands: `command.Add(subcommand)` (not `AddCommand`)
- Set handlers: `command.SetAction(parseResult => { ... })`
- Invoke: `root.Parse(args).Invoke()` or `.InvokeAsync()`
- Get values: `parseResult.GetValue(option)`
- Option constructor takes single string name; use `.Aliases.Add("-x")` for short aliases

## AOT Constraints

All code in Graft.Core and Graft.Cli must be AOT-safe:
- No `System.Reflection.Emit`
- No unbounded `Type.GetType()` or dynamic assembly loading
- Use source generators for JSON serialization (`System.Text.Json` with `[JsonSerializable]`)
- Tomlyn supports AOT via `[TomlModel]` attributes
- Avoid libraries that aren't trimmer/AOT-friendly

## Conventions

- Binary name: `graft` (system alias: `gt`, git alias: `git gt`)
- Project names: `Graft.Core`, `Graft.Cli`
- Namespaces: `Graft.Core.*`, `Graft.Cli.*`
- Internal storage dir: `.git/graft/`
- Global config dir: `~/.config/graft/`
- Error messages: Include what went wrong, why, and how to fix it (see [Error Handling](docs/spec/error-handling.md))
- Changelogs: Per-product in `docs/changelog/` (cli.md, vscode.md, vs.md, jetbrains.md). Always use [Keep a Changelog](https://keepachangelog.com/) style (Added, Changed, Removed, Fixed sections)
- Git: Never commit directly to main/master. Always create a branch and open a PR.
- Git: Always squash-merge PRs. Never rebase shared branches. Use `git pull` (merge) to sync with remote.

### CLI Naming Convention

Commands use **full names** as primary, with **short aliases** registered as hidden alternatives:

| Action | Full (primary) | Short (alias) |
|--------|---------------|---------------|
| Remove | `remove` | `rm` |
| List | `list` | `ls` |
| Switch | `switch` | `sw` |
| Commit | `commit` | `ci` |
| Status | `status` | `st` |

Options always have a long form primary with a short alias: `--force`/`-f`, `--create`/`-c`, `--message`/`-m`, `--base`/`-b`, `--branch`/`-b`. All destructive commands support `--force`/`-f` to skip confirmation.

## CLI Command Structure

Short aliases shown in parentheses.

```
graft stack init <name> [-b/--base <branch>]          # Create stack (sets active)
graft stack list (ls)                                  # List all stacks
graft stack switch (sw) <name>                         # Switch active stack
graft stack push <branch> [-c/--create]                # Push branch to active stack
graft stack pop                                        # Remove top branch from stack
graft stack drop <branch>                              # Remove named branch from stack
graft stack shift <branch>                             # Insert branch at bottom of stack
graft stack commit (ci) -m/--message '<msg>' [-b <branch>]  # Commit to stack branch
graft stack sync [<branch>]                            # Merge parent + push stack branches
graft stack log                                        # Show stack branch graph
graft stack remove (rm) <name> [-f/--force]            # Delete a stack

graft wt <branch>                                      # Create worktree for existing branch
graft wt <branch> -c/--create                          # Create new branch + worktree
graft wt remove (rm) <branch> [-f/--force]             # Delete worktree
graft wt list (ls)                                     # List worktrees

graft scan add <directory>                             # Register directory for repo scanning
graft scan remove (rm) <directory>                     # Unregister directory
graft scan list (ls)                                   # List registered scan paths

graft cd <name>                                        # Navigate to repo or worktree
graft status (st)                                      # Cross-repo status overview
graft status (st) <reponame>                           # Detailed status for one repo

graft nuke [-f/--force]                                # Remove all graft resources
graft nuke wt [-f/--force]                             # Remove all worktrees
graft nuke stack [-f/--force]                          # Remove all stacks
graft nuke branches                                    # Remove branches whose upstream is gone

graft --continue                                       # Continue after conflict resolution
graft --abort                                          # Abort in-progress operation

graft install / uninstall / update / version           # Setup commands
graft ui                                               # Start web UI
```

## Module Layout (Graft.Core)

- `Git/` — Git CLI abstraction (`GitRunner`)
- `Stack/` — Stack management (`StackManager`, `ActiveStackManager`, definitions, sync)
- `Worktree/` — Worktree management (fixed path convention)
- `Commit/` — Commit routing (cross-branch commits, no cascade merge)
- `Nuke/` — Nuke operations (remove worktrees, stacks, gone branches)
- `AutoUpdate/` — Update checking and binary replacement
- `Install/` — Alias installer (`gt` symlink, `git gt` alias)
- `Config/` — Configuration types, loading, and active stack persistence
- `Scan/` — Repo scanner (background directory scanning, repo cache, scan path management)
- `Status/` — Cross-repo status aggregation (branch, ahead/behind, changed files, stacks, worktrees)

## Module Layout (Graft.Cli)

- `Commands/` — CLI command definitions: `StackCommand`, `WorktreeCommand`, `ScanCommand`, `NukeCommand`, `CdCommand`, `StatusCommand`, setup commands
- `Server/` — HTTP API server (`ApiServer`, handlers: `StackHandler`, `WorktreeHandler`, `NukeHandler`, `ConfigHandler`, `GitHandler`)
- `Json/` — Request/response DTOs and AOT-safe JSON serialization context
- `frontend/` — Svelte SPA source (built by MSBuild targets into `wwwroot/`)

## Data Model Details

### Stack definition (`stacks/<name>.toml`)

```toml
name = "auth-refactor"
created_at = "2025-02-01T10:00:00Z"
trunk = "main"

[[branches]]
name = "auth/base-types"
pr_number = 123
pr_url = "https://..."

[[branches]]
name = "auth/session-manager"
pr_number = 124
```

Branches ordered bottom-to-top (index 0 is closest to trunk).

### Active stack (`active-stack`)

Plain text file containing the name of the active stack. Located at `.git/graft/active-stack`.

### Worktree paths

Fixed convention: `../<repoName>.wt.<safeBranch>/` where slashes in branch names are replaced with hyphens.
Example: branch `feature/api` in repo `Graft` → `../Graft.wt.feature-api/`

### Scan paths (in `~/.config/graft/config.toml`)

```toml
[[scan_paths]]
path = "/Users/dev/projects"

[[scan_paths]]
path = "/Users/dev/work"
```

### Repo cache (`~/.config/graft/repo-cache.toml`)

```toml
[[repos]]
name = "Graft"
path = "/Users/dev/projects/Graft"
auto_fetch = false

[[repos]]
name = "Graft.wt.feature-api"
path = "/Users/dev/projects/Graft.wt.feature-api"
branch = "feature/api"
auto_fetch = false
```

Repos store name → path mappings. Worktree entries include a `branch` field for branch-name lookup via `graft cd`. The `auto_fetch` flag controls background `git fetch --all` (#19).

### Update state (`update-state.toml`)

```toml
last_checked = "2026-02-05T14:30:00Z"
current_version = "0.3.1"

[pending_update]
version = "0.3.2"
binary_path = "/Users/dev/.config/graft/staging/graft-0.3.2"
checksum = "sha256:abc123..."
downloaded_at = "2026-02-05T14:30:05Z"
```

## Auto-Update Implementation

1. On every invocation, a background thread checks for updates (rate-limited to once per hour via `update-state.toml`).
2. If a newer version exists: download to `~/.config/graft/staging/`, verify checksum, write `pending_update` to state file.
3. On next invocation, before running the command: replace binary with staged version (atomic rename on Unix, MoveFileEx on Windows), clear `pending_update`, re-exec with original args.

## Alias Implementation

`graft install`:
1. Creates `gt` symlink next to `graft` binary (hardlink/copy on Windows).
2. Writes `gt = !graft` to `~/.gitconfig` `[alias]` section.

The binary detects whether invoked as `graft` or `gt` and behaves identically.

## Testing

- Unit tests per module in `tests/Graft.Core.Tests/`
- CLI integration tests in `tests/Graft.Cli.Tests/`
- Run all tests: `dotnet test src/Graft.sln`

## Common Commands

```bash
dotnet build src/Graft.sln                  # Build all projects
dotnet run --project src/Graft.Cli          # Run the CLI
dotnet run --project src/Graft.Cli -- ui    # Start web UI (browser-based)
dotnet test src/Graft.sln                   # Run all tests
dotnet publish src/Graft.Cli -c Release     # AOT publish
```
