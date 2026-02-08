---
title: Conflict Resolution
---

# Conflict Resolution

Handle merge conflicts that occur during `graft stack sync`.

---

## Quick Reference

| Command | Description |
|---------|-------------|
| `graft --continue` | Continue after resolving conflicts |
| `graft --abort` | Abort the in-progress sync |

---

## When Conflicts Happen

Only `sync` can produce merge conflicts. All other Graft commands modify metadata or commit changes — they never merge.

When `sync` merges a parent branch into a child branch and the merge can't be completed automatically, Graft pauses and tells you exactly what to do:

```
Error: Merge conflict in 'auth/session'

Conflicting files:
  - src/Auth/SessionManager.cs
  - src/Auth/TokenValidator.cs

To resolve:
  1. Fix conflicts in the files above
  2. Stage resolved files: git add <file>
  3. Continue: graft --continue

To abort: graft --abort
```

Graft saves its state to `.git/graft/operation.toml` so it can resume where it left off.

---

## `graft --continue`

Continue after resolving conflicts during a sync operation. Finishes the current merge and proceeds to remaining branches in the stack.

```bash
# After resolving conflicts:
$ git add src/Auth/SessionManager.cs src/Auth/TokenValidator.cs
$ graft --continue
Merge complete.
Merging auth/session → auth/api... done.
Pushing auth/session... done.
Pushing auth/api... done.
```

If another conflict occurs further up the stack, the process repeats — resolve, stage, `--continue`.

---

## `graft --abort`

Abort an in-progress sync operation. Aborts the merge and restores your original branch.

```bash
$ graft --abort
Aborted. Restored to 'feature/api'.
```

---

## Conflict Resolution Workflow

Here's the full workflow when a sync hits a conflict:

1. **`graft stack sync`** — starts merging bottom-to-top
2. **Conflict detected** — sync pauses, shows conflicting files
3. **Fix conflicts** — open the files in your editor, resolve the merge markers
4. **Stage resolved files** — `git add <file>` for each resolved file
5. **Continue** — `graft --continue` finishes the merge and moves to the next branch
6. **Repeat** — if another conflict occurs, go back to step 3

At any point, `graft --abort` cancels the entire operation and restores your original state.

---

## Tips

- **Check operation state**: If you're unsure whether a sync is in progress, look for `.git/graft/operation.toml`. If it exists, there's an unfinished operation.
- **Web UI support**: The [Web UI](../web-ui.md) shows a conflict banner with Continue and Abort buttons when a sync is paused.
- **Standard git tools work**: During a conflict, you're in a normal git merge state. All your usual tools (VS Code merge editor, IntelliJ merge tool, etc.) work as expected.

---

## See Also

- [Stack Commands](./stack.md) — `graft stack sync` triggers the merge cascade
- [Workflow Guide](../workflow.md#conflict-handling) — Conflicts in the context of a full workflow
