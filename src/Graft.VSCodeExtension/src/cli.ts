import { execFile } from "node:child_process";
import * as vscode from "vscode";

export class GraftCli {
  private binaryPath: string | null = null;

  async findBinary(): Promise<string> {
    // Check user setting first
    const configured = vscode.workspace
      .getConfiguration("graft")
      .get<string>("cliPath");
    if (configured) {
      this.binaryPath = configured;
      return configured;
    }

    // Try `which graft`, then `which gt`
    for (const name of ["graft", "gt"]) {
      const found = await this.which(name);
      if (found) {
        this.binaryPath = found;
        return found;
      }
    }

    throw new Error(
      "Graft CLI not found. Install it or set graft.cliPath in settings."
    );
  }

  resetBinaryPath(): void {
    this.binaryPath = null;
  }

  private which(name: string): Promise<string | null> {
    const cmd = process.platform === "win32" ? "where" : "which";
    return new Promise((resolve) => {
      execFile(cmd, [name], (error, stdout) => {
        const out = (stdout ?? "").trim();
        if (error || !out) {
          resolve(null);
        } else {
          // On Windows, 'where' may return multiple paths (one per line)
          resolve(out.split(/\r?\n/)[0]);
        }
      });
    });
  }

  async run(args: string[], cwd: string): Promise<string> {
    if (!this.binaryPath) {
      await this.findBinary();
    }
    return new Promise((resolve, reject) => {
      execFile(
        this.binaryPath!,
        args,
        { cwd, timeout: 60_000 },
        (error, stdout, stderr) => {
          if (error) {
            const msg = (stderr ?? "").trim() || (stdout ?? "").trim() || error.message;
            reject(new Error(msg));
          } else {
            resolve((stdout ?? "").trimEnd());
          }
        }
      );
    });
  }

  async stackInit(name: string, cwd: string, baseBranch?: string): Promise<void> {
    const args = ["stack", "init", name];
    if (baseBranch) {
      args.push("-b", baseBranch);
    }
    await this.run(args, cwd);
  }

  async stackSwitch(name: string, cwd: string): Promise<void> {
    await this.run(["stack", "switch", name], cwd);
  }

  async stackPush(branch: string, cwd: string, create: boolean): Promise<void> {
    const args = ["stack", "push", branch];
    if (create) args.push("-c");
    await this.run(args, cwd);
  }

  async stackPop(cwd: string): Promise<void> {
    await this.run(["stack", "pop"], cwd);
  }

  async stackDrop(branch: string, cwd: string): Promise<void> {
    await this.run(["stack", "drop", branch], cwd);
  }

  async stackSync(cwd: string): Promise<void> {
    await this.run(["stack", "sync"], cwd);
  }

  async stackCommit(message: string, cwd: string, branch?: string): Promise<void> {
    const args = ["stack", "commit", "-m", message];
    if (branch) args.push("-b", branch);
    await this.run(args, cwd);
  }

  async stackDel(name: string, cwd: string, force: boolean): Promise<void> {
    const args = ["stack", "del", name];
    if (force) args.push("-f");
    await this.run(args, cwd);
  }

  async continueOp(cwd: string): Promise<void> {
    await this.run(["--continue"], cwd);
  }

  async abortOp(cwd: string): Promise<void> {
    await this.run(["--abort"], cwd);
  }

  async checkoutBranch(branch: string, cwd: string): Promise<void> {
    const gitPath = vscode.workspace.getConfiguration("git").get<string>("path") || "git";
    return new Promise((resolve, reject) => {
      execFile(gitPath, ["checkout", branch], { cwd, timeout: 30_000 }, (error, _stdout, stderr) => {
        if (error) {
          reject(new Error((stderr ?? "").trim() || error.message));
        } else {
          resolve();
        }
      });
    });
  }
}
