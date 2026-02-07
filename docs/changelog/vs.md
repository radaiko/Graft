---
layout: default
title: Visual Studio
parent: Changelog
nav_order: 3
---

# Visual Studio Extension Changelog

---

## [Unreleased]

### Added

- **Extension:** Visual Studio 2022/2026 extension project scaffolding (Phase 1)
- **Extension:** Stack Explorer tool window with TreeView showing stacks and branches
- **Extension:** Status bar integration displaying active stack and top branch
- **Extension:** Tools > Graft menu with Init, Push, Pop, Sync, Switch, Log, and Open Stack Explorer commands
- **Extension:** CLI wrapper service with binary detection and async command execution
- **Extension:** Direct TOML file reading for stack data (no CLI parsing for display)
- **Extension:** FileSystemWatcher with debouncing for live `.git/graft/` change detection
- **Extension:** Dedicated "Graft" output window pane for command logging
- **Extension:** Reusable input dialog with TextBox/ComboBox and optional checkbox
- **CI:** VSIX build and publish pipeline (GitHub Releases + VS Marketplace)
