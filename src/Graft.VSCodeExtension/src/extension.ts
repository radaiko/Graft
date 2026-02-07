import * as vscode from "vscode";
import { GraftCli } from "./cli.js";
import { StackReader } from "./stackReader.js";
import { StackTreeProvider } from "./stackTreeProvider.js";
import { StatusBar } from "./statusBar.js";
import { FileWatcher } from "./fileWatcher.js";
import { registerCommands } from "./commands.js";
import { resolveGitCommonDir } from "./git.js";

export async function activate(
  context: vscode.ExtensionContext
): Promise<void> {
  const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
  if (!workspaceRoot) return;

  // Resolve git directory for file watching
  let gitCommonDir: string;
  try {
    gitCommonDir = await resolveGitCommonDir(workspaceRoot);
  } catch {
    // Not a git repo — register commands so stackInit can still be triggered,
    // but skip tree/watcher/status bar
    const cli = new GraftCli();
    const reader = new StackReader(workspaceRoot);
    const treeProvider = new StackTreeProvider(reader);
    const treeView = vscode.window.createTreeView("graftStacks", {
      treeDataProvider: treeProvider,
    });
    context.subscriptions.push(treeView, treeProvider);
    registerCommands(context, cli, treeProvider, () => workspaceRoot);
    return;
  }

  const graftDir = `${gitCommonDir}/graft`;

  // Initialize components
  const cli = new GraftCli();
  const reader = new StackReader(workspaceRoot);
  const treeProvider = new StackTreeProvider(reader);
  const statusBar = new StatusBar();
  const fileWatcher = new FileWatcher(() => {
    treeProvider.refresh();
    updateStatusBar();
  });

  // Register tree view
  const treeView = vscode.window.createTreeView("graftStacks", {
    treeDataProvider: treeProvider,
  });

  // Start file watcher
  fileWatcher.watch(graftDir, gitCommonDir);

  // Register commands
  registerCommands(context, cli, treeProvider, () => workspaceRoot);

  // Update status bar after tree refreshes
  context.subscriptions.push(
    treeProvider.onDidChangeTreeData(() => {
      setTimeout(updateStatusBar, 100);
    })
  );

  async function updateStatusBar(): Promise<void> {
    // Re-read state for status bar
    try {
      const state = await reader.readState();
      statusBar.update(state);
    } catch {
      statusBar.update(null);
    }
  }

  // Push disposables
  context.subscriptions.push(treeView, treeProvider, statusBar, fileWatcher);

  // Check CLI availability
  try {
    await cli.findBinary();
  } catch {
    vscode.window.showWarningMessage(
      "Graft CLI not found. Install it with `brew install graft` or set graft.cliPath in settings.",
      "Open Settings"
    ).then((action) => {
      if (action === "Open Settings") {
        vscode.commands.executeCommand(
          "workbench.action.openSettings",
          "graft.cliPath"
        );
      }
    });
  }

  // Initial status bar update
  updateStatusBar();
}

export function deactivate(): void {
  // Disposables handled by context.subscriptions
}
