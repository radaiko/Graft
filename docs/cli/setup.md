---
title: Setup Commands
---

# Setup Commands

Installation, aliases, updates, and the web UI.

---

## Quick Reference

| Command | Description |
|---------|-------------|
| `graft install` | Create `gt` shortcut and `git gt` alias |
| `graft uninstall` | Remove shortcuts and aliases |
| `graft update` | Check for and apply updates |
| `graft version` | Print current version |
| `graft ui` | Start the web UI |

---

## `graft install`

Create the `gt` shortcut, add `git gt` alias, and set up shell integration for `graft cd`.

```bash
$ graft install
Installed aliases: gt, git gt
Added shell integration to /Users/you/.zshrc
Restart your shell or run: source /Users/you/.zshrc
```

After installation, you can use any of these interchangeably:

| Form | Example |
|------|---------|
| `graft` | `graft stack sync` |
| `gt` | `gt stack sync` |
| `git gt` | `git gt stack sync` |

On macOS/Linux, `gt` is a symlink. On Windows, it's a copy of the binary.

The `git gt` alias is written to `~/.gitconfig` as `gt = !graft`.

### Shell Integration

`graft install` detects your current shell and writes wrapper functions into your profile:

| Shell | Profile |
|-------|---------|
| Zsh | `~/.zshrc` |
| Bash | `~/.bashrc` (or `~/.bash_profile`) |
| Fish | `~/.config/fish/config.fish` |
| PowerShell | `$PROFILE` |

The wrapper functions intercept `graft cd` and `gt cd`, capture the target path from stdout, and run `cd` in the parent shell. All other subcommands pass through to the binary unchanged. This is the same pattern used by zoxide, direnv, and similar tools.

If your shell can't be detected, `graft install` will print a notice. Supported shells: bash, zsh, fish, PowerShell.

---

## `graft uninstall`

Remove the `gt` shortcut, `git gt` alias, and shell integration from your profile.

---

## `graft update`

Check for updates and apply if available.

```bash
$ graft update
Checking for updates...
New version available: v0.3.0 (current: v0.2.0)
Downloading... done.
Update will be applied on next run.
```

### Auto-Update

Graft also checks for updates automatically in the background (at most once per hour). When a new version is found:

1. The binary is downloaded to `~/.config/graft/staging/`
2. A SHA-256 checksum is verified
3. On your next invocation, the binary is replaced atomically and re-executed
4. If the update fails, the previous binary is restored automatically

No telemetry or usage data is collected. The only network request is a GitHub API call to check for new releases.

---

## `graft version`

Print the current version.

```bash
$ graft version
graft 0.2.0
```

---

## `graft ui`

Start the web-based UI. Opens your default browser automatically.

```bash
$ graft ui
Server started at http://localhost:5123
```

See [Web UI](../web-ui.md) for details on the browser interface.

---

## See Also

- [Web UI](../web-ui.md) — Browser-based interface for stacks and worktrees
- [FAQ](../faq.md) — Auto-update details
