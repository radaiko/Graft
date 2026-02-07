<script>
  import { onMount } from 'svelte';
  import { listStacks, getStack, initStack, deleteStack, pushBranch, syncStack,
           getGitStatus, getActiveStack, setActiveStack, popBranch, dropBranch, shiftBranch } from '../lib/api.js';
  import StackGraph from '../components/StackGraph.svelte';
  import CommitDialog from '../components/CommitDialog.svelte';
  import ConflictBanner from '../components/ConflictBanner.svelte';

  let { onError, onSuccess } = $props();

  let stacks = $state([]);
  let selectedName = $state(null);
  let activeStackName = $state(null);
  let stackDetail = $state(null);
  let gitStatus = $state(null);
  let loading = $state(false);
  let showCommitDialog = $state(false);

  // New stack / push inputs
  let newStackName = $state('');
  let pushBranchName = $state('');
  let createBranchOnPush = $state(false);
  let shiftBranchName = $state('');

  let pollTimer = $state(null);

  onMount(() => {
    loadStacks();
    pollTimer = setInterval(poll, 2000);
    return () => clearInterval(pollTimer);
  });

  async function loadStacks() {
    try {
      stacks = await listStacks();
      const active = await getActiveStack();
      activeStackName = active?.name;

      if (stacks.length > 0 && !selectedName) {
        selectedName = activeStackName || stacks[0];
      }
      if (selectedName) {
        await loadStack(selectedName);
      }
    } catch (e) {
      onError(e.message);
    }
  }

  async function loadStack(name) {
    try {
      stackDetail = await getStack(name);
    } catch (e) {
      onError(e.message);
    }
  }

  async function poll() {
    if (loading) return;
    const pollTarget = selectedName;
    try {
      if (pollTarget) {
        const detail = await getStack(pollTarget);
        if (selectedName === pollTarget) {
          stackDetail = detail;
        }
      }
      gitStatus = await getGitStatus();
      const active = await getActiveStack();
      activeStackName = active?.name;
    } catch {
      // Silently ignore poll errors
    }
  }

  async function handleSelect(name) {
    selectedName = name;
    await loadStack(name);
  }

  async function handleSwitchActive(name) {
    loading = true;
    try {
      await setActiveStack(name);
      activeStackName = name;
      selectedName = name;
      await loadStack(name);
      onSuccess(`Switched to stack '${name}'`);
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }

  async function handleInit() {
    if (!newStackName.trim()) return;
    loading = true;
    try {
      await initStack(newStackName.trim());
      newStackName = '';
      stacks = await listStacks();
      const active = await getActiveStack();
      activeStackName = active?.name;
      selectedName = activeStackName || stacks[stacks.length - 1];
      await loadStack(selectedName);
      onSuccess('Stack created');
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }

  async function handleDelete() {
    if (!selectedName) return;
    loading = true;
    try {
      await deleteStack(selectedName);
      selectedName = null;
      stackDetail = null;
      stacks = await listStacks();
      const active = await getActiveStack();
      activeStackName = active?.name;
      if (stacks.length > 0) {
        selectedName = activeStackName || stacks[0];
        await loadStack(selectedName);
      }
      onSuccess('Stack deleted');
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }

  async function handlePush() {
    if (!pushBranchName.trim()) return;
    loading = true;
    try {
      // Ensure this stack is active before push
      if (selectedName && selectedName !== activeStackName) {
        await setActiveStack(selectedName);
        activeStackName = selectedName;
      }
      await pushBranch(pushBranchName.trim(), createBranchOnPush);
      pushBranchName = '';
      createBranchOnPush = false;
      await loadStack(selectedName);
      onSuccess('Branch pushed');
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }

  async function handleSync() {
    if (!selectedName) return;
    loading = true;
    try {
      // Ensure this stack is active before sync
      if (selectedName !== activeStackName) {
        await setActiveStack(selectedName);
        activeStackName = selectedName;
      }
      const result = await syncStack();
      await loadStack(selectedName);
      if (result.hasConflict) {
        onError('Sync stopped: merge conflict');
      } else {
        onSuccess('Stack synced');
      }
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }

  async function handlePop() {
    loading = true;
    try {
      if (selectedName !== activeStackName) {
        await setActiveStack(selectedName);
        activeStackName = selectedName;
      }
      const result = await popBranch();
      await loadStack(selectedName);
      onSuccess(`Removed '${result.removedBranch}' from top of stack`);
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }

  async function handleDrop(branchName) {
    loading = true;
    try {
      if (selectedName !== activeStackName) {
        await setActiveStack(selectedName);
        activeStackName = selectedName;
      }
      await dropBranch(branchName);
      await loadStack(selectedName);
      onSuccess(`Dropped '${branchName}' from stack`);
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }

  async function handleShift() {
    if (!shiftBranchName.trim()) return;
    loading = true;
    try {
      if (selectedName !== activeStackName) {
        await setActiveStack(selectedName);
        activeStackName = selectedName;
      }
      await shiftBranch(shiftBranchName.trim());
      shiftBranchName = '';
      await loadStack(selectedName);
      onSuccess('Branch shifted to bottom of stack');
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }

  function handleConflictResolved() {
    loadStack(selectedName);
    onSuccess('Conflict resolved');
  }
</script>

<div class="stacks-view">
  <div class="stack-sidebar">
    <div class="section-header">
      <h2>Stacks</h2>
    </div>

    <div class="stack-list">
      {#each stacks as name}
        <button
          class="stack-item"
          class:active={selectedName === name}
          onclick={() => handleSelect(name)}
        >
          <span class="mono">{name}</span>
          {#if name === activeStackName}
            <span class="active-badge">active</span>
          {/if}
        </button>
      {/each}
      {#if stacks.length === 0}
        <p class="empty-hint">No stacks yet</p>
      {/if}
    </div>

    <div class="new-stack">
      <input
        type="text"
        bind:value={newStackName}
        placeholder="New stack name..."
        onkeydown={(e) => e.key === 'Enter' && handleInit()}
        disabled={loading}
      />
      <button class="btn btn-accent" onclick={handleInit} disabled={loading || !newStackName.trim()}>
        Init
      </button>
    </div>
  </div>

  <div class="stack-main">
    {#if stackDetail}
      <div class="main-header">
        <div class="header-info">
          <h2 class="mono">{stackDetail.name}</h2>
          {#if stackDetail.isActive}
            <span class="active-indicator">active</span>
          {:else}
            <button class="btn btn-sm" onclick={() => handleSwitchActive(stackDetail.name)} disabled={loading}>
              Switch to
            </button>
          {/if}
          {#if gitStatus}
            <span class="current-branch mono">
              on <span class="branch-highlight">{gitStatus.currentBranch}</span>
            </span>
          {/if}
        </div>
        <div class="header-actions">
          <button class="btn btn-danger" onclick={handleDelete} disabled={loading}>Delete</button>
        </div>
      </div>

      {#if stackDetail.hasConflict}
        <ConflictBanner onResolved={handleConflictResolved} {onError} />
      {/if}

      <div class="graph-area">
        <StackGraph stack={stackDetail} onDrop={handleDrop} />
      </div>

      <div class="action-bar">
        <div class="push-group">
          <input
            type="text"
            class="push-input"
            bind:value={pushBranchName}
            placeholder="Branch name to push..."
            onkeydown={(e) => e.key === 'Enter' && handlePush()}
            disabled={loading}
          />
          <label class="checkbox-label-inline">
            <input type="checkbox" bind:checked={createBranchOnPush} disabled={loading} />
            Create
          </label>
          <button class="btn" onclick={handlePush} disabled={loading || !pushBranchName.trim()}>
            Push
          </button>
        </div>

        <div class="action-group">
          <button class="btn" onclick={handlePop} disabled={loading || !stackDetail.branches.length}>
            Pop
          </button>
          <button class="btn" onclick={handleSync} disabled={loading}>
            {loading ? 'Syncing...' : 'Sync'}
          </button>
          <button class="btn btn-accent" onclick={() => showCommitDialog = true} disabled={loading}>
            Commit
          </button>
        </div>
      </div>

      <div class="shift-bar">
        <input
          type="text"
          class="push-input"
          bind:value={shiftBranchName}
          placeholder="Branch to shift to bottom..."
          onkeydown={(e) => e.key === 'Enter' && handleShift()}
          disabled={loading}
        />
        <button class="btn" onclick={handleShift} disabled={loading || !shiftBranchName.trim()}>
          Shift
        </button>
      </div>
    {:else if stacks.length === 0}
      <div class="empty-state">
        <div class="empty-icon mono">G</div>
        <h3>No stacks</h3>
        <p>Create a stack to start managing stacked branches.</p>
      </div>
    {:else}
      <div class="empty-state">
        <p>Select a stack from the sidebar</p>
      </div>
    {/if}
  </div>
</div>

{#if showCommitDialog && stackDetail}
  <CommitDialog
    branches={stackDetail.branches}
    onClose={() => showCommitDialog = false}
    {onSuccess}
    {onError}
  />
{/if}

<style>
  .stacks-view {
    display: flex;
    gap: 0;
    height: calc(100vh - 64px);
    margin: calc(-1 * var(--space-xl));
  }

  /* Stack sidebar (left panel) */
  .stack-sidebar {
    width: 220px;
    min-width: 220px;
    border-right: 1px solid var(--border);
    display: flex;
    flex-direction: column;
    background: var(--bg-surface);
  }

  .section-header {
    padding: var(--space-md) var(--space-md) var(--space-sm);
  }

  .section-header h2 {
    font-size: 11px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    color: var(--text-tertiary);
  }

  .stack-list {
    flex: 1;
    overflow-y: auto;
    padding: 0 var(--space-sm);
  }

  .stack-item {
    display: flex;
    width: 100%;
    text-align: left;
    padding: 8px var(--space-sm);
    border-radius: var(--radius-sm);
    background: transparent;
    color: var(--text-secondary);
    font-size: 13px;
    justify-content: space-between;
    align-items: center;
  }

  .stack-item:hover {
    background: var(--bg-hover);
    color: var(--text-primary);
  }

  .stack-item.active {
    background: var(--accent-glow);
    color: var(--accent);
  }

  .active-badge {
    font-size: 9px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    padding: 1px 5px;
    border-radius: 3px;
    background: color-mix(in srgb, var(--color-head) 15%, transparent);
    color: var(--color-head);
  }

  .active-indicator {
    font-size: 10px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    padding: 2px 6px;
    border-radius: 3px;
    background: color-mix(in srgb, var(--color-head) 15%, transparent);
    color: var(--color-head);
  }

  .empty-hint {
    padding: var(--space-md);
    font-size: 12px;
    color: var(--text-tertiary);
    text-align: center;
  }

  .new-stack {
    display: flex;
    gap: var(--space-xs);
    padding: var(--space-sm);
    border-top: 1px solid var(--border);
  }

  .new-stack input {
    flex: 1;
    min-width: 0;
  }

  /* Main area */
  .stack-main {
    flex: 1;
    display: flex;
    flex-direction: column;
    padding: var(--space-lg);
    overflow-y: auto;
  }

  .main-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding-bottom: var(--space-md);
    border-bottom: 1px solid var(--border);
    margin-bottom: var(--space-md);
  }

  .header-info {
    display: flex;
    align-items: baseline;
    gap: var(--space-md);
  }

  .header-info h2 {
    font-size: 18px;
    font-weight: 700;
    letter-spacing: -0.02em;
  }

  .current-branch {
    font-size: 12px;
    color: var(--text-tertiary);
  }

  .branch-highlight {
    color: var(--color-head);
  }

  .header-actions {
    display: flex;
    gap: var(--space-sm);
  }

  .graph-area {
    flex: 1;
    overflow-y: auto;
    padding: var(--space-sm) 0;
  }

  .action-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding-top: var(--space-md);
    border-top: 1px solid var(--border);
    margin-top: var(--space-md);
  }

  .push-group {
    display: flex;
    gap: var(--space-xs);
    align-items: center;
  }

  .push-input {
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

  .action-group {
    display: flex;
    gap: var(--space-sm);
  }

  .shift-bar {
    display: flex;
    gap: var(--space-xs);
    padding-top: var(--space-sm);
  }

  .btn-sm {
    font-size: 10px;
    padding: 2px 8px;
  }

  .empty-state {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: var(--space-sm);
    color: var(--text-tertiary);
  }

  .empty-icon {
    width: 48px;
    height: 48px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 24px;
    font-weight: 700;
    color: var(--accent);
    background: var(--accent-glow);
    border-radius: var(--radius-lg);
    margin-bottom: var(--space-sm);
  }

  .empty-state h3 {
    font-size: 16px;
    font-weight: 600;
    color: var(--text-secondary);
  }

  .empty-state p {
    font-size: 13px;
  }
</style>
