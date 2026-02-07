<script>
  import BranchNode from './BranchNode.svelte';

  let { stack, onDrop } = $props();
</script>

<div class="graph">
  <!-- Trunk label -->
  <div class="trunk">
    <div class="trunk-connector">
      <div class="trunk-dot"></div>
      <div class="trunk-line"></div>
    </div>
    <div class="trunk-info">
      <span class="trunk-name mono">{stack.trunk}</span>
      <span class="trunk-badge">trunk</span>
    </div>
  </div>

  <!-- Branches bottom-to-top: index 0 is closest to trunk, render top-down -->
  {#each stack.branches as branch, i (branch.name)}
    <BranchNode {branch} isLast={i === stack.branches.length - 1} {onDrop} />
  {/each}

  {#if stack.branches.length === 0}
    <div class="empty-state">
      <p class="mono">No branches yet</p>
      <p class="hint">Use the Push input below to add a branch to this stack</p>
    </div>
  {/if}
</div>

<style>
  .graph {
    display: flex;
    flex-direction: column;
    padding: var(--space-md) 0;
  }

  .trunk {
    display: flex;
    gap: var(--space-md);
    align-items: stretch;
    min-height: 40px;
  }

  .trunk-connector {
    display: flex;
    flex-direction: column;
    align-items: center;
    width: 20px;
    flex-shrink: 0;
  }

  .trunk-dot {
    width: 10px;
    height: 10px;
    border-radius: 50%;
    background: var(--accent);
    box-shadow: 0 0 0 2px var(--accent), 0 0 10px var(--accent-glow);
    flex-shrink: 0;
  }

  .trunk-line {
    flex: 1;
    width: 2px;
    background: var(--border-strong);
    min-height: 8px;
  }

  .trunk-info {
    flex: 1;
    display: flex;
    align-items: center;
    gap: var(--space-sm);
    padding: var(--space-sm) 0;
  }

  .trunk-name {
    font-weight: 600;
    font-size: 13px;
    color: var(--accent);
  }

  .trunk-badge {
    font-size: 10px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    padding: 1px 6px;
    border-radius: 3px;
    background: var(--accent-glow);
    color: var(--accent-dim);
  }

  .empty-state {
    padding: var(--space-lg) var(--space-xl);
    text-align: center;
    color: var(--text-tertiary);
  }

  .empty-state p {
    margin-bottom: var(--space-xs);
  }

  .hint {
    font-size: 12px;
  }
</style>
