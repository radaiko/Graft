<script>
  import { onMount } from 'svelte';
  import { listWorktrees, addWorktree, removeWorktree, getBranches } from '../lib/api.js';

  let { onError, onSuccess } = $props();

  let worktrees = $state([]);
  let branches = $state([]);
  let selectedBranch = $state('');
  let createBranch = $state(false);
  let loading = $state(false);
  let confirmRemove = $state(null);

  onMount(() => {
    load();
  });

  async function load() {
    try {
      [worktrees, branches] = await Promise.all([listWorktrees(), getBranches()]);
    } catch (e) {
      onError(e.message);
    }
  }

  async function handleAdd() {
    if (!selectedBranch && !createBranch) return;
    const branch = selectedBranch || '';
    if (!branch) return;
    loading = true;
    try {
      worktrees = await addWorktree(branch, createBranch);
      selectedBranch = '';
      createBranch = false;
      onSuccess('Worktree added');
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }

  let forceRemove = $state(null);

  async function handleRemove(branch, force = false) {
    if (!force && forceRemove === branch) {
      // Second click on "Force?" — do force removal
      force = true;
    } else if (!force && confirmRemove !== branch) {
      confirmRemove = branch;
      forceRemove = null;
      return;
    }
    loading = true;
    try {
      worktrees = await removeWorktree(branch, force);
      confirmRemove = null;
      forceRemove = null;
      onSuccess('Worktree removed');
    } catch (e) {
      if (e.message.includes('uncommitted changes') && !force) {
        // Show inline "Force?" button instead of browser confirm()
        forceRemove = branch;
        confirmRemove = branch;
      } else {
        onError(e.message);
        confirmRemove = null;
        forceRemove = null;
      }
    } finally {
      loading = false;
    }
  }
</script>

<div class="worktrees-view">
  <div class="view-header">
    <h2>Worktrees</h2>
    <div class="add-group">
      {#if createBranch}
        <input
          type="text"
          bind:value={selectedBranch}
          placeholder="New branch name..."
          disabled={loading}
        />
      {:else}
        <select bind:value={selectedBranch} disabled={loading}>
          <option value="">Select branch...</option>
          {#each branches as b}
            <option value={b}>{b}</option>
          {/each}
        </select>
      {/if}
      <label class="checkbox-label-inline">
        <input type="checkbox" bind:checked={createBranch} disabled={loading} />
        Create
      </label>
      <button class="btn btn-accent" onclick={handleAdd} disabled={loading || !selectedBranch}>
        Add worktree
      </button>
    </div>
  </div>

  {#if worktrees.length > 0}
    <div class="wt-table">
      <div class="wt-header mono">
        <span>Branch</span>
        <span>Path</span>
        <span>HEAD</span>
        <span></span>
      </div>
      {#each worktrees as wt}
        <div class="wt-row">
          <span class="wt-branch mono">{wt.branch ?? '(detached)'}</span>
          <span class="wt-path mono">{wt.path}</span>
          <span class="wt-sha mono">{wt.headSha ? wt.headSha.slice(0, 8) : '—'}</span>
          <div class="wt-actions">
            {#if wt.branch && !wt.isBare}
              <button
                class="btn btn-danger"
                onclick={() => handleRemove(wt.branch)}
                disabled={loading}
              >
                {forceRemove === wt.branch ? 'Force?' : confirmRemove === wt.branch ? 'Confirm?' : 'Remove'}
              </button>
            {/if}
          </div>
        </div>
      {/each}
    </div>
  {:else}
    <div class="empty-state">
      <p>No worktrees configured</p>
      <p class="hint">Select a branch and click "Add worktree" to create one</p>
    </div>
  {/if}
</div>

<style>
  .worktrees-view {
    display: flex;
    flex-direction: column;
    gap: var(--space-lg);
  }

  .view-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .view-header h2 {
    font-size: 18px;
    font-weight: 700;
    letter-spacing: -0.02em;
  }

  .add-group {
    display: flex;
    gap: var(--space-sm);
    align-items: center;
  }

  .add-group select,
  .add-group input[type="text"] {
    width: 200px;
  }

  .checkbox-label-inline {
    display: flex;
    align-items: center;
    gap: 3px;
    font-size: 11px;
    color: var(--text-secondary);
    cursor: pointer;
    white-space: nowrap;
  }

  .wt-table {
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    overflow: hidden;
  }

  .wt-header {
    display: grid;
    grid-template-columns: 200px 1fr 100px 100px;
    gap: var(--space-md);
    padding: var(--space-sm) var(--space-md);
    background: var(--bg-root);
    font-size: 10px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--text-tertiary);
  }

  .wt-row {
    display: grid;
    grid-template-columns: 200px 1fr 100px 100px;
    gap: var(--space-md);
    padding: var(--space-sm) var(--space-md);
    border-top: 1px solid var(--border);
    align-items: center;
    animation: fadeIn 200ms var(--ease-out);
  }

  .wt-row:hover {
    background: var(--bg-hover);
  }

  .wt-branch {
    font-weight: 600;
    font-size: 13px;
    color: var(--text-primary);
  }

  .wt-path {
    font-size: 12px;
    color: var(--text-secondary);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .wt-sha {
    font-size: 12px;
    color: var(--text-tertiary);
  }

  .wt-actions {
    display: flex;
    justify-content: flex-end;
  }

  .empty-state {
    text-align: center;
    padding: var(--space-2xl);
    color: var(--text-tertiary);
  }

  .empty-state p {
    margin-bottom: var(--space-xs);
  }

  .hint {
    font-size: 12px;
  }
</style>
