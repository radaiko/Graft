---
title: Navigation
---

# Navigation

Jump to any discovered repo or worktree with a single command.

---

## `graft cd <name>`

Navigate to a discovered repo or worktree. Matches repo names first, then branch names for worktrees.

```bash
# Jump to a repo by name
$ graft cd my-app

# Jump to a worktree by branch name
$ graft cd feature/auth
# → ../Graft.wt.feature-auth/
```

### Multiple Matches

If multiple repos or worktrees match the name, all matches are shown and you pick one interactively.

### How It Works

A child process can't change the parent shell's working directory. Graft handles this with a shell function that wraps the binary:

1. `graft cd <name>` prints the target directory path to stdout
2. The shell function captures this and runs `cd` in the parent shell

The shell function is set up automatically by `graft install`. If you're using `graft` directly (without the shell function), the path is printed but the directory change won't happen — use `cd $(graft cd <name>)` as a workaround.

---

## Tips

- **Register scan paths first**: `graft cd` only finds repos that are in the [scan cache](./scan.md). Run `graft scan add <directory>` to register your project directories.
- **Worktrees included automatically**: Worktrees created with `graft wt` are added to the cache, so `graft cd <branch>` works immediately.
- **Case-insensitive matching**: Repo name matching is case-insensitive.

---

## See Also

- [Scan & Discovery](./scan.md) — Register directories to discover repos
- [Status](./status.md) — See all discovered repos at a glance
- [Worktree Commands](./worktree.md) — Create worktrees to navigate to
