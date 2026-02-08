---
title: Scan & Discovery
---

# Scan & Discovery

Manage directories that Graft scans for git repositories. Discovered repos power [`graft cd`](./navigation.md) and [`graft status`](./status.md).

---

## Quick Reference

| Command | Description |
|---------|-------------|
| `graft scan add <directory>` | Register a directory for scanning |
| `graft scan remove <directory>` | Unregister a directory |
| `graft scan list` | List registered scan paths |
| `graft scan auto-fetch enable [<name>]` | Enable background fetch for a repo |
| `graft scan auto-fetch disable [<name>]` | Disable background fetch for a repo |
| `graft scan auto-fetch list` | List repos with auto-fetch status |

---

## Scan Path Commands

### `graft scan add <directory>`

Register a directory to scan for git repositories.

```bash
$ graft scan add ~/dev/projects
Added scan path: /Users/dev/projects
```

On every `graft` invocation, a background thread scans all registered paths for git repositories. The scan is non-blocking — the main command runs immediately. Results are cached in `~/.config/graft/repo-cache.toml`.

### `graft scan remove <directory>` (alias: `rm`)

Remove a directory from the scan list.

```bash
$ graft scan remove ~/dev/projects
Removed scan path: /Users/dev/projects
```

### `graft scan list` (alias: `ls`)

List all registered scan paths.

```bash
$ graft scan list
/Users/dev/projects
/Users/dev/work
```

---

## Auto-Fetch Commands

Repos can opt into automatic background fetching. When enabled, `git fetch --all --quiet` runs in a fire-and-forget background thread on every `graft` invocation, rate-limited to once every 15 minutes per repo.

### `graft scan auto-fetch enable [<name>]`

Enable auto-fetch for a repository. If `<name>` is provided, looks up the repo by name in the cache (case-insensitive). If omitted, uses the current working directory.

```bash
# Enable for the current repo
$ graft scan auto-fetch enable
Auto-fetch enabled for 'Graft'.

# Enable for a named repo
$ graft scan auto-fetch enable my-app
Auto-fetch enabled for 'my-app'.
```

### `graft scan auto-fetch disable [<name>]`

Disable auto-fetch for a repository. Same name/path resolution as `enable`. Also clears the `last_fetched` timestamp.

```bash
$ graft scan auto-fetch disable my-app
Auto-fetch disabled for 'my-app'.
```

### `graft scan auto-fetch list` (alias: `ls`)

List all cached repos with their auto-fetch status and last fetch time.

```bash
$ graft scan auto-fetch list
Graft        on   last fetched 2 min ago
my-app       off
other-repo   on   last fetched 14 min ago
```

---

## How Scanning Works

- **Background thread**: Scanning runs in a non-blocking background thread on every invocation. Your command executes immediately.
- **Stale removal**: If a cached repo no longer exists on disk, it's automatically pruned.
- **Worktree integration**: `graft wt` automatically adds new worktrees to the repo cache. `graft wt remove` automatically removes them.
- **Rate limiting (auto-fetch)**: Each repo tracks its own `last_fetched` UTC timestamp. A fetch is skipped if the repo was fetched within the last 15 minutes.
- **Error handling (auto-fetch)**: Fetch failures (network errors, unreachable remotes) are silently skipped per repo. One repo failing does not prevent others from being fetched.

---

## See Also

- [Navigation](./navigation.md) — Use discovered repos with `graft cd`
- [Status](./status.md) — Cross-repo overview of discovered repos
- [Worktree Commands](./worktree.md) — Worktrees are auto-registered in the cache
