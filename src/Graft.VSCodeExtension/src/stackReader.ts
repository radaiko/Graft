import { readFile, readdir } from "node:fs/promises";
import * as path from "node:path";
import { parse } from "smol-toml";
import { parseStackToml } from "./toml.js";
import {
  resolveGitCommonDir,
  getCurrentBranch,
  branchExists,
  getCommitCount,
  getMergeBase,
  getRevParse,
} from "./git.js";
import type {
  GraftState,
  StackDisplayInfo,
  BranchDisplayInfo,
} from "./types.js";

export class StackReader {
  private gitCommonDir: string | null = null;

  constructor(private readonly workspaceRoot: string) {}

  async getGraftDir(): Promise<string> {
    if (!this.gitCommonDir) {
      this.gitCommonDir = await resolveGitCommonDir(this.workspaceRoot);
    }
    return path.join(this.gitCommonDir, "graft");
  }

  async readState(): Promise<GraftState> {
    const graftDir = await this.getGraftDir();
    const stacksDir = path.join(graftDir, "stacks");

    const [activeStack, currentBranch, stackNames] = await Promise.all([
      this.readActiveStack(graftDir),
      getCurrentBranch(this.workspaceRoot),
      this.listStackFiles(stacksDir),
    ]);

    const results = await Promise.all(
      stackNames.map((name) =>
        this.readStack(name, stacksDir, activeStack, currentBranch).catch(() => null)
      )
    );
    const stacks = results.filter((s): s is StackDisplayInfo => s !== null);

    return { stacks, activeStack, currentBranch };
  }

  private async readActiveStack(graftDir: string): Promise<string | null> {
    try {
      const content = await readFile(
        path.join(graftDir, "active-stack"),
        "utf-8"
      );
      const trimmed = content.trim();
      return trimmed || null;
    } catch {
      return null;
    }
  }

  private async listStackFiles(stacksDir: string): Promise<string[]> {
    try {
      const files = await readdir(stacksDir);
      return files
        .filter((f) => f.endsWith(".toml"))
        .map((f) => f.slice(0, -5))
        .sort();
    } catch {
      return [];
    }
  }

  private async readStack(
    name: string,
    stacksDir: string,
    activeStack: string | null,
    currentBranch: string
  ): Promise<StackDisplayInfo> {
    const filePath = path.join(stacksDir, `${name}.toml`);
    const content = await readFile(filePath, "utf-8");
    const def = parseStackToml(content, name);

    const isActive = def.name === activeStack;
    const hasConflict = await this.checkConflict(def.name);

    const branches: BranchDisplayInfo[] = [];
    let parentBranch = def.trunk;

    for (const branch of def.branches) {
      const info = await this.computeBranchInfo(
        branch.name,
        parentBranch,
        currentBranch,
        branch.pr
          ? { number: branch.pr.number, url: branch.pr.url, state: branch.pr.state }
          : undefined
      );
      branches.push(info);
      parentBranch = branch.name;
    }

    return {
      name: def.name,
      trunk: def.trunk,
      isActive,
      hasConflict,
      branches,
    };
  }

  /**
   * Compute sync status for a branch. Same logic as StackHandler.cs:55-66.
   */
  private async computeBranchInfo(
    branchName: string,
    parentBranch: string,
    currentBranch: string,
    pr?: { number: number; url: string; state: string }
  ): Promise<BranchDisplayInfo> {
    const isHead = branchName === currentBranch;
    const info: BranchDisplayInfo = {
      name: branchName,
      pr: pr ? { number: pr.number, url: pr.url, state: pr.state as "open" | "merged" | "closed" } : undefined,
      isHead,
      commitCount: 0,
      needsMerge: false,
    };

    const exists = await branchExists(branchName, this.workspaceRoot);
    if (!exists) return info;

    const [commitCount, mergeBase, parentTip] = await Promise.all([
      getCommitCount(parentBranch, branchName, this.workspaceRoot),
      getMergeBase(parentBranch, branchName, this.workspaceRoot),
      getRevParse(parentBranch, this.workspaceRoot),
    ]);

    info.commitCount = commitCount;
    if (mergeBase && parentTip && mergeBase !== parentTip) {
      info.needsMerge = true;
    }

    return info;
  }

  private async checkConflict(stackName: string): Promise<boolean> {
    try {
      const graftDir = await this.getGraftDir();
      const opPath = path.join(graftDir, "operation.toml");
      const content = await readFile(opPath, "utf-8");
      const table = parse(content);
      return table["stack"] === stackName;
    } catch {
      return false;
    }
  }
}
