<script>
  let { branch, isLast = false, onDrop } = $props();

  let confirmDrop = $state(false);

  function handleDrop() {
    if (!confirmDrop) {
      confirmDrop = true;
      setTimeout(() => { confirmDrop = false; }, 3000);
      return;
    }
    confirmDrop = false;
    onDrop?.(branch.name);
  }
</script>

<div class="node" class:is-head={branch.isHead} class:needs-rebase={branch.needsMerge}>
  <div class="connector">
    <div class="line line-top"></div>
    <div class="dot"></div>
    {#if !isLast}
      <div class="line line-bottom"></div>
    {/if}
  </div>

  <div class="info">
    <div class="branch-row">
      <span class="branch-name mono">{branch.name}</span>
      {#if branch.isHead}
        <span class="badge badge-head">HEAD</span>
      {/if}
      {#if branch.needsMerge}
        <span class="badge badge-rebase">stale</span>
      {/if}
      {#if onDrop}
        <button class="drop-btn" onclick={handleDrop}>
          {confirmDrop ? 'Confirm?' : 'Drop'}
        </button>
      {/if}
    </div>
    <div class="meta">
      <span class="commit-count mono">
        {branch.commitCount} {branch.commitCount === 1 ? 'commit' : 'commits'}
      </span>
      {#if branch.pr}
        <a
          class="pr-link mono"
          href={branch.pr.url}
          target="_blank"
          rel="noopener"
          onclick={(e) => e.stopPropagation()}
        >
          #{branch.pr.number}
        </a>
      {/if}
    </div>
  </div>
</div>

<style>
  .node {
    display: flex;
    gap: var(--space-md);
    align-items: stretch;
    min-height: 52px;
  }

  .connector {
    display: flex;
    flex-direction: column;
    align-items: center;
    width: 20px;
    flex-shrink: 0;
  }

  .line {
    flex: 1;
    width: 2px;
    background: var(--border-strong);
  }

  .line-top {
    min-height: 8px;
  }

  .line-bottom {
    min-height: 8px;
  }

  .dot {
    width: 10px;
    height: 10px;
    border-radius: 50%;
    background: var(--text-tertiary);
    border: 2px solid var(--bg-surface);
    box-shadow: 0 0 0 2px var(--text-tertiary);
    flex-shrink: 0;
  }

  .is-head .dot {
    background: var(--color-head);
    box-shadow: 0 0 0 2px var(--color-head), 0 0 8px color-mix(in srgb, var(--color-head) 40%, transparent);
  }

  .needs-rebase .dot {
    background: var(--color-stale);
    box-shadow: 0 0 0 2px var(--color-stale);
  }

  .info {
    flex: 1;
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: 2px;
    padding: var(--space-sm) 0;
  }

  .branch-row {
    display: flex;
    align-items: center;
    gap: var(--space-sm);
  }

  .branch-name {
    font-weight: 600;
    font-size: 13px;
    color: var(--text-primary);
  }

  .is-head .branch-name {
    color: var(--color-head);
  }

  .badge {
    font-size: 10px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    padding: 1px 6px;
    border-radius: 3px;
  }

  .badge-head {
    background: color-mix(in srgb, var(--color-head) 15%, transparent);
    color: var(--color-head);
  }

  .badge-rebase {
    background: color-mix(in srgb, var(--color-stale) 15%, transparent);
    color: var(--color-stale);
  }

  .drop-btn {
    font-size: 10px;
    padding: 1px 6px;
    border-radius: 3px;
    background: transparent;
    color: var(--text-tertiary);
    border: 1px solid var(--border);
    cursor: pointer;
    opacity: 0;
    transition: opacity 150ms;
  }

  .node:hover .drop-btn {
    opacity: 1;
  }

  .drop-btn:hover {
    color: var(--color-stale);
    border-color: var(--color-stale);
  }

  .meta {
    display: flex;
    align-items: center;
    gap: var(--space-md);
  }

  .commit-count {
    font-size: 11px;
    color: var(--text-secondary);
  }

  .pr-link {
    font-size: 11px;
    color: var(--accent-dim);
    text-decoration: none;
  }

  .pr-link:hover {
    color: var(--accent);
    text-decoration: underline;
  }
</style>
