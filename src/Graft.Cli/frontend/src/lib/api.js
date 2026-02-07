async function request(method, path, body) {
  const opts = {
    method,
    headers: {},
  };
  if (body !== undefined) {
    opts.headers['Content-Type'] = 'application/json';
    opts.body = JSON.stringify(body);
  }
  const res = await fetch(path, opts);
  if (res.status === 204) return null;
  const data = await res.json();
  if (!res.ok) throw new Error(data.error || `Request failed: ${res.status}`);
  return data;
}

// Stack endpoints
export const listStacks = () => request('GET', '/api/stacks');
export const getStack = (name) => request('GET', `/api/stacks/${encodeURIComponent(name)}`);
export const initStack = (name, baseBranch) => request('POST', '/api/stacks', { name, baseBranch });
export const deleteStack = (name) => request('DELETE', `/api/stacks/${encodeURIComponent(name)}`);

// Stack operations (use active stack from server)
export const pushBranch = (branchName, createBranch = false) =>
  request('POST', '/api/stacks/push', { branchName, createBranch });
export const syncStack = () =>
  request('POST', '/api/stacks/sync');
export const commitToStack = (message, branch, amend = false) =>
  request('POST', '/api/stacks/commit', { message, branch, amend });

// Stack operations
export const popBranch = () => request('POST', '/api/stacks/pop');
export const dropBranch = (branchName) => request('POST', '/api/stacks/drop', { branchName });
export const shiftBranch = (branchName) => request('POST', '/api/stacks/shift', { branchName });

// Active stack
export const getActiveStack = () => request('GET', '/api/stacks/active');
export const setActiveStack = (name) => request('PUT', '/api/stacks/active', { name });

// Sync continue/abort
export const continueSync = () => request('POST', '/api/sync/continue');
export const abortSync = () => request('POST', '/api/sync/abort');

// Worktree endpoints
export const listWorktrees = () => request('GET', '/api/worktrees');
export const addWorktree = (branch, createBranch = false) =>
  request('POST', '/api/worktrees', { branch, createBranch });
export const removeWorktree = (branch, force = false) =>
  request('DELETE', `/api/worktrees/${encodeURIComponent(branch)}${force ? '?force=true' : ''}`);

// Nuke endpoints
export const nukeAll = (force = false) => request('POST', '/api/nuke', { force });
export const nukeWorktrees = (force = false) => request('POST', '/api/nuke/worktrees', { force });
export const nukeStacks = (force = false) => request('POST', '/api/nuke/stacks', { force });
export const nukeBranches = () => request('POST', '/api/nuke/branches');

// Git endpoints
export const getGitStatus = () => request('GET', '/api/git/status');
export const getBranches = () => request('GET', '/api/git/branches');
