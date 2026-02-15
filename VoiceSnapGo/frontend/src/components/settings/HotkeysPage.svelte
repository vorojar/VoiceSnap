<script lang="ts">
  import { Call } from '@wailsio/runtime'
  import { t } from '../../lib/i18n'
  import { hotkeyVK } from '../../lib/stores/config'
  import { hotkeyName } from '../../lib/stores/indicator'

  let currentKeyName = $state('Ctrl')
  let isRecording = $state(false)
  let hintText = $state('')

  const unsub = hotkeyName.subscribe(k => { currentKeyName = k })

  async function startRecordingHotkey() {
    isRecording = true
    hintText = t('hotkeys.pressAnyKey')

    try {
      await Call.ByName('voicesnap/services.HotkeyService.StartRecordingHotkey')
    } catch {}

    // Listen for keydown
    function onKey(e: KeyboardEvent) {
      e.preventDefault()
      e.stopPropagation()

      const vk = mapKeyToVK(e)
      if (vk > 0) {
        isRecording = false
        hintText = t('hotkeys.updated')
        hotkeyVK.set(vk)

        // Call backend
        ;(async () => {
          try {
            await Call.ByName('voicesnap/services.HotkeyService.SetHotkey', vk)
            const name: any = await Call.ByName('voicesnap/services.HotkeyService.GetKeyName', vk)
            if (name) {
              hotkeyName.set(name)
              currentKeyName = name
            }
          } catch {}
          try {
            await Call.ByName('voicesnap/services.HotkeyService.StopRecordingHotkey')
          } catch {}
        })()

        document.removeEventListener('keydown', onKey)
      }
    }

    document.addEventListener('keydown', onKey)
  }

  function mapKeyToVK(e: KeyboardEvent): number {
    // Map browser key codes to Windows VK codes
    switch (e.key) {
      case 'Control': return e.location === 1 ? 0xA2 : e.location === 2 ? 0xA3 : 0x11
      case 'Alt': return e.location === 1 ? 0xA4 : e.location === 2 ? 0xA5 : 0x12
      case 'Shift': return e.location === 1 ? 0xA0 : e.location === 2 ? 0xA1 : 0x10
      case 'CapsLock': return 0x14
      case ' ': return 0x20
      case 'Tab': return 0x09
      case 'Enter': return 0x0D
      case 'Escape': return 0x1B
      case 'Meta': return e.location === 1 ? 0x5B : 0x5C
      default:
        if (e.key.length === 1) {
          const code = e.key.toUpperCase().charCodeAt(0)
          if (code >= 0x41 && code <= 0x5A) return code
          if (code >= 0x30 && code <= 0x39) return code
        }
        if (e.key.startsWith('F') && e.key.length <= 3) {
          const num = parseInt(e.key.substring(1))
          if (num >= 1 && num <= 12) return 0x70 + num - 1
        }
        return 0
    }
  }
</script>

<div class="page">
  <h1 class="page-title">{t('hotkeys.title')}</h1>

  <div class="section">
    <div class="hotkey-display">
      <div class="hotkey-info">
        <span class="hotkey-label">{t('hotkeys.current')}</span>
        <div class="hotkey-key" class:recording={isRecording}>
          {isRecording ? '...' : currentKeyName}
        </div>
      </div>
      <button class="change-btn" onclick={startRecordingHotkey} disabled={isRecording}>
        {t('hotkeys.change')}
      </button>
    </div>

    {#if hintText}
      <p class="hint" class:recording={isRecording}>{hintText}</p>
    {/if}
  </div>

  <div class="section hint-section">
    <p class="hint-title">{t('hotkeys.hintTitle')}</p>
    <div class="mode-item">
      <span class="mode-badge hold">●</span>
      <div>
        <span class="mode-name">{t('hotkeys.hintHold')}</span>
        <p class="mode-desc">{t('hotkeys.hintHoldDesc')}</p>
      </div>
    </div>
    <div class="mode-item">
      <span class="mode-badge free">●</span>
      <div>
        <span class="mode-name">{t('hotkeys.hintFree')}</span>
        <p class="mode-desc">{t('hotkeys.hintFreeDesc')}</p>
      </div>
    </div>
    <p class="mode-note">{t('hotkeys.hintCombo')}</p>
  </div>
</div>

<style>
  .page {
    max-width: 560px;
  }

  .page-title {
    font-size: var(--font-size-title);
    font-weight: 700;
    margin-bottom: var(--spacing-xl);
  }

  .section {
    background: var(--color-bg-grouped-secondary);
    border-radius: var(--radius-lg);
    padding: var(--spacing-lg);
    margin-bottom: var(--spacing-lg);
  }

  .hotkey-display {
    display: flex;
    align-items: center;
    justify-content: space-between;
  }

  .hotkey-info {
    display: flex;
    align-items: center;
    gap: var(--spacing-md);
  }

  .hotkey-label {
    font-size: var(--font-size-base);
    font-weight: 500;
  }

  .hotkey-key {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    min-width: 60px;
    padding: 6px 16px;
    background: var(--color-bg-secondary);
    border-radius: var(--radius-sm);
    font-family: var(--font-family);
    font-size: var(--font-size-lg);
    font-weight: 600;
    color: var(--color-blue);
  }

  .hotkey-key.recording {
    color: var(--color-orange);
    animation: pulse 1s infinite;
  }

  @keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.5; }
  }

  .change-btn {
    padding: 6px 16px;
    background: var(--color-blue);
    color: white;
    border: none;
    border-radius: var(--radius-sm);
    font-size: var(--font-size-sm);
    font-weight: 600;
    cursor: pointer;
    transition: opacity var(--transition-fast);
  }

  .change-btn:hover { opacity: 0.85; }
  .change-btn:disabled { opacity: 0.5; cursor: not-allowed; }

  .hint {
    margin-top: var(--spacing-sm);
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
  }

  .hint.recording {
    color: var(--color-blue);
  }

  .hint-section {
    background: rgba(0, 122, 255, 0.06);
  }

  .hint-title {
    font-size: var(--font-size-base);
    font-weight: 600;
    margin-bottom: var(--spacing-md);
  }

  .mode-item {
    display: flex;
    gap: var(--spacing-sm);
    margin-bottom: var(--spacing-sm);
  }

  .mode-badge {
    flex-shrink: 0;
    font-size: 10px;
    margin-top: 3px;
  }

  .mode-badge.hold { color: var(--color-red); }
  .mode-badge.free { color: var(--color-blue); }

  .mode-name {
    font-size: var(--font-size-sm);
    font-weight: 600;
  }

  .mode-desc {
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
    line-height: 1.5;
    margin-top: 2px;
  }

  .mode-note {
    font-size: var(--font-size-xs);
    color: var(--color-tertiary-label);
    margin-top: var(--spacing-sm);
  }
</style>
