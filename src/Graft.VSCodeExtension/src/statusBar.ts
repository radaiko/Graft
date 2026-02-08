import * as vscode from "vscode";
import type { GraftState } from "./types.js";

export class StatusBar {
  private readonly stackItem: vscode.StatusBarItem;
  private readonly syncItem: vscode.StatusBarItem;

  constructor() {
    this.stackItem = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Left,
      100
    );
    this.stackItem.command = "graft.stackSwitch";
    this.stackItem.tooltip = "Active Graft stack (click to switch)";

    this.syncItem = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Left,
      99
    );
    this.syncItem.command = "graft.stackSync";
    this.syncItem.tooltip = "Stack sync status (click to sync)";
  }

  update(state: GraftState | null): void {
    if (!state || state.stacks.length === 0) {
      this.stackItem.hide();
      this.syncItem.hide();
      return;
    }

    // Stack name
    if (state.activeStack) {
      this.stackItem.text = `$(layers) ${state.activeStack}`;
      this.stackItem.show();
    } else {
      this.stackItem.text = "$(layers) No active stack";
      this.stackItem.show();
    }

    // Sync status — count stale branches across the active stack
    const activeStack = state.stacks.find((s) => s.isActive);
    if (activeStack) {
      const staleCount = activeStack.branches.filter(
        (b) => b.needsMerge
      ).length;

      if (activeStack.hasConflict) {
        this.syncItem.text = "$(error) conflict";
        this.syncItem.backgroundColor = new vscode.ThemeColor(
          "statusBarItem.errorBackground"
        );
      } else if (staleCount > 0) {
        this.syncItem.text = `$(warning) ${staleCount} stale`;
        this.syncItem.backgroundColor = new vscode.ThemeColor(
          "statusBarItem.warningBackground"
        );
      } else {
        this.syncItem.text = "$(check) synced";
        this.syncItem.backgroundColor = undefined;
      }
      this.syncItem.show();
    } else {
      this.syncItem.hide();
    }
  }

  dispose(): void {
    this.stackItem.dispose();
    this.syncItem.dispose();
  }
}
