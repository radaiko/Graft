---
layout: default
title: Stack Commands
parent: CLI Reference
nav_order: 1
---

# Stack Commands

Create, manage, and sync stacked branches. All stack commands operate on the **active stack** — use `graft stack switch` to change which stack is active.

---

## Quick Reference

| Command | Description |
|---------|-------------|
| `graft stack init <name> [-b <branch>]` | Create a new stack |
| `graft stack list` | List all stacks |
| `graft stack switch <name>` | Switch active stack |
| `graft stack push <branch> [-c]` | Add branch to top of stack |
| `graft stack pop` | Remove topmost branch |
| `graft stack drop <branch>` | Remove named branch |
| `graft stack shift <branch>` | Insert branch at bottom |
| `graft stack commit -m <msg> [-b <branch>]` | Commit to a stack branch |
| `graft stack sync [<branch>]` | Merge + push stack branches |
| `graft stack log` | Show stack graph |
| `graft stack remove <name> [-f]` | Delete a stack |

---

## `graft stack init <name> [-b/--base <branch>]`

Create a new stack. Uses the current branch as trunk, or the branch specified with `-b`. The new stack becomes the active stack.

```bash
$ graft stack init my-feature
Created stack 'my-feature' with trunk 'main'.
Active stack: my-feature

$ graft stack init hotfix -b release/2.0
Created stack 'hotfix' with trunk 'release/2.0'.
Active stack: hotfix
```

---

## `graft stack list` (alias: `ls`)

List all stacks. The active stack is marked with `*`.

```bash
$ graft stack list
* my-feature  (trunk: main, 3 branches)
  hotfix      (trunk: release/2.0, 1 branch)
```

---

## `graft stack switch <name>` (alias: `sw`)

Switch the active stack. Fails if the named stack does not exist.

```bash
$ graft stack switch hotfix
Active stack: hotfix
```

---

## `graft stack push <branch> [-c/--create]`

Add a branch to the top of the active stack. With `-c`, creates the branch first. The branch is checked out.

```bash
# Add an existing branch
$ graft stack push feature/auth
Pushed 'feature/auth' to stack 'my-feature'.

# Create a new branch and add it
$ graft stack push -c feature/api
Created branch 'feature/api'.
Pushed 'feature/api' to stack 'my-feature'.
```

---

## `graft stack pop`

Remove the topmost branch from the active stack. The git branch is kept.

```bash
$ graft stack pop
Popped 'feature/api' from stack 'my-feature'.
```

---

## `graft stack drop <branch>`

Remove the named branch from the stack regardless of position. The git branch is kept. Use this after a branch's PR is squash-merged.

```bash
$ graft stack drop feature/auth
Dropped 'feature/auth' from stack 'my-feature'.
```

---

## `graft stack shift <branch>`

Insert an existing branch at the bottom of the stack (directly above the trunk). Useful when you need to add a base branch that other branches depend on.

```bash
$ graft stack shift feature/config
Shifted 'feature/config' to bottom of stack 'my-feature'.
```

---

## `graft stack commit --message "<message>" [-b <branch>] [--amend]` (alias: `ci`)

Commit staged changes to the active stack. Defaults to the topmost branch. Use `-b` to target a specific branch. Use `--amend` to amend the latest commit. Short form: `-m`.

```bash
# Commit to the topmost branch
$ git add src/api.cs
$ graft stack commit -m "add API endpoint"

# Commit to a specific branch (stash/checkout/commit/return)
$ git add src/auth.cs
$ graft stack commit -m "fix auth bug" -b feature/auth

# Amend the latest commit on a branch
$ git add src/auth.cs
$ graft stack commit -m "fix auth bug" --amend -b feature/auth
```

When committing to a branch other than the current one, Graft:
1. Stashes your current work
2. Checks out the target branch
3. Applies staged changes and commits
4. Returns to your original branch
5. Restores your stash

{: .note }
> Branches above the target become stale after a cross-branch commit. Run `graft stack sync` to propagate changes upward.

---

## `graft stack sync [<branch>]`

Sync branches: fetch, merge each branch's parent into it (bottom-to-top), then push all updated branches. Optionally sync only the named branch.

```bash
# Sync entire stack
$ graft stack sync
Fetching...
Merging main → feature/auth... up-to-date.
Merging feature/auth → feature/api... done.
Pushing feature/api... done.

# Sync a single branch
$ graft stack sync feature/auth
```

If a merge conflict occurs, sync pauses. See [Conflict Resolution](conflict) for how to continue or abort.

---

## `graft stack log`

Show a visual graph of the active stack with branch relationships.

```bash
$ graft stack log
Stack: my-feature (trunk: main)
  main
   └── feature/auth    (2 commits)
        └── feature/api (1 commit)
```

---

## `graft stack remove <name> [-f/--force]` (alias: `rm`)

Remove a stack. Prompts for confirmation unless `--force` is used. Git branches are kept. If the removed stack was active, the active stack is cleared.

```bash
$ graft stack remove my-feature
Removed stack 'my-feature'.
```

---

## Tips

- **Drop after squash-merge**: When a PR is squash-merged, `drop` the branch and `sync` to propagate changes upward. See the [Workflow Guide](../workflow#6-when-featurea-gets-squash-merged) for a walkthrough.
- **Active stack auto-selection**: If exactly one stack exists, it's used automatically — no need to `switch`.
- **Multiple stacks**: You can have stacks on different trunk branches (e.g. one on `main`, another on `release/2.0`). Branches can only belong to one stack at a time.

---

## See Also

- [Workflow Guide](../workflow) — Full stacked branches walkthrough
- [Conflict Resolution](conflict) — Handle merge conflicts during sync
- [Nuke Commands](nuke) — Bulk remove all stacks
