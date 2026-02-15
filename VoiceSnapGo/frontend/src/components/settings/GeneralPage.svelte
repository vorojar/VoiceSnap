<script lang="ts">
  import { Call } from '@wailsio/runtime'
  import ToggleSwitch from '../shared/ToggleSwitch.svelte'
  import { t } from '../../lib/i18n'
  import { autoHide, soundFeedback, startAtLogin, hotkeyVK } from '../../lib/stores/config'
  import { deviceName, engineStatus, engineHardwareInfo } from '../../lib/stores/app'
  import { hotkeyName } from '../../lib/stores/indicator'

  interface InputDevice {
    Name: string
    IsDefault: boolean
  }

  let autoHideVal = $state(true)
  let soundFeedbackVal = $state(true)
  let startAtLoginVal = $state(false)
  let device = $state('Default')
  let status = $state('loading')
  let hwInfo = $state('')
  let currentKeyName = $state('Ctrl')
  let isRecording = $state(false)
  let hintText = $state('')
  let devices = $state<InputDevice[]>([])
  let showDeviceDropdown = $state(false)

  const unsub1 = autoHide.subscribe(v => { autoHideVal = v })
  const unsub2 = startAtLogin.subscribe(v => { startAtLoginVal = v })
  const unsub3 = deviceName.subscribe(v => { device = v })
  const unsub4 = engineStatus.subscribe(v => { status = v })
  const unsub5 = engineHardwareInfo.subscribe(v => { hwInfo = v })
  const unsub6 = hotkeyName.subscribe(k => { currentKeyName = k })
  const unsub7 = soundFeedback.subscribe(v => { soundFeedbackVal = v })

  async function onAutoHideChange(checked: boolean) {
    autoHide.set(checked)
    try {
      await Call.ByName('voicesnap/services.ConfigService.SetAutoHide', checked)
    } catch {}
  }

  async function onSoundFeedbackChange(checked: boolean) {
    soundFeedback.set(checked)
    try {
      await Call.ByName('voicesnap/services.ConfigService.SetSoundFeedback', checked)
    } catch {}
  }

  async function onStartAtLoginChange(checked: boolean) {
    startAtLogin.set(checked)
    try {
      await Call.ByName('voicesnap/services.ConfigService.SetStartupEnabled', checked)
    } catch {}
  }

  async function toggleDeviceDropdown() {
    if (showDeviceDropdown) {
      showDeviceDropdown = false
      return
    }
    try {
      const list: any = await Call.ByName('voicesnap/services.AudioService.ListInputDevices')
      devices = list || []
    } catch {
      devices = []
    }
    showDeviceDropdown = true
  }

  async function selectDevice(dev: InputDevice) {
    showDeviceDropdown = false
    device = dev.Name
    deviceName.set(dev.Name)
    try {
      await Call.ByName('voicesnap/services.AudioService.SetDevice', dev.Name)
    } catch {}
  }

  function closeDropdown(e: MouseEvent) {
    const target = e.target as HTMLElement
    if (!target.closest('.device-selector')) {
      showDeviceDropdown = false
    }
  }

  async function startRecordingHotkey() {
    isRecording = true
    hintText = t('hotkeys.pressAnyKey')

    try {
      await Call.ByName('voicesnap/services.HotkeyService.StartRecordingHotkey')
    } catch {}

    function onKey(e: KeyboardEvent) {
      e.preventDefault()
      e.stopPropagation()

      const vk = mapKeyToVK(e)
      if (vk > 0) {
        isRecording = false
        hintText = t('hotkeys.updated')
        hotkeyVK.set(vk)

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
  <!-- Header -->
  <div class="header">
    <h1 class="tagline">{t('home.tagline')}</h1>
    <p class="app-desc">{t('home.modeHint')}</p>
  </div>

  <!-- Hotkey -->
  <div class="section">
    <div class="hotkey-display">
      <div class="hotkey-info">
        <span class="hotkey-label">{t('home.hotkeyLabel')}</span>
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

  <!-- Engine + Device -->
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="section" onclick={closeDropdown}>
    <div class="setting-row">
      <div class="setting-info">
        <span class="setting-label">{t('about.engineMode')}</span>
        <span class="setting-value">
          <span class="status-dot" class:green={status === 'ready'} class:orange={status === 'loading'} class:red={status === 'error'}></span>
          {#if status === 'ready'}
            {hwInfo}
          {:else if status === 'loading'}
            {t('status.loading')}
          {:else}
            {t('status.engineNotReady')}
          {/if}
        </span>
      </div>
    </div>

    <div class="divider"></div>

    <div class="setting-row">
      <div class="setting-info">
        <span class="setting-label">{t('settings.inputDevice')}</span>
      </div>
      <div class="device-selector">
        <button class="device-btn" onclick={toggleDeviceDropdown}>
          <span class="device-name">{device}</span>
          <svg class="chevron" class:open={showDeviceDropdown} width="10" height="6" viewBox="0 0 10 6" fill="none">
            <path d="M1 1L5 5L9 1" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
        </button>
        {#if showDeviceDropdown && devices.length > 0}
          <div class="device-dropdown">
            {#each devices as dev}
              <button
                class="device-option"
                class:selected={dev.Name === device}
                onclick={() => selectDevice(dev)}
              >
                {dev.Name}
                {#if dev.Name === device}
                  <svg width="12" height="9" viewBox="0 0 12 9" fill="none">
                    <path d="M1 4L4.5 7.5L11 1" stroke="var(--color-blue)" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
                  </svg>
                {/if}
              </button>
            {/each}
          </div>
        {/if}
      </div>
    </div>
  </div>

  <!-- Toggles -->
  <div class="section">
    <div class="setting-row">
      <div class="setting-info">
        <span class="setting-label">{t('settings.soundFeedback')}</span>
        <span class="setting-desc">{t('settings.soundFeedbackDesc')}</span>
      </div>
      <ToggleSwitch checked={soundFeedbackVal} onchange={onSoundFeedbackChange} />
    </div>

    <div class="divider"></div>

    <div class="setting-row">
      <div class="setting-info">
        <span class="setting-label">{t('settings.autoHide')}</span>
        <span class="setting-desc">{t('settings.autoHideDesc')}</span>
      </div>
      <ToggleSwitch checked={autoHideVal} onchange={onAutoHideChange} />
    </div>

    <div class="divider"></div>

    <div class="setting-row">
      <div class="setting-info">
        <span class="setting-label">{t('settings.startAtLogin')}</span>
        <span class="setting-desc">{t('settings.startAtLoginDesc')}</span>
      </div>
      <ToggleSwitch checked={startAtLoginVal} onchange={onStartAtLoginChange} />
    </div>
  </div>
</div>

<style>
  .header {
    margin-bottom: var(--spacing-xl);
  }

  .tagline {
    font-size: 22px;
    font-weight: 700;
  }

  .app-desc {
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
    margin-top: 6px;
  }

  .section {
    background: var(--color-bg-grouped-secondary);
    border-radius: var(--radius-md);
    padding: var(--spacing-lg);
    margin-bottom: var(--spacing-md);
  }

  /* Hotkey */
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
    min-width: 56px;
    padding: 5px 14px;
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
    padding: 6px 2px;
    background: transparent;
    border: none;
    font-size: var(--font-size-sm);
    font-weight: 500;
    color: var(--color-blue);
    cursor: pointer;
    transition: opacity var(--transition-fast);
  }

  .change-btn:hover { opacity: 0.65; }
  .change-btn:disabled { opacity: 0.4; cursor: not-allowed; }

  .hint {
    margin-top: var(--spacing-sm);
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
  }

  .hint.recording {
    color: var(--color-blue);
  }

  /* Settings rows */
  .status-dot {
    display: inline-block;
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: var(--color-blue);
    margin-right: 4px;
    vertical-align: middle;
  }

  .status-dot.green { background: var(--color-green); }
  .status-dot.orange { background: var(--color-orange); }
  .status-dot.red { background: var(--color-red); }

  .setting-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: var(--spacing-xs) 0;
  }

  .setting-info {
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  .setting-label {
    font-size: var(--font-size-base);
    font-weight: 500;
  }

  .setting-value {
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
  }

  .setting-desc {
    font-size: var(--font-size-xs);
    color: var(--color-tertiary-label);
  }

  .divider {
    height: 1px;
    background: var(--color-separator);
    margin: var(--spacing-sm) 0;
  }

  /* Device selector */
  .device-selector {
    position: relative;
  }

  .device-btn {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 4px 0;
    background: transparent;
    border: none;
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
    cursor: pointer;
    transition: opacity var(--transition-fast);
  }

  .device-btn:hover { opacity: 0.65; }

  .device-name {
    max-width: 200px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .chevron {
    color: var(--color-tertiary-label);
    transition: transform 0.2s ease;
    flex-shrink: 0;
  }

  .chevron.open {
    transform: rotate(180deg);
  }

  .device-dropdown {
    position: absolute;
    right: 0;
    top: calc(100% + 4px);
    min-width: 240px;
    max-width: 320px;
    background: var(--color-bg-grouped-secondary);
    border: 1px solid var(--color-separator);
    border-radius: var(--radius-sm);
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12);
    z-index: 100;
    overflow: hidden;
  }

  .device-option {
    display: flex;
    align-items: center;
    justify-content: space-between;
    width: 100%;
    padding: 8px 12px;
    background: transparent;
    border: none;
    font-size: var(--font-size-sm);
    color: var(--color-label);
    cursor: pointer;
    text-align: left;
    transition: background 0.15s ease;
  }

  .device-option:hover {
    background: var(--color-bg-secondary);
  }

  .device-option.selected {
    color: var(--color-blue);
    font-weight: 500;
  }

  .device-option + .device-option {
    border-top: 1px solid var(--color-separator);
  }
</style>
