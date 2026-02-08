# Data Storage

Graft stores metadata alongside git. No external database or service is required.

---

## Per-Repo Storage (`.git/graft/`)

All repo-specific data lives under the `.git/graft/` directory (resolved via `git rev-parse --git-common-dir` for worktrees).

```
.git/graft/
├── config.toml           # Repo-level settings
├── stacks/
│   └── <name>.toml       # One file per stack
├── worktrees.toml        # Worktree layout and templates
└── operation.toml        # Transient: tracks in-progress operations
```

### `config.toml` — Repo Configuration

```toml
[defaults]
trunk = "main"                # Default trunk branch for new stacks
stack_pr_strategy = "chain"   # PR creation strategy: "chain" or "independent"
```

All fields are optional. Defaults are applied when a field is missing.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `defaults.trunk` | string | `"main"` | Default trunk branch for `graft stack init` |
| `defaults.stack_pr_strategy` | string | `"chain"` | How PRs are created for stacked branches |

### `stacks/<name>.toml` — Stack Definition

One file per stack. The filename is the stack name (no forward slashes allowed in stack names).

```toml
name = "auth-refactor"
trunk = "main"
created_at = "2025-02-01T10:00:00Z"
updated_at = "2025-02-01T12:30:00Z"

[[branches]]
name = "auth/base-types"
pr_number = 123
pr_url = "https://github.com/user/repo/pull/123"
pr_state = "open"

[[branches]]
name = "auth/session-manager"
pr_number = 124
pr_url = "https://github.com/user/repo/pull/124"
pr_state = "open"
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | yes | Stack name (alphanumeric, hyphens, underscores, dots) |
| `trunk` | string | yes | Base branch the stack is built on |
| `created_at` | ISO 8601 datetime | no | When the stack was created |
| `updated_at` | ISO 8601 datetime | no | Last modification time |
| `branches` | array of tables | no | Ordered list of branches (index 0 is closest to trunk) |

**Branch fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | yes | Git branch name |
| `pr_number` | integer | no | Pull request number (requires `pr_url`) |
| `pr_url` | string | no | Pull request URL (requires `pr_number`) |
| `pr_state` | string | no | `"open"`, `"merged"`, or `"closed"` (default: `"open"`) |

**Branch ordering:** Branches are ordered bottom-to-top. Index 0 is merged from the trunk. Each subsequent branch is merged from the one before it.

### `worktrees.toml` — Worktree Configuration

```toml
[layout]
pattern = "../{name}"

[templates]
[[templates.files]]
src = ".env.template"
dst = ".env"
mode = "copy"

[[templates.files]]
src = ".vscode/settings.json"
dst = ".vscode/settings.json"
mode = "symlink"
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `layout.pattern` | string | `"../{name}"` | Path pattern for new worktrees. Must contain `{name}`. Resolved relative to the repo root. |
| `templates.files` | array of tables | `[]` | Files to copy or symlink into new worktrees |

**Template file fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `src` | string | yes | Source path relative to the repo root |
| `dst` | string | yes | Destination path relative to the worktree root |
| `mode` | string | no | `"copy"` (default) or `"symlink"` |

### `operation.toml` — In-Progress Operation State

This file exists only while a multi-step operation (sync or commit) is paused for conflict resolution. It is deleted when the operation completes or is aborted.

```toml
stack = "auth-refactor"
branch_index = 1
original_branch = "auth/oauth-provider"
operation = "sync"
```

| Field | Type | Description |
|-------|------|-------------|
| `stack` | string | Name of the stack being operated on |
| `branch_index` | integer | Index into the stack's branch list where the operation paused |
| `original_branch` | string | The branch HEAD was on before the operation started (restored on completion/abort) |
| `operation` | string | Operation type: `"sync"` |

---

## Global Storage (`~/.config/graft/`)

User-level configuration and update state, shared across all repos.

```
~/.config/graft/
├── config.toml           # Global user preferences (scan paths, settings)
├── repo-cache.toml       # Cached repo/worktree discovery results
├── update-state.toml     # Auto-update tracking
└── staging/              # Downloaded update binaries
    └── graft-<version>   # Staged binary awaiting application
```

### `config.toml` — Global Configuration

Includes scan paths for repo discovery (see [Scan Commands](../spec.md#scan-commands)).

```toml
[[scan_paths]]
path = "/Users/dev/projects"

[[scan_paths]]
path = "/Users/dev/work"
```

| Field | Type | Description |
|-------|------|-------------|
| `scan_paths` | array of tables | Directories to scan for git repositories |
| `scan_paths[].path` | string | Absolute path to a directory |

### `repo-cache.toml` — Discovered Repos Cache

Populated by background scanning. Updated automatically when worktrees are created/removed. Stale entries (paths that no longer exist) are pruned on each scan.

```toml
[[repos]]
name = "Graft"
path = "/Users/dev/projects/Graft"
auto_fetch = true
last_fetched = "2026-02-08T10:15:00Z"

[[repos]]
name = "Graft.wt.feature-api"
path = "/Users/dev/projects/Graft.wt.feature-api"
branch = "feature/api"
auto_fetch = false
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | yes | Repository directory name |
| `path` | string | yes | Absolute path to the repository |
| `branch` | string | no | Branch name (present only for worktree entries, used for `graft cd` branch-name lookup) |
| `auto_fetch` | boolean | no | Whether to run `git fetch --all` in the background (default: `false`) |
| `last_fetched` | ISO 8601 datetime | no | When the last successful background fetch occurred (managed automatically) |

### `update-state.toml` — Auto-Update State

```toml
last_checked = "2026-02-05T14:30:00Z"
current_version = "0.3.1"

[pending_update]
version = "0.3.2"
binary_path = "/Users/dev/.config/graft/staging/graft-0.3.2"
checksum = "sha256:abc123def456..."
downloaded_at = "2026-02-05T14:30:05Z"
```

| Field | Type | Description |
|-------|------|-------------|
| `last_checked` | ISO 8601 datetime | When the last update check occurred |
| `current_version` | string | Currently installed version |
| `pending_update` | table | Present only when an update is staged |

**Pending update fields:**

| Field | Type | Description |
|-------|------|-------------|
| `version` | string | Version of the staged binary |
| `binary_path` | string | Absolute path to the staged binary in `staging/` |
| `checksum` | string | SHA-256 checksum prefixed with `sha256:` |
| `downloaded_at` | ISO 8601 datetime | When the binary was downloaded |

---

## Write Safety

All TOML files are written atomically: content is written to a uniquely-named temporary file (`.tmp.<guid>`) in the same directory, then renamed to the target path. This prevents corruption from crashes or concurrent writes.

## Name Validation

Stack names and branch names are validated before use to prevent path traversal and git flag injection:

- **Stack names:** Alphanumeric, hyphens, underscores, dots. No forward slashes (used as filenames). Must start with alphanumeric. Must not contain `..`, null bytes, or backslashes.
- **Branch names:** Same as stack names, plus forward slashes for namespacing (e.g., `auth/base-types`). Must not start or end with `/`, contain `//`, end with `.lock`, or contain `@{`.
- **Version strings:** 1-64 characters, alphanumeric plus dots and hyphens, must start with alphanumeric.
