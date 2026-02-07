import { parse } from "smol-toml";
import type { StackDefinition, StackBranch, PullRequestRef, PrState } from "./types.js";

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
    throw new Error(`Stack is missing required field 'trunk'`);
  }

  const name =
    typeof table["name"] === "string" ? table["name"] : fallbackName;

  const branches: StackBranch[] = [];
  const branchesArr = table["branches"];
  if (Array.isArray(branchesArr)) {
    for (const entry of branchesArr) {
      if (typeof entry !== "object" || entry === null) continue;
      const rec = entry as Record<string, unknown>;
      const branchName = rec["name"];
      if (typeof branchName !== "string") continue;

      const branch: StackBranch = { name: branchName };

      const prNumber = rec["pr_number"];
      const prUrl = rec["pr_url"];
      if (
        prNumber != null &&
        typeof prUrl === "string"
      ) {
        const num = Number(prNumber);
        if (!isNaN(num)) {
          const prStateRaw = rec["pr_state"];
          let prState: PrState = "open";
          if (prStateRaw === "merged") prState = "merged";
          else if (prStateRaw === "closed") prState = "closed";

          const pr: PullRequestRef = {
            number: num,
            url: prUrl,
            state: prState,
          };
          branch.pr = pr;
        }
      }

      branches.push(branch);
    }
  }

  return {
    name,
    trunk,
    branches,
    createdAt: typeof table["created_at"] === "string" ? table["created_at"] : undefined,
    updatedAt: typeof table["updated_at"] === "string" ? table["updated_at"] : undefined,
  };
}
