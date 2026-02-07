# Installation and Updates

Graft ships as a single native binary. No runtime install required.

---

## Supported Platforms

| OS | Architectures |
|----|---------------|
| Windows | x64, arm64 |
| macOS | Intel (x64), Apple Silicon (arm64) |
| Linux | x64, arm64 |

---

## Alias Implementation

After running `graft install`, the user gets two shortcuts:

- **`gt`** — system alias (symlink next to the `graft` binary)
- **`git gt`** — git alias

All three forms are identical: `graft stack sync`, `gt stack sync`, `git gt stack sync`.

**`gt` symlink:**
- **Unix (macOS/Linux):** Symbolic link via `File.CreateSymbolicLink`.
- **Windows:** File copy (symlinks require elevated privileges or developer mode).
- If `gt` already exists and points to a different target, it's replaced.
- If `gt` is a directory, the operation fails with a clear error.

**`git gt` alias:** Writes `gt = !graft` to `~/.gitconfig` `[alias]` section using `git config --global alias.gt '!graft'`.

**Uninstall:**
1. Deletes the `gt` symlink/copy.
2. Removes the `alias.gt` entry from `~/.gitconfig` using `git config --global --unset alias.gt`. Exit code 5 (key not found) is ignored.

---

## Auto-Update

Every invocation of Graft checks for updates in the background. The process is designed to never slow down the user's command.

### Lifecycle

```
Invocation N:
  1. Check: Is there a pending update?
     → No: Start background check (rate-limited to 1/hour)
     → Yes: Apply it before running the command

Background check:
  2. Query update server for latest version
  3. If newer: download to ~/.config/graft/staging/graft-<version>
  4. Verify SHA-256 checksum
  5. Write pending_update to update-state.toml

Invocation N+1:
  6. Detect pending_update in update-state.toml
  7. Re-verify checksum of staged binary
  8. Validate staged path is within staging/ directory
  9. Replace binary: rename current → .bak, move staged → current
  10. Set executable permissions (Unix: 755)
  11. Update current_version, clear pending_update
  12. Delete .bak
  13. Re-exec with original arguments
```

### Rate Limiting

Update checks are rate-limited to once per hour. The `last_checked` timestamp in `update-state.toml` is compared against the current time.

### Checksum Verification

Checksums use SHA-256, stored as `sha256:<hex>`. The checksum is verified:
- After download (before writing `pending_update`).
- Before applying (in case the file was tampered with between invocations).

If the checksum doesn't match, the staged binary is deleted and the pending update is cleared.

### Binary Replacement

On Unix, the new binary gets permissions `755` (owner rwx, group rx, other rx).

If replacement fails mid-operation:
- The old binary was renamed to `<path>.bak` first.
- If the new binary can't be moved into place, the backup is restored.
- If both fail, the error tells the user where the backup is.

See [Error Handling — Update Rollback](error-handling.md#update-rollback) for recovery details.

### Path Validation

Before applying an update, the staged binary path is validated to be within the expected `~/.config/graft/staging/` directory. Paths that resolve outside this directory are rejected to prevent path traversal attacks from tampered state files.

### Disabling Auto-Update

```
graft config set update.enabled false
```

### State File

Update state is stored in `~/.config/graft/update-state.toml`. See [Data Storage — `update-state.toml`](data-storage.md) for the full schema.

### Version String Validation

Version strings in update state are validated: 1-64 characters, alphanumeric plus dots and hyphens, must start with alphanumeric.
