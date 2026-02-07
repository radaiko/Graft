import * as vscode from "vscode";

export class FileWatcher {
  private watchers: vscode.FileSystemWatcher[] = [];
  private debounceTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly debounceMs = 300;

  constructor(private readonly onChanged: () => void) {}

  watch(graftDir: string, gitDir: string): void {
    this.dispose();

    // Watch stacks/*.toml
    const stacksPattern = new vscode.RelativePattern(
      vscode.Uri.file(graftDir),
      "stacks/*.toml"
    );
    const stacksWatcher = vscode.workspace.createFileSystemWatcher(stacksPattern);
    stacksWatcher.onDidChange(() => this.trigger());
    stacksWatcher.onDidCreate(() => this.trigger());
    stacksWatcher.onDidDelete(() => this.trigger());
    this.watchers.push(stacksWatcher);

    // Watch active-stack
    const activeStackPattern = new vscode.RelativePattern(
      vscode.Uri.file(graftDir),
      "active-stack"
    );
    const activeWatcher = vscode.workspace.createFileSystemWatcher(activeStackPattern);
    activeWatcher.onDidChange(() => this.trigger());
    activeWatcher.onDidCreate(() => this.trigger());
    activeWatcher.onDidDelete(() => this.trigger());
    this.watchers.push(activeWatcher);

    // Watch operation.toml (conflict state)
    const opPattern = new vscode.RelativePattern(
      vscode.Uri.file(graftDir),
      "operation.toml"
    );
    const opWatcher = vscode.workspace.createFileSystemWatcher(opPattern);
    opWatcher.onDidChange(() => this.trigger());
    opWatcher.onDidCreate(() => this.trigger());
    opWatcher.onDidDelete(() => this.trigger());
    this.watchers.push(opWatcher);

    // Watch .git/HEAD (branch switches)
    const headPattern = new vscode.RelativePattern(
      vscode.Uri.file(gitDir),
      "HEAD"
    );
    const headWatcher = vscode.workspace.createFileSystemWatcher(headPattern);
    headWatcher.onDidChange(() => this.trigger());
    this.watchers.push(headWatcher);
  }

  private trigger(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
    }
    this.debounceTimer = setTimeout(() => {
      this.debounceTimer = null;
      this.onChanged();
    }, this.debounceMs);
  }

  dispose(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    for (const w of this.watchers) {
      w.dispose();
    }
    this.watchers = [];
  }
}
