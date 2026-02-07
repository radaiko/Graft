<script>
  import Stacks from './views/Stacks.svelte';
  import Worktrees from './views/Worktrees.svelte';

  let activeView = $state('stacks');
  let toasts = $state([]);
  let toastId = 0;

  function addToast(message, type = 'error') {
    const id = ++toastId;
    toasts = [...toasts, { id, message, type }];
    setTimeout(() => {
      toasts = toasts.filter(t => t.id !== id);
    }, 5000);
  }

  function onError(msg) {
    addToast(msg, 'error');
  }

  function onSuccess(msg) {
    addToast(msg, 'success');
  }

  const navItems = [
    { id: 'stacks', label: 'Stacks', icon: 'S' },
    { id: 'worktrees', label: 'Worktrees', icon: 'W' },
  ];
</script>

<div class="app">
  <nav class="sidebar">
    <div class="sidebar-brand">
      <span class="brand-mark">G</span>
      <span class="brand-text">Graft</span>
    </div>

    <div class="sidebar-nav">
      {#each navItems as item}
        <button
          class="nav-item"
          class:active={activeView === item.id}
          onclick={() => activeView = item.id}
        >
          <span class="nav-icon mono">{item.icon}</span>
          <span class="nav-label">{item.label}</span>
        </button>
      {/each}
    </div>

    <div class="sidebar-footer">
      <span class="version mono">v0.1.0</span>
    </div>
  </nav>

  <main class="content">
    {#if activeView === 'stacks'}
      <Stacks {onError} {onSuccess} />
    {:else if activeView === 'worktrees'}
      <Worktrees {onError} {onSuccess} />
    {/if}
  </main>

  {#if toasts.length > 0}
    <div class="toast-container">
      {#each toasts as toast (toast.id)}
        <div class="toast toast-{toast.type}">
          <span class="toast-icon">{toast.type === 'error' ? '✕' : '✓'}</span>
          <span class="toast-message">{toast.message}</span>
        </div>
      {/each}
    </div>
  {/if}
</div>

<style>
  .app {
    display: flex;
    height: 100%;
    overflow: hidden;
  }

  /* Sidebar */
  .sidebar {
    width: 180px;
    min-width: 180px;
    background: var(--bg-surface);
    border-right: 1px solid var(--border);
    display: flex;
    flex-direction: column;
    padding: var(--space-md) 0;
  }

  .sidebar-brand {
    display: flex;
    align-items: center;
    gap: var(--space-sm);
    padding: 0 var(--space-md) var(--space-lg);
    border-bottom: 1px solid var(--border);
    margin-bottom: var(--space-md);
  }

  .brand-mark {
    font-family: var(--font-mono);
    font-weight: 700;
    font-size: 18px;
    color: var(--accent);
    width: 28px;
    height: 28px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: var(--accent-glow);
    border-radius: var(--radius-sm);
  }

  .brand-text {
    font-weight: 700;
    font-size: 15px;
    color: var(--text-primary);
    letter-spacing: -0.02em;
  }

  .sidebar-nav {
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: 2px;
    padding: 0 var(--space-sm);
  }

  .nav-item {
    display: flex;
    align-items: center;
    gap: var(--space-sm);
    padding: 8px var(--space-sm);
    border-radius: var(--radius-sm);
    background: transparent;
    color: var(--text-secondary);
    text-align: left;
    width: 100%;
  }

  .nav-item:hover {
    background: var(--bg-hover);
    color: var(--text-primary);
  }

  .nav-item.active {
    background: var(--accent-glow);
    color: var(--accent);
  }

  .nav-icon {
    width: 20px;
    text-align: center;
    font-size: 12px;
    font-weight: 600;
  }

  .nav-label {
    font-size: 13px;
    font-weight: 500;
  }

  .sidebar-footer {
    padding: var(--space-md);
    border-top: 1px solid var(--border);
    margin-top: var(--space-md);
  }

  .version {
    font-size: 11px;
    color: var(--text-tertiary);
  }

  /* Content */
  .content {
    flex: 1;
    overflow-y: auto;
    padding: var(--space-xl);
    animation: fadeIn 200ms var(--ease-out);
  }

  /* Toasts */
  .toast-container {
    position: fixed;
    top: var(--space-md);
    right: var(--space-md);
    display: flex;
    flex-direction: column;
    gap: var(--space-sm);
    z-index: 1000;
    pointer-events: none;
  }

  .toast {
    display: flex;
    align-items: center;
    gap: var(--space-sm);
    padding: 8px 14px;
    border-radius: var(--radius-md);
    background: var(--bg-raised);
    border: 1px solid var(--border);
    font-size: 12px;
    animation: slideIn 200ms var(--ease-out);
    pointer-events: auto;
  }

  .toast-error {
    border-color: color-mix(in srgb, var(--color-danger) 40%, transparent);
  }

  .toast-error .toast-icon {
    color: var(--color-danger);
  }

  .toast-success {
    border-color: color-mix(in srgb, var(--color-success) 40%, transparent);
  }

  .toast-success .toast-icon {
    color: var(--color-success);
  }

  .toast-icon {
    font-weight: 700;
    font-size: 11px;
  }

  .toast-message {
    color: var(--text-primary);
  }
</style>
