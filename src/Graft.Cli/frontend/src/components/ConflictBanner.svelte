<script>
  import { continueSync, abortSync } from '../lib/api.js';

  let { onResolved, onError } = $props();

  let loading = $state(false);

  async function handleContinue() {
    loading = true;
    try {
      await continueSync();
      onResolved();
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }

  async function handleAbort() {
    loading = true;
    try {
      await abortSync();
      onResolved();
    } catch (e) {
      onError(e.message);
    } finally {
      loading = false;
    }
  }
</script>

<div class="banner">
  <div class="banner-icon">!</div>
  <div class="banner-content">
    <span class="banner-title">Merge conflict</span>
    <span class="banner-text">Resolve conflicts in your editor, stage files, then continue or abort.</span>
  </div>
  <div class="banner-actions">
    <button class="btn btn-danger" onclick={handleAbort} disabled={loading}>Abort</button>
    <button class="btn btn-accent" onclick={handleContinue} disabled={loading}>Continue</button>
  </div>
</div>

<style>
  .banner {
    display: flex;
    align-items: center;
    gap: var(--space-md);
    padding: var(--space-sm) var(--space-md);
    background: color-mix(in srgb, var(--color-conflict) 8%, var(--bg-raised));
    border: 1px solid color-mix(in srgb, var(--color-conflict) 30%, transparent);
    border-radius: var(--radius-md);
    animation: fadeIn 200ms var(--ease-out);
  }

  .banner-icon {
    width: 24px;
    height: 24px;
    border-radius: 50%;
    background: color-mix(in srgb, var(--color-conflict) 20%, transparent);
    color: var(--color-conflict);
    display: flex;
    align-items: center;
    justify-content: center;
    font-weight: 700;
    font-size: 13px;
    flex-shrink: 0;
  }

  .banner-content {
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: 1px;
  }

  .banner-title {
    font-weight: 600;
    font-size: 12px;
    color: var(--color-conflict);
  }

  .banner-text {
    font-size: 11px;
    color: var(--text-secondary);
  }

  .banner-actions {
    display: flex;
    gap: var(--space-sm);
    flex-shrink: 0;
  }
</style>
