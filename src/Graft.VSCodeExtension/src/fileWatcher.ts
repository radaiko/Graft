import * as vscode from "vscode";

export class FileWatcher {
  private watchers: vscode.FileSystemWatcher[] = [];
  private debounceTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly debounceMs = 300;

  constructor(private readonly onChanged: () => void) {}

  watch(graftDir: string, gitDir: string, gitCommonDir: string): void {
    this.dispose();

    const addWatcher = (base: string, pattern: string) => {
      const w = vscode.workspace.createFileSystemWatcher(
        new vscode.RelativePattern(vscode.Uri.file(base), pattern)
      );
      w.onDidChange(() => this.trigger());
      w.onDidCreate(() => this.trigger());
      w.onDidDelete(() => this.trigger());
      this.watchers.push(w);
    };

    // Graft state files
    addWatcher(graftDir, "stacks/*.toml");
    addWatcher(graftDir, "active-stack");
    addWatcher(graftDir, "operation.toml");

    // Per-worktree: HEAD (branch switches)
    addWatcher(gitDir, "HEAD");

    // Shared refs: local branch tips (commits, merges, rebases)
    addWatcher(gitCommonDir, "refs/heads/**");

    // Packed refs (git gc packs loose refs into this file)
    addWatcher(gitCommonDir, "packed-refs");

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
