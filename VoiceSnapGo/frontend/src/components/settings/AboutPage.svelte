<script lang="ts">
  import { Call } from '@wailsio/runtime'
  import { t } from '../../lib/i18n'

  let version = $state('2.0.0')
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

    <button class="check-btn" onclick={checkUpdate} disabled={checking}>
      {t('about.checkUpdate')}
    </button>

    {#if updateStatus}
      <p class="update-status" class:green={updateColor === 'green'} class:red={updateColor === 'red'} class:blue={updateColor === 'blue'}>
        {updateStatus}
      </p>
    {/if}
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

  .check-btn {
    margin-top: 20px;
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

  .update-status {
    margin-top: 10px;
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
  }

  .update-status.green { color: var(--color-green); }
  .update-status.red { color: var(--color-red); }
  .update-status.blue { color: var(--color-blue); }

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
