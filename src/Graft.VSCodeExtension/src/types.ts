export interface PullRequestRef {
  number: number;
  url: string;
  state: PrState;
}

export type PrState = "open" | "merged" | "closed";

export interface StackBranch {
  name: string;
  pr?: PullRequestRef;
}

export interface StackDefinition {
  name: string;
  trunk: string;
  branches: StackBranch[];
  createdAt?: string;
  updatedAt?: string;
}

export interface BranchDisplayInfo {
  name: string;
  pr?: PullRequestRef;
  isHead: boolean;
  commitCount: number;
  needsMerge: boolean;
}

export interface StackDisplayInfo {
  name: string;
  trunk: string;
  isActive: boolean;
  hasConflict: boolean;
  branches: BranchDisplayInfo[];
}

export interface GraftState {
  stacks: StackDisplayInfo[];
  activeStack: string | null;
  currentBranch: string;
}
