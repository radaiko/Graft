---
title: Web UI
---

# Web UI

A browser-based visual interface for stacks and worktrees.

---

## Starting the UI

```bash
graft ui
```

Starts a local HTTP server on `localhost` and opens your browser automatically. The UI is bundled inside the `graft` binary — no additional installation or dependencies needed.

The server only listens on `localhost` and is not accessible from other machines.

---

## Views

### Stacks (Default View)

The main view shows your active stack as a vertical graph:

```
main
 └── feature/auth        2 commits  ✓ up-to-date
      └── feature/api    1 commit   ⚠ needs sync
```

- **Branch nodes** show the branch name, commit count, and sync status
- **Action bar** at the top with Sync, Push, and Commit buttons
- **Conflict banner** appears when a sync hits a conflict, with Continue and Abort buttons
- **Stack selector** to switch between stacks

The Commit button opens a dialog where you can:
- Write a commit message
- Choose which branch to commit to
- See currently staged files

### Worktrees

A table showing all worktrees in the repository:

- Branch name and worktree path for each entry
- **Add** button to create a new worktree (with branch selector and create option)
- **Remove** button on each row to delete a worktree
- Status indicator for worktrees with uncommitted changes

### Settings

Configuration form for repo-level settings:

- View and edit the TOML config values
- Inline validation for invalid values
- Save button applies changes immediately

---

## How It Works

The web UI is a Svelte single-page application (SPA). During the build process, it's compiled to static HTML/CSS/JS and embedded as assembly resources inside the native `graft` binary. When you run `graft ui`, a built-in HTTP listener serves these files along with a REST API.

### Live Updates

- The UI polls the API every 2 seconds to detect external changes (e.g. `git commit` from the terminal, another developer pushing)
- Stack and worktree views update automatically
- Sync and commit operations show a loading spinner and block the UI until complete

### Conflict Resolution

When a sync operation hits a conflict:

1. A conflict banner appears at the top of the page
2. The banner shows the conflicting branch and files
3. You resolve conflicts in your editor/terminal as usual
4. Click **Continue** in the UI (equivalent to `graft --continue`) or **Abort** (`graft --abort`)

### Error Handling

Errors are shown as dismissable toast notifications at the bottom of the screen. Each toast includes the error message and disappears after a few seconds (or can be dismissed manually).

---

## API Endpoints

The UI communicates via a local REST API. These endpoints are available for scripting or building custom integrations.

### Stack

These endpoints correspond to the [Stack CLI commands](./cli/stack.md).

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/stacks` | List all stacks |
| POST | `/api/stacks` | Create a new stack |
| DELETE | `/api/stacks` | Delete a stack |
| GET | `/api/stacks/active` | Get the active stack |
| PUT | `/api/stacks/active` | Set the active stack |
| POST | `/api/stacks/push` | Push a branch to the stack |
| POST | `/api/stacks/pop` | Pop the top branch |
| POST | `/api/stacks/drop` | Drop a named branch |
| POST | `/api/stacks/shift` | Shift a branch to the bottom |
| POST | `/api/stacks/sync` | Sync the stack |
| POST | `/api/stacks/commit` | Commit to a branch |

### Conflict Resolution

These endpoints correspond to the [Conflict Resolution CLI commands](./cli/conflict.md).

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/sync/continue` | Continue after resolving a conflict |
| POST | `/api/sync/abort` | Abort the in-progress sync |

### Worktree

These endpoints correspond to the [Worktree CLI commands](./cli/worktree.md).

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/worktrees` | List all worktrees |
| POST | `/api/worktrees` | Create a worktree |
| DELETE | `/api/worktrees` | Delete a worktree |

### Nuke

These endpoints correspond to the [Nuke CLI commands](./cli/nuke.md).

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/nuke` | Remove all Graft resources |
| POST | `/api/nuke/worktrees` | Remove all worktrees |
| POST | `/api/nuke/stacks` | Remove all stacks |
| POST | `/api/nuke/branches` | Remove gone branches |

### Config & Git

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/config` | Get current configuration |
| PUT | `/api/config` | Update configuration |
| GET | `/api/git/status` | Get git status |
| GET | `/api/git/branches` | List all branches |
