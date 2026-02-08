---
layout: default
title: Status
parent: CLI Reference
nav_order: 5
---

# Status

Cross-repo overview of all discovered repositories.

---

## `graft status` (alias: `st`)

Show a compact overview of all discovered repos.

```bash
$ graft status
Graft  ~/dev/projects/Graft
  branch   main
  status   ✓ clean
  stack    auth-refactor (3 branches)
  worktrees  2 active

my-app  ~/dev/projects/my-app
  branch   feature/api
  status   ↑2 ↓1  3 changed
```

### Output Fields

| Field | Description |
|-------|-------------|
| **branch** | Currently checked-out branch |
| **status** | Clean/dirty indicator, ahead/behind remote counts, changed file count |
| **stack** | Active stack name and branch count (if any) |
| **worktrees** | Number of active worktrees (if any) |

---

## `graft status <reponame>` (alias: `st`)

Show detailed status for a specific repo, including the full stack graph and worktree list.

```bash
$ graft status Graft
Graft  ~/dev/projects/Graft
  branch   main
  status   ✓ clean

  Stack: auth-refactor (trunk: main)
    main
     └── auth/types       ✓ up-to-date
          └── auth/session ↑1
               └── auth/api ⚠ needs sync

  Worktrees:
    ../Graft.wt.auth-types     auth/types
    ../Graft.wt.auth-session   auth/session
```

---

## Tips

- **Requires scan paths**: Status only shows repos in the [scan cache](scan). Run `graft scan add <directory>` first.
- **Background scanning**: The repo list updates automatically in the background on every `graft` invocation.
- **Quick navigation**: Spot a repo that needs attention? Jump to it with `graft cd <name>`. See [Navigation](navigation).

---

## See Also

- [Scan & Discovery](scan) — Register directories to discover repos
- [Navigation](navigation) — Jump to repos from the status overview
- [Stack Commands](stack) — Manage stacks shown in status output
