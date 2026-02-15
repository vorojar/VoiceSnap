<script lang="ts">
  import { onMount } from 'svelte'
  import { Events, Call } from '@wailsio/runtime'
  import { t } from '../../lib/i18n'
  import { engineStatus } from '../../lib/stores/app'

  let statusText = $state(t('onboarding.syncModel'))
  let detailText = $state('')
  let progress = $state(0)
  let failed = $state(false)

  onMount(() => {
    Events.On('model:download-progress', (ev: any) => {
      const data = ev?.data
      if (data?.percent != null) {
        progress = data.percent
        const downloadedMB = Math.floor((data.downloaded || 0) / 1024 / 1024)
        const totalMB = Math.floor((data.total || 0) / 1024 / 1024)
        detailText = `${data.percent.toFixed(1)}% (${downloadedMB}MB / ${totalMB}MB)`
      }
    })

    Events.On('engine:status', (ev: any) => {
      const data = ev?.data
      if (data?.status === 'ready') {
        statusText = t('onboarding.complete')
        progress = 100
      } else if (data?.status === 'error') {
        statusText = t('onboarding.failed')
        detailText = data?.error || ''
        failed = true
      }
    })

    triggerDownload()
  })

  async function triggerDownload() {
    try {
      const config: any = await Call.ByName('voicesnap/services.ConfigService.GetConfig')
      if (config) {
        await Call.ByName('voicesnap/services.EngineService.DownloadModel',
          config.ModelDownloadUrl,
          config.FallbackModelDownloadUrl
        )
        statusText = t('onboarding.optimizing')
        detailText = t('onboarding.extracting')
      }
    } catch (err: any) {
      statusText = t('onboarding.failed')
      detailText = err?.message || String(err)
      failed = true
    }
  }
</script>

<div class="onboarding">
  <div class="card">
    <div class="icon">V</div>
    <h1 class="title">{t('onboarding.title')}</h1>

    <div class="status">
      <p class="status-text" class:failed>{statusText}</p>
      {#if detailText}
        <p class="detail-text">{detailText}</p>
      {/if}
    </div>

    <!-- Progress bar -->
    <div class="progress-track">
      <div class="progress-fill" style="width: {progress}%" class:failed></div>
    </div>
  </div>
</div>

<style>
  .onboarding {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100vh;
    background: var(--color-bg-secondary);
    padding: var(--spacing-xxl);
  }

  .card {
    background: var(--color-bg-primary);
    border-radius: var(--radius-xl);
    padding: var(--spacing-xxl) 48px;
    text-align: center;
    max-width: 400px;
    width: 100%;
    box-shadow: 0 2px 20px rgba(0, 0, 0, 0.06);
  }

  .icon {
    width: 56px;
    height: 56px;
    background: var(--color-blue);
    border-radius: var(--radius-lg);
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    font-weight: 700;
    font-size: 24px;
    margin: 0 auto var(--spacing-lg);
  }

  .title {
    font-size: var(--font-size-xl);
    font-weight: 700;
    margin-bottom: var(--spacing-xl);
  }

  .status {
    margin-bottom: var(--spacing-lg);
  }

  .status-text {
    font-size: var(--font-size-base);
    font-weight: 500;
    color: var(--color-label);
  }

  .status-text.failed {
    color: var(--color-red);
  }

  .detail-text {
    margin-top: var(--spacing-xs);
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
  }

  .progress-track {
    height: 6px;
    background: var(--color-bg-secondary);
    border-radius: 3px;
    overflow: hidden;
  }

  .progress-fill {
    height: 100%;
    background: var(--color-blue);
    border-radius: 3px;
    transition: width 200ms ease;
  }

  .progress-fill.failed {
    background: var(--color-red);
  }
</style>
