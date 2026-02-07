import * as path from "node:path";
import * as vscode from "vscode";
import { GraftCli } from "./cli.js";
import { StackReader } from "./stackReader.js";
import { StackTreeProvider } from "./stackTreeProvider.js";
import { StatusBar } from "./statusBar.js";
import { FileWatcher } from "./fileWatcher.js";
import { registerCommands } from "./commands.js";
import { resolveGitCommonDir, resolveGitDir } from "./git.js";

export async function activate(
  context: vscode.ExtensionContext
): Promise<void> {
  const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
  if (!workspaceRoot) return;

  // Resolve git directory for file watching
  let gitCommonDir: string;
  let gitDir: string;
  try {
    [gitCommonDir, gitDir] = await Promise.all([
      resolveGitCommonDir(workspaceRoot),
      resolveGitDir(workspaceRoot),
    ]);
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

  const graftDir = path.join(gitCommonDir, "graft");

  // Initialize components
  const cli = new GraftCli();
  const reader = new StackReader(workspaceRoot);
  const treeProvider = new StackTreeProvider(reader);
  const statusBar = new StatusBar();
  const fileWatcher = new FileWatcher(() => {
    treeProvider.refreshImmediate();
  });

  // Register tree view
  const treeView = vscode.window.createTreeView("graftStacks", {
    treeDataProvider: treeProvider,
  });

  // Start file watcher — pass per-worktree git dir for HEAD watching
  fileWatcher.watch(graftDir, gitDir);

  // Register commands
  registerCommands(context, cli, treeProvider, () => workspaceRoot);

  // Update status bar after tree refreshes
  context.subscriptions.push(
    treeProvider.onDidChangeTreeData(() => {
      setTimeout(updateStatusBar, 100);
    })
  );

  // Invalidate cached CLI path on settings change
  context.subscriptions.push(
    vscode.workspace.onDidChangeConfiguration((e) => {
      if (e.affectsConfiguration("graft.cliPath")) {
        cli.resetBinaryPath();
      }
    })
  );

  async function updateStatusBar(): Promise<void> {
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
    void Promise.resolve(
      vscode.window.showWarningMessage(
        "Graft CLI not found. Install it or set graft.cliPath in settings.",
        "Open Settings"
      )
    ).then((action) => {
      if (action === "Open Settings") {
        vscode.commands.executeCommand(
          "workbench.action.openSettings",
          "graft.cliPath"
        );
      }
    }).catch(() => { /* best-effort */ });
  }

  // Initial status bar update
  updateStatusBar();
}

export function deactivate(): void {
  // Disposables handled by context.subscriptions
}
