<script>
  import { onMount } from 'svelte';
  import { commitToStack, getGitStatus } from '../lib/api.js';

  let { branches = [], onClose, onSuccess, onError } = $props();

  let message = $state('');
  let selectedBranch = $state('');
  let amend = $state(false);
  let loading = $state(false);
  let gitStatus = $state(null);

  onMount(() => {
    loadStatus();
  });

  async function loadStatus() {
    try {
      gitStatus = await getGitStatus();
    } catch (e) {
      onError(e.message);
    }
  }

  async function handleCommit() {
    if (!amend && !message.trim()) return;
    loading = true;
    try {
      const branch = selectedBranch || undefined;
      const result = await commitToStack(amend ? '' : message, branch, amend);
      if (result?.branchesAreStale) {
        onSuccess('Commit created (branches above are now stale — run sync)');
      } else {
        onSuccess('Commit created');
      }
      onClose();
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }
</script>

<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
<div class="overlay" onclick={onClose} onkeydown={(e) => e.key === 'Escape' && onClose()}>
  <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
  <div class="dialog" role="dialog" aria-modal="true" tabindex="-1" onclick={(e) => e.stopPropagation()} onkeydown={(e) => e.stopPropagation()}>
    <div class="dialog-header">
      <h3>Commit</h3>
      <button class="close-btn" onclick={onClose}>✕</button>
    </div>

    <div class="dialog-body">
      {#if gitStatus}
        <div class="staged-files">
          <!-- svelte-ignore a11y_label_has_associated_control -->
          <label>Staged files</label>
          {#if gitStatus.stagedFiles.length > 0}
            <div class="file-list">
              {#each gitStatus.stagedFiles as file}
                <div class="file-item mono">{file}</div>
              {/each}
            </div>
          {:else}
            <p class="no-files">No staged files. Stage changes with <code>git add</code> first.</p>
          {/if}
        </div>
      {/if}

      <div class="field">
        <label class="checkbox-label-inline">
          <input type="checkbox" bind:checked={amend} disabled={loading} />
          Amend previous commit
        </label>
      </div>

      {#if !amend}
        <div class="field">
          <label for="commit-msg">Message</label>
          <textarea
            id="commit-msg"
            bind:value={message}
            placeholder="Describe your changes..."
            rows="3"
            disabled={loading}
          ></textarea>
        </div>
      {/if}

      <div class="field">
        <label for="branch-select">Target branch</label>
        <select id="branch-select" bind:value={selectedBranch} disabled={loading}>
          <option value="">Top of stack (default)</option>
          {#each branches as b}
            <option value={b.name}>{b.name}</option>
          {/each}
        </select>
      </div>
    </div>

    <div class="dialog-footer">
      <button class="btn" onclick={onClose} disabled={loading}>Cancel</button>
      <button
        class="btn btn-accent"
        onclick={handleCommit}
        disabled={loading || (!amend && !message.trim())}
      >
        {loading ? 'Committing...' : 'Commit'}
      </button>
    </div>
  </div>
</div>

<style>
  .overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.6);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 100;
    animation: fadeIn 120ms var(--ease-out);
  }

  .dialog {
    background: var(--bg-surface);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    width: 480px;
    max-height: 80vh;
    display: flex;
    flex-direction: column;
    box-shadow: 0 20px 60px rgba(0, 0, 0, 0.4);
  }

  .dialog-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: var(--space-md) var(--space-lg);
    border-bottom: 1px solid var(--border);
  }

  .dialog-header h3 {
    font-size: 14px;
    font-weight: 600;
  }

  .close-btn {
    background: transparent;
    color: var(--text-tertiary);
    font-size: 14px;
    padding: 4px;
  }

  .close-btn:hover {
    color: var(--text-primary);
  }

  .dialog-body {
    padding: var(--space-lg);
    display: flex;
    flex-direction: column;
    gap: var(--space-md);
    overflow-y: auto;
  }

  .staged-files {
    display: flex;
    flex-direction: column;
    gap: var(--space-xs);
  }

  .file-list {
    background: var(--bg-root);
    border: 1px solid var(--border);
    border-radius: var(--radius-sm);
    max-height: 120px;
    overflow-y: auto;
  }

  .file-item {
    padding: 3px var(--space-sm);
    font-size: 11px;
    color: var(--text-secondary);
    border-bottom: 1px solid var(--border);
  }

  .file-item:last-child {
    border-bottom: none;
  }

  .no-files {
    font-size: 12px;
    color: var(--text-tertiary);
  }

  .no-files code {
    color: var(--accent-dim);
  }

  .checkbox-label-inline {
    display: flex;
    align-items: center;
    gap: 4px;
    font-size: 12px;
    color: var(--text-secondary);
    cursor: pointer;
  }

  .field {
    display: flex;
    flex-direction: column;
    gap: var(--space-xs);
  }

  .field textarea {
    resize: vertical;
    min-height: 60px;
  }

  .dialog-footer {
    display: flex;
    justify-content: flex-end;
    gap: var(--space-sm);
    padding: var(--space-md) var(--space-lg);
    border-top: 1px solid var(--border);
  }
</style>
