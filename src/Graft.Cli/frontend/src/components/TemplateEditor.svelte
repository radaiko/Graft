<script>
  let { files = $bindable([]), disabled = false } = $props();

  function addFile() {
    files = [...files, { src: '', dst: '', mode: 'copy' }];
  }

  function removeFile(index) {
    files = files.filter((_, i) => i !== index);
  }
</script>

<div class="editor">
  <div class="editor-header">
    <strong>Template files</strong>
    <button class="btn" onclick={addFile} {disabled}>+ Add</button>
  </div>

  {#if files.length > 0}
    <div class="file-grid">
      <div class="grid-header mono">
        <span>Source</span>
        <span>Destination</span>
        <span>Mode</span>
        <span></span>
      </div>
      {#each files as file, i}
        <div class="grid-row">
          <input type="text" bind:value={file.src} placeholder=".env.template" {disabled} />
          <input type="text" bind:value={file.dst} placeholder=".env" {disabled} />
          <select bind:value={file.mode} {disabled}>
            <option value="copy">copy</option>
            <option value="symlink">symlink</option>
          </select>
          <button class="remove-btn" onclick={() => removeFile(i)} {disabled}>✕</button>
        </div>
      {/each}
    </div>
  {:else}
    <p class="empty">No template files configured</p>
  {/if}
</div>

<style>
  .editor {
    display: flex;
    flex-direction: column;
    gap: var(--space-sm);
  }

  .editor-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .file-grid {
    display: flex;
    flex-direction: column;
    border: 1px solid var(--border);
    border-radius: var(--radius-sm);
    overflow: hidden;
  }

  .grid-header {
    display: grid;
    grid-template-columns: 1fr 1fr 100px 32px;
    gap: var(--space-sm);
    padding: var(--space-xs) var(--space-sm);
    background: var(--bg-root);
    font-size: 10px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--text-tertiary);
  }

  .grid-row {
    display: grid;
    grid-template-columns: 1fr 1fr 100px 32px;
    gap: var(--space-sm);
    padding: var(--space-xs) var(--space-sm);
    border-top: 1px solid var(--border);
    align-items: center;
  }

  .grid-row input, .grid-row select {
    width: 100%;
    padding: 4px 6px;
    font-size: 11px;
  }

  .remove-btn {
    background: transparent;
    color: var(--text-tertiary);
    font-size: 12px;
    padding: 4px;
    width: 24px;
    height: 24px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: var(--radius-sm);
  }

  .remove-btn:hover:not(:disabled) {
    color: var(--color-danger);
    background: color-mix(in srgb, var(--color-danger) 10%, transparent);
  }

  .empty {
    font-size: 12px;
    color: var(--text-tertiary);
    padding: var(--space-sm) 0;
  }
</style>
