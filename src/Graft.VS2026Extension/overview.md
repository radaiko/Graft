# Graft - Stacked Branches for Visual Studio

Graft brings stacked branch workflows and git worktree management directly into Visual Studio.

## Features

- **Stack Explorer** — A docked tool window showing all your stacks and their branches at a glance.
- **Stack Commands** — Initialize stacks, push/pop branches, sync, and switch stacks from the Tools menu.
- **Status Bar** — See your active stack and current branch in the VS status bar.
- **Auto-Refresh** — The UI updates automatically when stack files change on disk.
- **PR Integration** — View PR numbers and status for each branch in the stack.

## Requirements

- [Graft CLI](https://github.com/radaiko/Graft) must be installed and available on your PATH.
- Visual Studio 2022 (v17.0+), Community, Professional, or Enterprise edition.

## Getting Started

1. Install the Graft CLI: download from [GitHub Releases](https://github.com/radaiko/Graft/releases) and run `graft install`.
2. Install this extension from the Visual Studio Marketplace.
3. Open a git repository in Visual Studio.
4. Use **Tools > Graft > Initialize Stack** to create your first stack.
5. Open the Stack Explorer from **Tools > Graft > Open Stack Explorer** to visualize your branches.

## Documentation

Full documentation is available at [github.com/radaiko/Graft](https://github.com/radaiko/Graft).

## Feedback

Report issues or request features on [GitHub Issues](https://github.com/radaiko/Graft/issues).
