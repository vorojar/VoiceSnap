<script lang="ts">
  import { Call } from '@wailsio/runtime'
  import { t } from '../../lib/i18n'

  let version = $state('2.1.0')
  let updateStatus = $state('')
  let updateColor = $state('')
  let checking = $state(false)

  const currentYear = new Date().getFullYear().toString()

  async function loadVersion() {
    try {
      const v: any = await Call.ByName('voicesnap/services.AppService.GetVersion')
      if (v) version = v
    } catch {}
  }
  loadVersion()

  async function checkUpdate() {
    checking = true
    updateStatus = t('about.checkingUpdate')
    updateColor = ''
    try {
      const info: any = await Call.ByName('voicesnap/services.UpdaterService.CheckForUpdate')
      if (info) {
        updateStatus = `${t('update.title')}: v${info.Version}`
        updateColor = 'blue'
      } else {
        updateStatus = t('about.isLatestVersion')
        updateColor = 'green'
      }
    } catch {
      updateStatus = t('about.checkFailed')
      updateColor = 'red'
    }
    checking = false
  }
</script>

<div class="about">
  <div class="center-content">
    <div class="app-icon">V</div>
    <h2 class="app-name">VoiceSnap</h2>
    <p class="app-version">v{version}</p>

    <div class="btn-row">
      <button class="check-btn" onclick={checkUpdate} disabled={checking}>
        {t('about.checkUpdate')}
      </button>
      <button class="check-btn github-btn" onclick={() => Call.ByName('voicesnap/services.AppService.OpenURL', 'https://github.com/vorojar/VoiceSnap')}>
        <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
          <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0016 8c0-4.42-3.58-8-8-8z"/>
        </svg>
        {t('about.github')}
      </button>
    </div>

    {#if updateStatus}
      <p class="update-status" class:green={updateColor === 'green'} class:red={updateColor === 'red'} class:blue={updateColor === 'blue'}>
        {updateStatus}
      </p>
    {/if}

    <div class="privacy">
      <div class="privacy-item">
        <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
          <path d="M7 1L2.5 3.5V6.5C2.5 9.5 4.5 12 7 13C9.5 12 11.5 9.5 11.5 6.5V3.5L7 1Z" stroke="currentColor" stroke-width="1.2" stroke-linejoin="round"/>
          <path d="M5 7L6.5 8.5L9 5.5" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
        <span>{t('about.privacyOffline')}</span>
      </div>
      <div class="privacy-item">
        <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
          <rect x="3" y="6.5" width="8" height="6" rx="1.5" stroke="currentColor" stroke-width="1.2"/>
          <path d="M5 6.5V4.5C5 3.4 5.9 2.5 7 2.5C8.1 2.5 9 3.4 9 4.5V6.5" stroke="currentColor" stroke-width="1.2" stroke-linecap="round"/>
        </svg>
        <span>{t('about.privacyNoAccount')}</span>
      </div>
      <div class="privacy-item">
        <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
          <path d="M5 4L2 7L5 10" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/>
          <path d="M9 4L12 7L9 10" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
        <span>{t('about.privacyOpenSource')}</span>
      </div>
    </div>
  </div>

  <div class="bottom">
    <div class="divider"></div>
    <p class="copyright">{t('about.copyright', { year: currentYear })}</p>
  </div>
</div>

<style>
  .about {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    min-height: calc(100vh - 40px);
    background: var(--color-bg-grouped-secondary);
    border-radius: var(--radius-md);
    position: relative;
  }

  .center-content {
    display: flex;
    flex-direction: column;
    align-items: center;
    margin-top: -20px;
  }

  .app-icon {
    width: 80px;
    height: 80px;
    background: var(--color-blue);
    border-radius: 18px;
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    font-weight: 700;
    font-size: 32px;
    box-shadow: 0 4px 16px rgba(0, 122, 255, 0.2);
  }

  .app-name {
    font-size: 22px;
    font-weight: 700;
    margin-top: 16px;
  }

  .app-version {
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
    margin-top: 4px;
  }

  .btn-row {
    display: flex;
    gap: 10px;
    margin-top: 20px;
  }

  .check-btn {
    padding: 7px 24px;
    background: transparent;
    border: 1.5px solid var(--color-separator);
    border-radius: var(--radius-md);
    font-size: var(--font-size-sm);
    font-weight: 500;
    color: var(--color-label);
    cursor: pointer;
    transition: all var(--transition-fast);
  }

  .check-btn:hover {
    border-color: var(--color-blue);
    color: var(--color-blue);
  }

  .check-btn:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  .github-btn {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .update-status {
    margin-top: 10px;
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
  }

  .update-status.green { color: var(--color-green); }
  .update-status.red { color: var(--color-red); }
  .update-status.blue { color: var(--color-blue); }

  .privacy {
    display: flex;
    flex-direction: column;
    gap: 8px;
    margin-top: 32px;
  }

  .privacy-item {
    display: flex;
    align-items: center;
    gap: 8px;
    color: var(--color-tertiary-label);
    font-size: var(--font-size-xs);
  }

  .privacy-item svg {
    flex-shrink: 0;
  }

  .bottom {
    position: absolute;
    bottom: 20px;
    left: 30px;
    right: 30px;
    text-align: center;
  }

  .divider {
    height: 1px;
    background: var(--color-separator);
    margin-bottom: 12px;
  }

  .copyright {
    font-size: var(--font-size-xs);
    color: var(--color-tertiary-label);
  }
</style>
