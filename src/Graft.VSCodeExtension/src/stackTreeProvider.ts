import * as vscode from "vscode";
import { StackReader } from "./stackReader.js";
import { StackItem, TrunkItem, BranchItem } from "./stackTreeItems.js";
import type { GraftState } from "./types.js";

export class StackTreeProvider
  implements vscode.TreeDataProvider<vscode.TreeItem>
{
  private readonly _onDidChangeTreeData = new vscode.EventEmitter<
    vscode.TreeItem | undefined | void
  >();
  readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

  private state: GraftState | null = null;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(private readonly reader: StackReader) {}

  refresh(): void {
    // Debounce: collapse multiple rapid refresh calls into one
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
    }
    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = null;
      this.state = null;
      this._onDidChangeTreeData.fire();
    }, 300);
  }

  refreshImmediate(): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }
    this.state = null;
    this._onDidChangeTreeData.fire();
  }

  private async ensureState(): Promise<GraftState> {
    if (!this.state) {
      try {
        this.state = await this.reader.readState();
      } catch (err) {
        console.error("[Graft] Failed to read stack state:", err);
        this.state = { stacks: [], activeStack: null, currentBranch: "" };
      }
    }
    return this.state;
  }

  getTreeItem(element: vscode.TreeItem): vscode.TreeItem {
    return element;
  }

  async getChildren(
    element?: vscode.TreeItem
  ): Promise<vscode.TreeItem[]> {
    const state = await this.ensureState();

    // Root level: list all stacks
    if (!element) {
      if (state.stacks.length === 0) {
        const item = new vscode.TreeItem("No stacks yet");
        item.description = "Run 'Graft: Init Stack' to create one";
        return [item];
      }
      return state.stacks.map((s) => new StackItem(s));
    }

    // Under a stack: show trunk + branches
    if (element instanceof StackItem) {
      const stack = element.stack;
      const items: vscode.TreeItem[] = [new TrunkItem(stack.trunk)];
      for (const branch of stack.branches) {
        items.push(new BranchItem(branch, stack.name));
      }
      return items;
    }

    return [];
  }

  getState(): GraftState | null {
    return this.state;
  }

  dispose(): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
    }
    this._onDidChangeTreeData.dispose();
  }
}
