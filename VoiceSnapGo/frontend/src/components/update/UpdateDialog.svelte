<script lang="ts">
  import { Call } from '@wailsio/runtime'
  import { t } from '../../lib/i18n'
  import type { UpdateInfo } from '../../lib/stores/update'
  import { updateDownloading } from '../../lib/stores/update'

  interface Props {
    info: UpdateInfo
    onclose: () => void
  }

  let { info, onclose }: Props = $props()
  let downloading = $state(false)

  async function doUpdate() {
    downloading = true
    updateDownloading.set(true)
    try {
      await Call.ByName('voicesnap/services.UpdaterService.PerformUpdate', info.downloadUrl)
    } catch (err) {
      downloading = false
      updateDownloading.set(false)
    }
  }
</script>

<!-- svelte-ignore a11y_click_events_have_key_events -->
<div class="overlay" onclick={onclose} role="dialog">
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <div class="dialog" onclick={(e) => e.stopPropagation()} role="document">
    <h2 class="title">{t('update.title')}</h2>
    <p class="version">{t('update.version', { version: info.version })}</p>

    {#if info.releaseNotes}
      <div class="notes">
        <h3 class="notes-title">{t('update.releaseNotes')}</h3>
        <p class="notes-content">{info.releaseNotes}</p>
      </div>
    {/if}

    <div class="actions">
      <button class="btn btn-secondary" onclick={onclose} disabled={downloading}>
        {t('update.later')}
      </button>
      <button class="btn btn-primary" onclick={doUpdate} disabled={downloading}>
        {downloading ? t('update.downloading') : t('update.download')}
      </button>
    </div>
  </div>
</div>

<style>
  .overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.4);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 10000;
    backdrop-filter: blur(4px);
  }

  .dialog {
    background: var(--color-bg-primary);
    border-radius: var(--radius-xl);
    padding: var(--spacing-xxl);
    max-width: 420px;
    width: 90%;
    box-shadow: 0 8px 40px rgba(0, 0, 0, 0.15);
  }

  .title {
    font-size: var(--font-size-xl);
    font-weight: 700;
    margin-bottom: var(--spacing-xs);
  }

  .version {
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
    margin-bottom: var(--spacing-lg);
  }

  .notes {
    background: var(--color-bg-secondary);
    border-radius: var(--radius-md);
    padding: var(--spacing-md);
    margin-bottom: var(--spacing-lg);
  }

  .notes-title {
    font-size: var(--font-size-sm);
    font-weight: 600;
    margin-bottom: var(--spacing-xs);
  }

  .notes-content {
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
    line-height: 1.5;
    white-space: pre-wrap;
  }

  .actions {
    display: flex;
    gap: var(--spacing-sm);
    justify-content: flex-end;
  }

  .btn {
    padding: 8px 20px;
    border-radius: var(--radius-sm);
    font-size: var(--font-size-sm);
    font-weight: 600;
    cursor: pointer;
    border: none;
    transition: opacity var(--transition-fast);
  }

  .btn:hover { opacity: 0.85; }
  .btn:disabled { opacity: 0.5; cursor: not-allowed; }

  .btn-secondary {
    background: var(--color-bg-secondary);
    color: var(--color-label);
  }

  .btn-primary {
    background: var(--color-blue);
    color: white;
  }
</style>
