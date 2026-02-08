import { parse } from "smol-toml";
import type { StackDefinition, StackBranch, PullRequestRef, PrState } from "./types.js";

function parsePullRequest(rec: Record<string, unknown>): PullRequestRef | undefined {
  const prNumber = rec["pr_number"];
  const prUrl = rec["pr_url"];
  if (prNumber == null || typeof prUrl !== "string") return undefined;

  const num = Number(prNumber);
  if (Number.isNaN(num)) return undefined;

  const prStateRaw = rec["pr_state"];
  let prState: PrState = "open";
  if (prStateRaw === "merged") prState = "merged";
  else if (prStateRaw === "closed") prState = "closed";

  return { number: num, url: prUrl, state: prState };
}

function parseBranches(branchesArr: unknown): StackBranch[] {
  const branches: StackBranch[] = [];
  if (!Array.isArray(branchesArr)) return branches;

  for (const entry of branchesArr) {
    if (typeof entry !== "object" || entry === null) continue;
    const rec = entry as Record<string, unknown>;
    const branchName = rec["name"];
    if (typeof branchName !== "string") continue;

    const branch: StackBranch = { name: branchName };
    branch.pr = parsePullRequest(rec);
    branches.push(branch);
  }

  return branches;
}

/**
 * Parse a stack TOML file into a StackDefinition.
 * Matches the field names from C# ConfigLoader.LoadStack.
 */
export function parseStackToml(
  tomlContent: string,
  fallbackName: string
): StackDefinition {
  // .NET's Encoding.UTF8 writes a BOM — strip it before parsing
  const table = parse(tomlContent.replace(/^\uFEFF/, ""));

  const trunk = table["trunk"];
  if (typeof trunk !== "string") {
    throw new TypeError(`Stack is missing required field 'trunk'`);
  }

  const name =
    typeof table["name"] === "string" ? table["name"] : fallbackName;

  const branches = parseBranches(table["branches"]);

  return {
    name,
    trunk,
    branches,
    createdAt: typeof table["created_at"] === "string" ? table["created_at"] : undefined,
    updatedAt: typeof table["updated_at"] === "string" ? table["updated_at"] : undefined,
  };
}
