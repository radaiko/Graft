# Web UI

A browser-based web UI for Graft, providing a visual interface for stacked branches, worktrees, and settings.

---

## Architecture

The UI ships with the `graft` CLI. Running `graft ui` starts an HTTP API server on localhost and the user opens the URL in their browser. The Svelte frontend is compiled to static JS/CSS at build time and embedded into the CLI binary as assembly resources.

```
+----------------------------------+
|         Browser Tab              |
|  +----------------------------+  |
|  |   Svelte Frontend (JS)    |  |
|  |   fetch() <-> localhost    |  |
|  +----------------------------+  |
|              | HTTP              |
|  +----------------------------+  |
|  |   .NET HTTP API Server    |  |
|  |   (HttpListener)          |  |
|  +----------------------------+  |
|              |                   |
|  +----------------------------+  |
|  |      Graft.Core           |  |
|  |  (Stack, Worktree, Git)   |  |
|  +----------------------------+  |
+----------------------------------+
```

### Usage

```bash
graft ui              # Start on random free port
graft ui --port 5199  # Start on specific port
```

The server prints its URL and blocks until Ctrl+C. The same API can be consumed by VS Code extensions, Visual Studio extensions, or any HTTP client.

### Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Svelte 5 + Vite 6 |
| API | System.Net.HttpListener (JSON) |
| Backend | Graft.Core (existing library) |

The HTTP server uses `System.Net.HttpListener` to stay small and AOT-friendly — no ASP.NET dependency.

---

## API

All endpoints use JSON. The repo path is detected from the working directory where `graft ui` was launched.

All mutation endpoints return the updated state so the frontend can refresh without a second call.

### Stack Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/stacks` | List all stacks (names) |
| GET | `/api/stacks/{name}` | Stack detail: trunk, branches, commit counts, rebase status, HEAD |
| POST | `/api/stacks` | Init a new stack `{ "name": "...", "baseBranch": "..." }` |
| DELETE | `/api/stacks/{name}` | Delete a stack |
| GET | `/api/stacks/active` | Get the active stack `{ "name": "..." }` |
| PUT | `/api/stacks/active` | Set the active stack `{ "name": "..." }` |
| POST | `/api/stacks/push` | Push branch to active stack `{ "branchName": "...", "createBranch": false }` |
| POST | `/api/stacks/pop` | Pop top branch from active stack |
| POST | `/api/stacks/drop` | Drop a named branch `{ "branchName": "..." }` |
| POST | `/api/stacks/shift` | Shift branch to bottom `{ "branchName": "..." }` |
| POST | `/api/stacks/sync` | Sync/rebase the active stack |
| POST | `/api/stacks/commit` | Commit staged changes `{ "message": "...", "branch": "...", "amend": false }` |

All mutation endpoints (push, pop, drop, shift, sync, commit) operate on the active stack. The frontend ensures the correct stack is active before calling these.

These endpoints wrap the stacking commands described in [spec.md](../spec.md).

### Sync Continue/Abort Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/sync/continue` | Continue after conflict resolution |
| POST | `/api/sync/abort` | Abort in-progress sync operation |

### Worktree Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/worktrees` | List all worktrees with branch and path |
| POST | `/api/worktrees` | Add a worktree `{ "branch": "..." }` |
| DELETE | `/api/worktrees/{branch}` | Remove a worktree |

These endpoints wrap the worktree commands described in [spec.md](../spec.md).

### Nuke Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/nuke` | Nuke all (worktrees, stacks, branches) `{ "force": false }` |
| POST | `/api/nuke/worktrees` | Remove all worktrees `{ "force": false }` |
| POST | `/api/nuke/stacks` | Remove all stacks `{ "force": false }` |
| POST | `/api/nuke/branches` | Remove branches whose upstream is gone |

### Config Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/config` | Read repo config (`.git/graft/config.toml`) |
| PUT | `/api/config` | Write updated config |
| GET | `/api/config/worktree` | Read worktree config (`worktrees.toml`) |
| PUT | `/api/config/worktree` | Write updated worktree config |

### Git Context Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/git/status` | Current branch, dirty state, staged files |
| GET | `/api/git/branches` | List local branches |

---

## Views

The app has three views, navigated by a sidebar or tab bar.

### 1. Stacks View (default)

The left panel shows a list of stacks. The main area shows a vertical graph of the active stack: trunk at top, branches flowing down, each displaying branch name, commit count, and rebase status. The current HEAD branch is highlighted.

**Action bar:**
- **Sync** — rebase the stack onto trunk
- **Push** — input for branch name, creates and pushes to top
- **Commit** — shows staged files, message input, optional target branch selector

**On conflict:** A banner shows conflicting files with Continue/Abort buttons.

### 2. Worktrees View

A table of active worktrees showing: path, branch, HEAD SHA. Each row has a Remove button. "Add Worktree" button opens a branch selector dropdown (from `GET /api/git/branches`).

### 3. Settings View

A form that reads from and writes to the TOML config files:

- **Defaults** — trunk branch name, PR strategy
- **Worktree Layout** — pattern string (must contain `{name}`)
- **Templates** — list of template file mappings (src, dst, mode). Add/remove rows.

Each section has a Save button. Validation is inline.

---

## State Management

### Polling

The frontend polls `GET /api/stacks/{name}`, `GET /api/git/status`, and `GET /api/stacks/active` every 2 seconds to catch external changes (e.g., user running git commands in a terminal). The poll captures the selected stack name before awaiting to avoid race conditions with user navigation.

### Long-Running Operations

Sync and commit are handled synchronously. The frontend shows a spinner and disables other mutation buttons until the response returns.

### Conflict State

Sync responses with `hasConflict: true` trigger a conflict banner. Polling detects when conflicts are resolved externally.

### Error Responses

API errors return `{ "error": "message" }` with appropriate HTTP status codes. The frontend shows a dismissable toast notification. Error messages come from Graft.Core and follow the what/why/how-to-fix format (see [Error Handling](error-handling.md)).

### Repo Detection

On startup, the UI uses the current working directory. If not a git repo, the API returns appropriate errors.

---

## Theme

Dev-tool-inspired dark theme. Monospace fonts for branch names and SHAs. Muted colors. Minimal chrome.
