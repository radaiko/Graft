---
layout: default
title: Workflow Guide
nav_order: 2
---

# Stacked Branches Workflow

How Graft solves the dependent-branches problem — using merge commits, never rebase.

---

## Designed for Squash-Merge PRs

Graft is built around the **squash-merge pull request workflow** that most teams use on GitHub, GitLab, and Azure DevOps:

- Each branch in a stack becomes its own PR
- PRs are **squash-merged** into their target branch
- Graft uses **merge commits** to keep branches in sync — it never rebases or rewrites history
- No `--force-push` is ever needed
- When a PR is squash-merged, you `drop` that branch and `sync` — the squashed changes flow upward

---

## The Problem: Dependent Branches

When feature B depends on feature A (which is still in review), your options are all bad:

1. **Wait** for feature A to merge — wastes time
2. **Branch from feature/a** — messy diffs in the PR, reviewers see both features' changes
3. **Merge main into feature/a, then branch** — painful after squash merge, conflicts everywhere

## The Graft Workflow

### 1. Create a Stack

```bash
graft stack init my-feature
```

Creates a stack using the current branch as trunk. This becomes your **active stack**.

```
Stack: my-feature
  main  (trunk)
```

### 2. Add Your First Branch

```bash
graft stack push -c feature/a
```

Creates `feature/a` from `main` and adds it to the stack. You're now on `feature/a`.

```
Stack: my-feature
  main  (trunk)
   └── feature/a  ← you are here
```

Write your code, stage changes with `git add`, and commit:

```bash
graft stack commit -m "implement feature A"
```

### 3. Sync with Upstream

```bash
graft stack sync
```

What happens:

```
1. git fetch
2. Merge main → feature/a     (skip if already up-to-date)
3. git push origin feature/a
```

Create a PR for `feature/a` targeting `main`.

### 4. Stack Another Branch

```bash
graft stack push -c feature/b
```

```
Stack: my-feature
  main  (trunk)
   └── feature/a
        └── feature/b  ← you are here
```

Create a PR for `feature/b` targeting `feature/a`. Reviewers see **only B's changes**.

### 5. Keep Everything in Sync

When `main` moves forward (other PRs merge), run:

```bash
graft stack sync
```

What happens:

```
1. git fetch
2. Merge main → feature/a       (picks up new changes from main)
3. Merge feature/a → feature/b  (cascades those changes upward)
4. git push origin feature/a
5. git push origin feature/b
```

Both PRs are now up to date. One command.

### 6. When feature/a Gets Squash-Merged

After `feature/a` is squash-merged into `main` on GitHub:

```bash
# Remove feature/a from the stack
graft stack drop feature/a

# Sync — main now contains feature/a's code
graft stack sync
```

What happens:

```
Before:                          After drop + sync:
  main                             main (contains feature/a)
   └── feature/a  (merged)          └── feature/b ← updated
        └── feature/b
```

Git recognizes the squashed changes are already present in `main`, so the merge into `feature/b` is clean. Update `feature/b`'s PR to target `main`. Done.

---

## Working with Multiple Branches

Stacks can have many branches. Here's a larger example:

```bash
graft stack init auth-refactor
graft stack push -c auth/types
graft stack push -c auth/session
graft stack push -c auth/api
graft stack push -c auth/tests
```

```
Stack: auth-refactor
  main  (trunk)
   └── auth/types
        └── auth/session
             └── auth/api
                  └── auth/tests
```

`graft stack sync` merges the entire chain in order:
`main → types → session → api → tests`, then pushes each updated branch.

When PRs are merged from the bottom up, `drop` each one and `sync`:

```bash
# auth/types got squash-merged
graft stack drop auth/types
graft stack sync
# Now: main → session → api → tests
```

---

## Conflict Handling

{: .note }
> Only `sync` can produce conflicts. All other commands modify metadata or commit changes — they never merge.

When `sync` hits a conflict:

1. The merge pauses on the conflicting branch
2. Operation state is saved to `.git/graft/operation.toml`
3. You see exactly what to do:

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

After fixing the conflicts:

```bash
# Resolve conflicts in your editor, then:
git add src/Auth/SessionManager.cs src/Auth/TokenValidator.cs
graft --continue    # Finishes this merge, continues to remaining branches
```

Or abort the entire operation:

```bash
graft --abort       # Aborts the merge, restores your original branch
```

If there are more branches after the conflicting one, `--continue` proceeds to merge those too. If another conflict occurs, the process repeats.

---

## Committing to Lower Branches

Need to fix something in `auth/types` while working on `auth/api`?

```bash
# Stage your changes, then commit to a specific branch
git add src/Auth/BaseTypes.cs
graft stack commit -m "fix type definition" -b auth/types
```

What Graft does behind the scenes:

```
1. Stash your current work
2. Check out auth/types
3. Apply staged changes and commit
4. Return to auth/api
5. Restore your stash
```

{: .note }
> Branches above the target become stale after this. Run `graft stack sync` to propagate the changes upward through the stack.

---

## Inserting Branches

Need to add a branch at the bottom of the stack (right above trunk)?

```bash
graft stack shift auth/config
```

```
Before:                          After:
  main                             main
   └── auth/types                   └── auth/config  (new)
        └── auth/session                 └── auth/types
                                              └── auth/session
```

Run `graft stack sync` to merge `main → config → types → session`.

---

## Why Merge, Not Rebase?

Many stacked-branch tools use `git rebase` to keep branches in sync. This rewrites commit history:

| | Merge (Graft) | Rebase (other tools) |
|---|---|---|
| History rewritten? | No | Yes |
| Force push needed? | No | Yes, after every sync |
| Safe for shared branches? | Yes | Risky |
| Commit SHAs change? | No | Yes, breaks PR links |
| Recovery from errors? | Easy | Can be painful |

Graft uses merge commits only. Your commits stay exactly as you wrote them. Pushes are regular `git push`.

The tradeoff is merge commits in your branch history. But since most teams **squash-merge PRs anyway**, those merge commits disappear when the PR lands. The final history on `main` is clean regardless.

## Key Benefits

- **One command sync** — `graft stack sync` replaces manual `git merge` across all branches
- **Clean PRs** — each PR shows only its own changes
- **No force push** — merge-based, history is never rewritten
- **Safe for teams** — shared branches are never force-pushed
- **Built for squash-merge** — merge commits in branches don't matter
- **Conflict handling** — clear error messages, `--continue` / `--abort` workflow
