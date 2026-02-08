import * as vscode from "vscode";
import type { StackDisplayInfo, BranchDisplayInfo } from "./types.js";

export class StackItem extends vscode.TreeItem {
  constructor(public readonly stack: StackDisplayInfo) {
    super(stack.name, vscode.TreeItemCollapsibleState.Expanded);

    if (stack.isActive) {
      this.iconPath = new vscode.ThemeIcon("star-full");
      this.contextValue = "graft.stack";
    } else {
      this.iconPath = new vscode.ThemeIcon("star-empty");
      this.contextValue = "graft.stack.inactive";
    }

    if (stack.hasConflict) {
      this.description = "conflict";
      this.iconPath = new vscode.ThemeIcon(
        "warning",
        new vscode.ThemeColor("editorWarning.foreground")
      );
    }
  }
}

export class TrunkItem extends vscode.TreeItem {
  constructor(public readonly trunkName: string) {
    super(trunkName, vscode.TreeItemCollapsibleState.None);
    this.iconPath = new vscode.ThemeIcon("git-branch");
    this.description = "trunk";
    this.contextValue = "graft.trunk";
  }
}

export class BranchItem extends vscode.TreeItem {
  constructor(
    public readonly branch: BranchDisplayInfo,
    public readonly stackName: string
  ) {
    super(branch.name, vscode.TreeItemCollapsibleState.None);

    // Icon based on sync status
    if (branch.needsMerge) {
      this.iconPath = new vscode.ThemeIcon(
        "warning",
        new vscode.ThemeColor("editorWarning.foreground")
      );
    } else {
      this.iconPath = new vscode.ThemeIcon(
        "check",
        new vscode.ThemeColor("testing.iconPassed")
      );
    }

    // Description shows commit count and sync status
    const parts: string[] = [];
    if (branch.commitCount > 0) {
      parts.push(
        `${branch.commitCount} commit${branch.commitCount === 1 ? "" : "s"}`
      );
    }
    if (branch.needsMerge) {
      parts.push("stale");
    }
    if (branch.isHead) {
      parts.push("HEAD");
    }
    this.description = parts.join(" · ");

    // Click to checkout
    this.command = {
      command: "graft.checkoutBranch",
      title: "Checkout Branch",
      arguments: [branch.name],
    };

    // Context value for menus
    if (branch.pr) {
      this.contextValue = "graft.branch.withPr";
    } else if (branch.isHead) {
      this.contextValue = "graft.branch.head";
    } else {
      this.contextValue = "graft.branch";
    }
  }
}
