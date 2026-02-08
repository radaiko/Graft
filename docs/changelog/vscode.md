---
title: VS Code Extension
---

# VS Code Extension Changelog

Install from the [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=radaiko.graft-vscode) or download `.vsix` from [GitHub Releases](https://github.com/radaiko/Graft/releases).

---

## [0.1.0] -- Initial release

### Added

- Activity bar with tree view showing all stacks, trunk, and branches with sync status icons
- 13 command palette commands -- init, switch, push, pop, drop, sync, commit, delete, continue, abort, checkout, open PR, refresh
- Status bar showing active stack name and sync status (synced/stale/conflict)
- File watcher on `.git/graft/`, refs, and index for automatic tree refresh
- Context menus on stacks (push, pop, sync, commit, delete) and branches (drop, checkout, open PR)
- Inline action buttons on stacks (push, sync) and branches (checkout, open PR)
- `graft.cliPath` setting for custom CLI binary location
