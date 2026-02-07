import * as vscode from "vscode";
import { GraftCli } from "./cli.js";
import { StackTreeProvider } from "./stackTreeProvider.js";
import { StackItem, BranchItem } from "./stackTreeItems.js";

export function registerCommands(
  context: vscode.ExtensionContext,
  cli: GraftCli,
  treeProvider: StackTreeProvider,
  getWorkspaceRoot: () => string
): void {
  const cwd = getWorkspaceRoot;

  function reg(
    command: string,
    handler: (...args: unknown[]) => Promise<void>
  ): void {
    context.subscriptions.push(
      vscode.commands.registerCommand(command, handler)
    );
  }

  reg("graft.refresh", async () => {
    treeProvider.refreshImmediate();
  });

  reg("graft.stackInit", async () => {
    const name = await vscode.window.showInputBox({
      prompt: "Stack name",
      placeHolder: "my-feature",
      validateInput: (v) =>
        v.trim() ? null : "Stack name is required",
    });
    if (!name) return;

    const baseBranch = await vscode.window.showInputBox({
      prompt: "Base branch (leave empty for current branch)",
      placeHolder: "main",
    });

    try {
      await cli.stackInit(name.trim(), cwd(), baseBranch?.trim() || undefined);
      treeProvider.refreshImmediate();
      vscode.window.showInformationMessage(`Stack '${name.trim()}' created.`);
    } catch (e) {
      vscode.window.showErrorMessage(`Init failed: ${errorMessage(e)}`);
    }
  });

  reg("graft.stackSwitch", async (...args: unknown[]) => {
    let name: string | undefined;

    // Called from tree context menu
    if (args[0] instanceof StackItem) {
      name = args[0].stack.name;
    } else {
      // Called from command palette — show picker
      const state = treeProvider.getState();
      const stacks = state?.stacks.map((s) => s.name) ?? [];
      if (stacks.length === 0) {
        vscode.window.showWarningMessage("No stacks to switch to.");
        return;
      }
      name = await vscode.window.showQuickPick(stacks, {
        placeHolder: "Select stack to switch to",
      });
    }
    if (!name) return;

    try {
      await cli.stackSwitch(name, cwd());
      treeProvider.refreshImmediate();
      vscode.window.showInformationMessage(`Switched to stack '${name}'.`);
    } catch (e) {
      vscode.window.showErrorMessage(`Switch failed: ${errorMessage(e)}`);
    }
  });

  reg("graft.stackPush", async () => {
    const branch = await vscode.window.showInputBox({
      prompt: "Branch name to push onto stack",
      placeHolder: "feature/my-branch",
      validateInput: (v) =>
        v.trim() ? null : "Branch name is required",
    });
    if (!branch) return;

    const createChoice = await vscode.window.showQuickPick(
      [
        { label: "Use existing branch", create: false },
        { label: "Create new branch", create: true },
      ],
      { placeHolder: "Does the branch exist?" }
    );
    if (!createChoice) return;

    try {
      await cli.stackPush(branch.trim(), cwd(), createChoice.create);
      treeProvider.refreshImmediate();
      vscode.window.showInformationMessage(
        `Branch '${branch.trim()}' pushed to stack.`
      );
    } catch (e) {
      vscode.window.showErrorMessage(`Push failed: ${errorMessage(e)}`);
    }
  });

  reg("graft.stackPop", async () => {
    try {
      await cli.stackPop(cwd());
      treeProvider.refreshImmediate();
      vscode.window.showInformationMessage("Top branch removed from stack.");
    } catch (e) {
      vscode.window.showErrorMessage(`Pop failed: ${errorMessage(e)}`);
    }
  });

  reg("graft.stackDrop", async (...args: unknown[]) => {
    let branchName: string | undefined;

    if (args[0] instanceof BranchItem) {
      branchName = args[0].branch.name;
    } else {
      branchName = await vscode.window.showInputBox({
        prompt: "Branch name to drop from stack",
        validateInput: (v) =>
          v.trim() ? null : "Branch name is required",
      });
    }
    if (!branchName) return;

    try {
      await cli.stackDrop(branchName, cwd());
      treeProvider.refreshImmediate();
      vscode.window.showInformationMessage(
        `Branch '${branchName}' dropped from stack.`
      );
    } catch (e) {
      vscode.window.showErrorMessage(`Drop failed: ${errorMessage(e)}`);
    }
  });

  reg("graft.stackSync", async () => {
    try {
      await vscode.window.withProgress(
        {
          location: vscode.ProgressLocation.Notification,
          title: "Syncing stack...",
          cancellable: false,
        },
        async () => {
          await cli.stackSync(cwd());
        }
      );
      treeProvider.refreshImmediate();
      vscode.window.showInformationMessage("Stack synced.");
    } catch (e) {
      const msg = errorMessage(e);
      if (msg.toLowerCase().includes("conflict")) {
        const action = await vscode.window.showWarningMessage(
          `Sync conflict: ${msg}`,
          "Continue",
          "Abort"
        );
        if (action === "Continue") {
          await vscode.commands.executeCommand("graft.continue");
        } else if (action === "Abort") {
          await vscode.commands.executeCommand("graft.abort");
        }
      } else {
        vscode.window.showErrorMessage(`Sync failed: ${msg}`);
      }
    }
  });

  reg("graft.stackCommit", async () => {
    const message = await vscode.window.showInputBox({
      prompt: "Commit message",
      placeHolder: "feat: add new feature",
      validateInput: (v) =>
        v.trim() ? null : "Commit message is required",
    });
    if (!message) return;

    const branch = await vscode.window.showInputBox({
      prompt: "Target branch (leave empty for current branch)",
    });

    try {
      await cli.stackCommit(
        message.trim(),
        cwd(),
        branch?.trim() || undefined
      );
      treeProvider.refreshImmediate();
      vscode.window.showInformationMessage("Committed to stack.");
    } catch (e) {
      vscode.window.showErrorMessage(`Commit failed: ${errorMessage(e)}`);
    }
  });

  reg("graft.stackDel", async (...args: unknown[]) => {
    let name: string | undefined;

    if (args[0] instanceof StackItem) {
      name = args[0].stack.name;
    } else {
      name = await vscode.window.showInputBox({
        prompt: "Stack name to delete",
        validateInput: (v) =>
          v.trim() ? null : "Stack name is required",
      });
    }
    if (!name) return;

    const confirm = await vscode.window.showWarningMessage(
      `Delete stack '${name}'?`,
      { modal: true },
      "Delete",
      "Force Delete"
    );
    if (!confirm) return;

    try {
      await cli.stackDel(name, cwd(), confirm === "Force Delete");
      treeProvider.refreshImmediate();
      vscode.window.showInformationMessage(`Stack '${name}' deleted.`);
    } catch (e) {
      vscode.window.showErrorMessage(`Delete failed: ${errorMessage(e)}`);
    }
  });

  reg("graft.continue", async () => {
    try {
      await cli.continueOp(cwd());
      treeProvider.refreshImmediate();
      vscode.window.showInformationMessage("Operation continued.");
    } catch (e) {
      vscode.window.showErrorMessage(`Continue failed: ${errorMessage(e)}`);
    }
  });

  reg("graft.abort", async () => {
    try {
      await cli.abortOp(cwd());
      treeProvider.refreshImmediate();
      vscode.window.showInformationMessage("Operation aborted.");
    } catch (e) {
      vscode.window.showErrorMessage(`Abort failed: ${errorMessage(e)}`);
    }
  });

  reg("graft.checkoutBranch", async (...args: unknown[]) => {
    let branchName: string | undefined;

    if (typeof args[0] === "string") {
      branchName = args[0];
    } else if (args[0] instanceof BranchItem) {
      branchName = args[0].branch.name;
    }
    if (!branchName) return;

    try {
      await cli.checkoutBranch(branchName, cwd());
      treeProvider.refreshImmediate();
    } catch (e) {
      vscode.window.showErrorMessage(`Checkout failed: ${errorMessage(e)}`);
    }
  });

  reg("graft.openPr", async (...args: unknown[]) => {
    if (args[0] instanceof BranchItem && args[0].branch.pr) {
      const url = args[0].branch.pr.url;
      vscode.env.openExternal(vscode.Uri.parse(url));
    }
  });
}

function errorMessage(e: unknown): string {
  if (e instanceof Error) return e.message;
  return String(e);
}
