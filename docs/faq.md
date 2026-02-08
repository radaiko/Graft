---
title: FAQ
---

# Frequently Asked Questions

---

<details>
<summary><strong>What does <code>graft stack sync</code> do?</strong></summary>

In a stacked branch setup, each branch builds on the one below it:

```
main
 └── auth/base-types        (merged from main)
      └── auth/session       (merged from auth/base-types)
           └── auth/api       (merged from auth/session)
```

When `main` moves forward, every branch above becomes stale. You'd normally need to manually merge each branch in order, then push each one.

`graft stack sync` automates this:

1. **Fetch** — `git fetch --quiet`
2. **Cascade merge (bottom-to-top)** — For each branch, merge the parent into it. Skip if already up-to-date.
3. **Push** — `git push origin <branch>` for each updated branch.
4. **Restore** — Return to whatever branch you were on.

```
$ graft stack sync
Fetching...
Merging main → auth/base-types... done.
Merging auth/base-types → auth/session... up-to-date.
Merging auth/session → auth/api... done.
Pushing auth/base-types... done.
Pushing auth/api... done.
```

You can also sync a single branch: `graft stack sync auth/session`.

See [Stack Commands](./cli/stack.md#graft-stack-sync-branch) for the full reference.

</details>

---

<details>
<summary><strong>Why does Graft use merge instead of rebase?</strong></summary>

Many stacked-branch tools use `git rebase`. Rebase rewrites history, which means:

- You need `--force-push` after every sync — risky on shared branches
- Commit SHAs change, breaking links in PR comments and reviews
- Recovery from a bad rebase can be painful

Graft uses **merge commits only**. `sync` runs `git merge <parent>` on each branch. Your commits stay exactly as you wrote them. Pushes are regular `git push`.

The tradeoff is merge commits in your branch history. But since Graft is designed for **squash-merge PR workflows**, those merge commits disappear when the PR lands on `main`.

| | Merge (Graft) | Rebase (other tools) |
|---|---|---|
| History rewritten? | No | Yes |
| Force push needed? | No | Yes |
| Safe for shared branches? | Yes | Risky |
| Commit SHAs stable? | Yes | No |

</details>

---

<details>
<summary><strong>Does Graft work with squash-merge PRs?</strong></summary>

Yes — Graft is specifically designed for squash-merge workflows:

1. Each branch in your stack becomes a separate PR
2. The bottom PR targets `main`, subsequent PRs target the branch below
3. When a PR is approved, **squash-merge** it on GitHub/GitLab
4. `graft stack drop <branch>` — remove the merged branch from the stack
5. `graft stack sync` — `main` now contains the squashed code, merges cleanly into branches above
6. Update the next PR to target `main`

```
Before squash-merge:              After drop + sync:
  main                              main (contains auth/types)
   └── auth/types  (squash-merged)   └── auth/session ← clean merge
        └── auth/session
```

Git recognizes the changes are already present, so the merge is conflict-free.

</details>

---

<details>
<summary><strong>What happens if sync hits a conflict?</strong></summary>

Sync pauses and tells you exactly what to do:

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

Graft saves its state to `.git/graft/operation.toml`. After resolving:

- **`graft --continue`** — finishes the merge, continues to remaining branches in the stack
- **`graft --abort`** — cancels everything, restores your original branch

If another conflict occurs further up the stack, the process repeats.

See [Conflict Resolution](./cli/conflict.md) for the full workflow guide.

</details>

---

<details>
<summary><strong>What is an "active stack"?</strong></summary>

Graft tracks which stack is "active." All stack commands (`push`, `pop`, `drop`, `shift`, `commit`, `sync`, `log`) operate on the active stack automatically.

- **Auto-set on init**: `graft stack init <name>` activates the new stack
- **Auto-select**: If exactly one stack exists, it's used automatically
- **Switch**: `graft stack switch <name>`
- **Cleared on remove**: Removing the active stack clears the setting

```bash
$ graft stack list
* my-feature  (trunk: main, 3 branches)    ← active
  hotfix      (trunk: release/2.0, 1 branch)

$ graft stack switch hotfix
Active stack: hotfix
```

Stored as plain text in `.git/graft/active-stack`.

</details>

---

<details>
<summary><strong>How do worktrees work in Graft?</strong></summary>

Git worktrees let you have multiple branches checked out in separate directories simultaneously. Graft simplifies this with a fixed naming convention.

```bash
# Create worktree for existing branch
gt wt feature/auth
# → ../Graft.wt.feature-auth/

# Create new branch + worktree
gt wt feature/new -c
# → ../Graft.wt.feature-new/
```

**Path convention:** `../<repoName>.wt.<safeBranch>/` where slashes become hyphens.

```bash
$ gt wt list
/Users/dev/Graft                      main
/Users/dev/Graft.wt.feature-auth      feature/auth
/Users/dev/Graft.wt.feature-api       feature/api

# Jump to a worktree
$ graft cd feature/auth
```

Remove worktrees:

```bash
gt wt remove feature/auth         # fails if uncommitted changes
gt wt remove feature/auth -f      # force-remove
```

Worktrees are useful when working on multiple stack branches at the same time without switching.

See [Worktree Commands](./cli/worktree.md) for the full reference.

</details>

---

<details>
<summary><strong>What happens when I commit to a lower branch?</strong></summary>

`graft stack commit -b <branch>` commits to any branch in the stack:

```bash
$ git add src/Auth/BaseTypes.cs
$ graft stack commit -m "fix type definition" -b auth/types
```

Behind the scenes:

1. Stash your current work
2. Check out `auth/types`
3. Apply staged changes and commit
4. Return to your original branch
5. Restore your stash

Branches above `auth/types` become stale. Run `graft stack sync` to propagate changes upward.

If something goes wrong (e.g. checkout fails), your changes are preserved in a git stash and the error message includes the stash reference for recovery.

</details>

---

<details>
<summary><strong>Can I use Graft with existing branches?</strong></summary>

Yes. You can add existing branches to a stack:

```bash
graft stack init my-stack
graft stack push feature/existing-branch
graft stack push feature/another-existing
```

The branches must already exist in git. Graft adds them to its metadata — it doesn't modify the branches themselves until you run `sync`.

</details>

---

<details>
<summary><strong>Can I have multiple stacks?</strong></summary>

Yes. Each stack has its own trunk branch and set of branches.

```bash
graft stack init feature-a          # stack on main
graft stack init hotfix -b release  # stack on release branch

graft stack list
* feature-a  (trunk: main, 2 branches)
  hotfix     (trunk: release, 1 branch)

graft stack switch hotfix           # switch active stack
```

Branches can only belong to one stack at a time.

</details>

---

<details>
<summary><strong>What if I delete a branch outside of Graft?</strong></summary>

If you delete a git branch that's part of a stack (e.g. with `git branch -D`), Graft won't automatically detect this. The stack metadata still references the branch.

To clean up, `drop` the branch from the stack:

```bash
graft stack drop deleted-branch
```

Or if you've deleted many branches, `graft nuke stack` removes all stack definitions and you can start fresh.

</details>

---

<details>
<summary><strong>Does Graft auto-update?</strong></summary>

Yes. Graft checks for updates in the background (at most once per hour). When a new version is found, it's downloaded to `~/.config/graft/staging/` and verified with a SHA-256 checksum.

On your next invocation, the binary is replaced atomically and re-executed. If the update fails, the previous binary is restored automatically.

Check manually: `graft update`.

No telemetry or usage data is collected. The only network request is a GitHub API call to check for new releases.

See [Setup Commands](./cli/setup.md#auto-update) for more details.

</details>

---

<details>
<summary><strong>Does Graft work with GitHub / GitLab / Azure DevOps?</strong></summary>

Graft works with any git hosting platform. It operates on local git branches and pushes to whatever remote you have configured.

The PR workflow is the same everywhere:
1. Bottom branch targets `main` (or your trunk)
2. Each subsequent branch targets the one below it
3. Squash-merge from the bottom up
4. `drop` + `sync` after each merge

You create and manage PRs through your hosting platform's UI or CLI — Graft handles the branch management side.

</details>

---

<details>
<summary><strong>How does repo scanning work?</strong></summary>

Graft can discover git repositories across your machine. Register directories with `graft scan add`:

```bash
graft scan add ~/dev/projects
graft scan add ~/dev/work
```

On every `graft` invocation, a background thread scans registered paths for git repos. The scan is non-blocking — your command runs immediately. Results are cached in `~/.config/graft/repo-cache.toml`.

Discovered repos power two features:
- **`graft cd <name>`** — jump to any repo or worktree by name. See [Navigation](./cli/navigation.md).
- **`graft status`** — cross-repo overview of all discovered repos. See [Status](./cli/status.md).

Worktrees created with `graft wt` are automatically added to the cache. Stale entries (deleted repos) are pruned automatically.

See [Scan & Discovery](./cli/scan.md) for the full command reference.

</details>

---

<details>
<summary><strong>What is auto-fetch?</strong></summary>

Auto-fetch runs `git fetch --all --quiet` in the background on every `graft` invocation, keeping your repos up to date without manual fetching.

```bash
# Enable for the current repo
graft scan auto-fetch enable

# Enable for a named repo
graft scan auto-fetch enable my-app

# See auto-fetch status for all repos
graft scan auto-fetch list
```

Auto-fetch is rate-limited to once every 15 minutes per repo. Fetch failures are silently skipped — one repo failing doesn't block others.

See [Scan & Discovery](./cli/scan.md#auto-fetch-commands) for full details.

</details>

---

<details>
<summary><strong>What does <code>graft status</code> show?</strong></summary>

`graft status` gives a compact overview of all discovered repos:

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

Each repo shows: current branch, clean/dirty status, ahead/behind remote, active stack info, and worktree count.

Use `graft status <reponame>` for detailed output including the full stack graph and worktree list.

See [Status](./cli/status.md) for full details.

</details>
