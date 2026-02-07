import { execFile } from "node:child_process";
import { readFile, stat } from "node:fs/promises";
import * as path from "node:path";

export interface GitResult {
  exitCode: number;
  stdout: string;
  stderr: string;
}

export async function runGit(
  args: string[],
  cwd: string
): Promise<GitResult> {
  return new Promise((resolve) => {
    execFile("git", args, { cwd, timeout: 30_000 }, (error, stdout, stderr) => {
      const exitCode = error
        ? (error as NodeJS.ErrnoException & { status?: number })?.status ?? 1
        : 0;
      resolve({
        exitCode,
        stdout: (stdout ?? "").trimEnd(),
        stderr: (stderr ?? "").trimEnd(),
      });
    });
  });
}

/**
 * Port of GitRunner.ResolveGitCommonDir from C#.
 * Resolves the shared .git directory (handles worktrees where .git is a file).
 */
export async function resolveGitCommonDir(workingDir: string): Promise<string> {
  const dotGit = path.join(workingDir, ".git");

  // Regular repo: .git is a directory
  try {
    const s = await stat(dotGit);
    if (s.isDirectory()) {
      return dotGit;
    }
  } catch {
    // Not a directory, check if it's a file
  }

  // Worktree or submodule: .git is a file containing "gitdir: <path>"
  try {
    const content = (await readFile(dotGit, "utf-8")).trim();
    if (content.startsWith("gitdir: ")) {
      let gitDir = content.slice("gitdir: ".length);
      if (!path.isAbsolute(gitDir)) {
        gitDir = path.resolve(workingDir, gitDir);
      }

      // Check for commondir file (worktree-specific git dir points to shared dir)
      const commonDirFile = path.join(gitDir, "commondir");
      try {
        const commonDir = (await readFile(commonDirFile, "utf-8")).trim();
        if (path.isAbsolute(commonDir)) {
          return commonDir;
        }
        return path.resolve(gitDir, commonDir);
      } catch {
        return gitDir;
      }
    }
  } catch {
    // Not a file either
  }

  throw new Error(`'${workingDir}' is not a git repository`);
}

/**
 * Port of GitRunner.ResolveGitDir from C#.
 * Returns the per-worktree git dir (not the shared common dir).
 * For regular repos this is the same as resolveGitCommonDir.
 */
export async function resolveGitDir(workingDir: string): Promise<string> {
  const dotGit = path.join(workingDir, ".git");

  try {
    const s = await stat(dotGit);
    if (s.isDirectory()) {
      return dotGit;
    }
  } catch {
    // Not a directory
  }

  try {
    const content = (await readFile(dotGit, "utf-8")).trim();
    if (content.startsWith("gitdir: ")) {
      let gitDir = content.slice("gitdir: ".length);
      if (!path.isAbsolute(gitDir)) {
        gitDir = path.resolve(workingDir, gitDir);
      }
      return gitDir;
    }
  } catch {
    // Not a file
  }

  throw new Error(`'${workingDir}' is not a git repository`);
}

export async function getCurrentBranch(cwd: string): Promise<string> {
  const result = await runGit(["rev-parse", "--abbrev-ref", "HEAD"], cwd);
  return result.exitCode === 0 ? result.stdout.trim() : "";
}

export async function branchExists(
  branch: string,
  cwd: string
): Promise<boolean> {
  const result = await runGit(
    ["rev-parse", "--verify", `refs/heads/${branch}`],
    cwd
  );
  return result.exitCode === 0;
}

export async function getCommitCount(
  from: string,
  to: string,
  cwd: string
): Promise<number> {
  const result = await runGit(["rev-list", "--count", `${from}..${to}`], cwd);
  if (result.exitCode !== 0) return 0;
  const n = parseInt(result.stdout.trim(), 10);
  return isNaN(n) ? 0 : n;
}

export async function getMergeBase(
  a: string,
  b: string,
  cwd: string
): Promise<string | null> {
  const result = await runGit(["merge-base", a, b], cwd);
  return result.exitCode === 0 ? result.stdout.trim() : null;
}

export async function getRevParse(
  ref: string,
  cwd: string
): Promise<string | null> {
  const result = await runGit(["rev-parse", ref], cwd);
  return result.exitCode === 0 ? result.stdout.trim() : null;
}
