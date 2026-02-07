<script>
  import { onMount } from 'svelte';
  import { getConfig, putConfig, getWorktreeConfig, putWorktreeConfig } from '../lib/api.js';
  import TemplateEditor from '../components/TemplateEditor.svelte';

  let { onError, onSuccess } = $props();

  let trunk = $state('main');
  let prStrategy = $state('chain');
  let layoutPattern = $state('../{name}');
  let templateFiles = $state([]);
  let saving = $state({ defaults: false, layout: false, templates: false });

  onMount(() => {
    load();
  });

  async function load() {
    try {
      const [config, wtConfig] = await Promise.all([getConfig(), getWorktreeConfig()]);
      trunk = config.defaults?.trunk ?? 'main';
      prStrategy = config.defaults?.stackPrStrategy ?? 'chain';
      layoutPattern = wtConfig.layout?.pattern ?? '../{name}';
      templateFiles = (wtConfig.templates?.files ?? []).map(f => ({ ...f }));
    } catch (e) {
      onError(e.message);
    }
  }

  async function saveDefaults() {
    saving.defaults = true;
    try {
      await putConfig({ defaults: { trunk, stackPrStrategy: prStrategy } });
      onSuccess('Defaults saved');
    } catch (e) {
      onError(e.message);
    } finally {
      saving.defaults = false;
    }
  }

  async function saveLayout() {
    if (!layoutPattern.includes('{name}')) {
      onError('Layout pattern must include {name}');
      return;
    }
    saving.layout = true;
    try {
      const wtConfig = await getWorktreeConfig();
      wtConfig.layout = { pattern: layoutPattern };
      await putWorktreeConfig(wtConfig);
      onSuccess('Layout saved');
    } catch (e) {
      onError(e.message);
    } finally {
      saving.layout = false;
    }
  }

  async function saveTemplates() {
    saving.templates = true;
    try {
      const wtConfig = await getWorktreeConfig();
      wtConfig.templates = { files: templateFiles };
      await putWorktreeConfig(wtConfig);
      onSuccess('Templates saved');
    } catch (e) {
      onError(e.message);
    } finally {
      saving.templates = false;
    }
  }
</script>

<div class="settings-view">
  <h2>Settings</h2>

  <section class="settings-section">
    <div class="section-header">
      <h3>Defaults</h3>
      <button class="btn btn-accent" onclick={saveDefaults} disabled={saving.defaults}>
        {saving.defaults ? 'Saving...' : 'Save'}
      </button>
    </div>

    <div class="form-grid">
      <div class="field">
        <label for="trunk-input">Trunk branch</label>
        <input id="trunk-input" type="text" bind:value={trunk} disabled={saving.defaults} />
      </div>
      <div class="field">
        <label for="strategy-input">PR strategy</label>
        <input id="strategy-input" type="text" bind:value={prStrategy} disabled={saving.defaults} />
      </div>
    </div>
  </section>

  <section class="settings-section">
    <div class="section-header">
      <h3>Worktree layout</h3>
      <button class="btn btn-accent" onclick={saveLayout} disabled={saving.layout}>
        {saving.layout ? 'Saving...' : 'Save'}
      </button>
    </div>

    <div class="form-grid">
      <div class="field">
        <label for="pattern-input">Path pattern</label>
        <input
          id="pattern-input"
          type="text"
          bind:value={layoutPattern}
          placeholder="../{'{name}'}"
          disabled={saving.layout}
        />
        <span class="field-hint">
          Use <code>{'{name}'}</code> for the branch name. Relative to repo root.
        </span>
      </div>
    </div>
  </section>

  <section class="settings-section">
    <div class="section-header">
      <h3>Templates</h3>
      <button class="btn btn-accent" onclick={saveTemplates} disabled={saving.templates}>
        {saving.templates ? 'Saving...' : 'Save'}
      </button>
    </div>

    <TemplateEditor bind:files={templateFiles} disabled={saving.templates} />
  </section>
</div>

<style>
  .settings-view {
    display: flex;
    flex-direction: column;
    gap: var(--space-xl);
    max-width: 600px;
  }

  .settings-view > h2 {
    font-size: 18px;
    font-weight: 700;
    letter-spacing: -0.02em;
  }

  .settings-section {
    display: flex;
    flex-direction: column;
    gap: var(--space-md);
    padding: var(--space-lg);
    background: var(--bg-surface);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
  }

  .section-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .section-header h3 {
    font-size: 14px;
    font-weight: 600;
  }

  .form-grid {
    display: flex;
    flex-direction: column;
    gap: var(--space-md);
  }

  .field {
    display: flex;
    flex-direction: column;
    gap: var(--space-xs);
  }

  .field input {
    max-width: 300px;
  }

  .field-hint {
    font-size: 11px;
    color: var(--text-tertiary);
  }

  .field-hint code {
    color: var(--accent-dim);
  }
</style>
