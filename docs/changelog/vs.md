---
layout: default
title: Visual Studio
parent: Changelog
nav_order: 3
---

# Visual Studio Extension Changelog

---

## [0.1.0] -- Initial release

### Added

- Visual Studio 2022/2026 extension project scaffolding (Phase 1)
- Stack Explorer tool window with TreeView showing stacks and branches
- Status bar integration displaying active stack and top branch
- Tools > Graft menu with Init, Push, Pop, Sync, Switch, Log, and Open Stack Explorer commands
- CLI wrapper service with binary detection and async command execution
- Direct TOML file reading for stack data (no CLI parsing for display)
- FileSystemWatcher with debouncing for live `.git/graft/` change detection
- Dedicated "Graft" output window pane for command logging
- Reusable input dialog with TextBox/ComboBox and optional checkbox
- **CI:** VSIX build and publish pipeline (GitHub Releases + VS Marketplace)
