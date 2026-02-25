<script lang="ts">
  import { Call } from '@wailsio/runtime'
  import { t } from '../../lib/i18n'

  interface HistoryEntry {
    text: string
    timestamp: number
  }

  let entries = $state<HistoryEntry[]>([])
  let retentionDays = $state(30)
  let copiedTs = $state<number | null>(null)
  let showConfirm = $state(false)
  let showRetention = $state(false)

  const retentionOptions = [
    { value: 7, label: () => t('history.days7') },
    { value: 30, label: () => t('history.days30') },
    { value: 90, label: () => t('history.days90') },
    { value: 0, label: () => t('history.forever') },
  ]

  function currentRetentionLabel(): string {
    const opt = retentionOptions.find(o => o.value === retentionDays)
    return opt ? opt.label() : t('history.days30')
  }

  async function loadHistory() {
    try {
      const list: any = await Call.ByName('voicesnap/services.HistoryService.GetAll')
      entries = list || []
    } catch {
      entries = []
    }
    try {
      const days: any = await Call.ByName('voicesnap/services.HistoryService.GetRetentionDays')
      retentionDays = (days !== null && days !== undefined) ? days : 30
    } catch {}
  }

  async function setRetention(days: number) {
    retentionDays = days
    showRetention = false
    try {
      await Call.ByName('voicesnap/services.HistoryService.SetRetentionDays', days)
      await loadHistory()
    } catch {}
  }

  async function copyText(entry: HistoryEntry) {
    try {
      await navigator.clipboard.writeText(entry.text)
      copiedTs = entry.timestamp
      setTimeout(() => { if (copiedTs === entry.timestamp) copiedTs = null }, 1500)
    } catch {}
  }

  async function deleteEntry(timestamp: number) {
    try {
      await Call.ByName('voicesnap/services.HistoryService.Delete', timestamp)
      entries = entries.filter(e => e.timestamp !== timestamp)
    } catch {}
  }

  async function clearAll() {
    showConfirm = false
    try {
      await Call.ByName('voicesnap/services.HistoryService.ClearAll')
      entries = []
    } catch {}
  }

  function formatTime(ts: number): string {
    const date = new Date(ts)
    const now = new Date()
    const hours = date.getHours().toString().padStart(2, '0')
    const mins = date.getMinutes().toString().padStart(2, '0')
    const time = `${hours}:${mins}`

    const isToday = date.toDateString() === now.toDateString()
    if (isToday) return time

    const yesterday = new Date(now)
    yesterday.setDate(yesterday.getDate() - 1)
    if (date.toDateString() === yesterday.toDateString()) {
      return `${t('history.yesterday')} ${time}`
    }

    const month = (date.getMonth() + 1).toString()
    const day = date.getDate().toString()
    return `${month}/${day} ${time}`
  }

  function closeDropdowns(e: MouseEvent) {
    const target = e.target as HTMLElement
    if (!target.closest('.retention-selector')) {
      showRetention = false
    }
  }

  // Load on mount
  loadHistory()
</script>

<!-- svelte-ignore a11y_click_events_have_key_events -->
<!-- svelte-ignore a11y_no_static_element_interactions -->
<div class="page" onclick={closeDropdowns}>
  <!-- Toolbar: retention + clear all -->
  <div class="toolbar">
    <div class="toolbar-left">
      <span class="toolbar-label">{t('history.retention')}</span>
      <div class="retention-selector">
        <button class="retention-btn" onclick={() => showRetention = !showRetention}>
          <span>{currentRetentionLabel()}</span>
          <svg class="chevron" class:open={showRetention} width="10" height="6" viewBox="0 0 10 6" fill="none">
            <path d="M1 1L5 5L9 1" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
        </button>
        {#if showRetention}
          <div class="retention-dropdown">
            {#each retentionOptions as opt}
              <button
                class="retention-option"
                class:selected={retentionDays === opt.value}
                onclick={() => setRetention(opt.value)}
              >
                {opt.label()}
                {#if retentionDays === opt.value}
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

    {#if entries.length > 0}
      <div class="toolbar-right">
        {#if showConfirm}
          <span class="confirm-text">{t('history.clearConfirm')}</span>
          <button class="clear-btn confirm" onclick={clearAll}>{t('history.clearAll')}</button>
          <button class="cancel-btn" onclick={() => showConfirm = false}>&times;</button>
        {:else}
          <button class="clear-btn" onclick={() => showConfirm = true}>{t('history.clearAll')}</button>
        {/if}
      </div>
    {/if}
  </div>

  <!-- History list -->
  <div class="section list-section">
    {#if entries.length === 0}
      <div class="empty">
        <p class="empty-title">{t('history.empty')}</p>
        <p class="empty-desc">{t('history.emptyDesc')}</p>
      </div>
    {:else}
      {#each entries as entry, i}
        {#if i > 0}
          <div class="divider"></div>
        {/if}
        <div class="history-row">
          <div class="history-content">
            <span class="history-time">{formatTime(entry.timestamp)}</span>
            <span class="history-text">{entry.text}</span>
          </div>
          <div class="history-actions">
            <button class="action-btn copy-btn" onclick={() => copyText(entry)}>
              {#if copiedTs === entry.timestamp}
                <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                  <path d="M2 7L5.5 10.5L12 4" stroke="var(--color-green)" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
                </svg>
              {:else}
                <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                  <rect x="5" y="5" width="7.5" height="7.5" rx="1.5" stroke="currentColor" stroke-width="1.2"/>
                  <path d="M9 5V3C9 2.17 8.33 1.5 7.5 1.5H3C2.17 1.5 1.5 2.17 1.5 3V7.5C1.5 8.33 2.17 9 3 9H5" stroke="currentColor" stroke-width="1.2"/>
                </svg>
              {/if}
            </button>
            <button class="action-btn delete-btn" onclick={() => deleteEntry(entry.timestamp)}>
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                <path d="M3 3.5L11 11.5M11 3.5L3 11.5" stroke="currentColor" stroke-width="1.2" stroke-linecap="round"/>
              </svg>
            </button>
          </div>
        </div>
      {/each}
    {/if}
  </div>
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    height: calc(100vh - 40px);
  }

  /* Toolbar */
  .toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: var(--spacing-md);
    padding: 0 2px;
  }

  .toolbar-left {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .toolbar-label {
    font-size: var(--font-size-sm);
    color: var(--color-secondary-label);
  }

  .toolbar-right {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  /* Retention dropdown */
  .retention-selector {
    position: relative;
  }

  .retention-btn {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 3px 0;
    background: transparent;
    border: none;
    font-size: var(--font-size-sm);
    font-weight: 500;
    color: var(--color-blue);
    cursor: pointer;
    transition: opacity var(--transition-fast);
  }

  .retention-btn:hover { opacity: 0.65; }

  .chevron {
    color: var(--color-tertiary-label);
    transition: transform 0.2s ease;
    flex-shrink: 0;
  }

  .chevron.open {
    transform: rotate(180deg);
  }

  .retention-dropdown {
    position: absolute;
    left: 0;
    top: calc(100% + 4px);
    min-width: 120px;
    background: var(--color-bg-grouped-secondary);
    border: 1px solid var(--color-separator);
    border-radius: var(--radius-sm);
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12);
    z-index: 100;
    overflow: hidden;
  }

  .retention-option {
    display: flex;
    align-items: center;
    justify-content: space-between;
    width: 100%;
    padding: 7px 12px;
    background: transparent;
    border: none;
    font-size: var(--font-size-sm);
    color: var(--color-label);
    cursor: pointer;
    text-align: left;
    transition: background 0.15s ease;
  }

  .retention-option:hover {
    background: var(--color-bg-secondary);
  }

  .retention-option.selected {
    color: var(--color-blue);
    font-weight: 500;
  }

  .retention-option + .retention-option {
    border-top: 1px solid var(--color-separator);
  }

  /* Clear all */
  .clear-btn {
    padding: 3px 0;
    background: transparent;
    border: none;
    font-size: var(--font-size-sm);
    color: var(--color-tertiary-label);
    cursor: pointer;
    transition: color 0.15s ease;
  }

  .clear-btn:hover {
    color: var(--color-red);
  }

  .clear-btn.confirm {
    color: var(--color-red);
    font-weight: 500;
  }

  .confirm-text {
    font-size: var(--font-size-xs);
    color: var(--color-secondary-label);
  }

  .cancel-btn {
    padding: 0 4px;
    background: transparent;
    border: none;
    font-size: var(--font-size-base);
    color: var(--color-tertiary-label);
    cursor: pointer;
    line-height: 1;
  }

  .cancel-btn:hover { color: var(--color-label); }

  /* List */
  .section {
    background: var(--color-bg-grouped-secondary);
    border-radius: var(--radius-md);
    padding: var(--spacing-lg);
  }

  .list-section {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
  }

  .empty {
    text-align: center;
    padding: 40px 0;
  }

  .empty-title {
    font-size: var(--font-size-base);
    color: var(--color-secondary-label);
    font-weight: 500;
  }

  .empty-desc {
    font-size: var(--font-size-xs);
    color: var(--color-tertiary-label);
    margin-top: 6px;
  }

  .history-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 8px 0;
    gap: 12px;
  }

  .history-content {
    display: flex;
    align-items: baseline;
    gap: 10px;
    min-width: 0;
    flex: 1;
  }

  .history-time {
    font-size: var(--font-size-xs);
    color: var(--color-tertiary-label);
    white-space: nowrap;
    flex-shrink: 0;
    min-width: 40px;
  }

  .history-text {
    font-size: var(--font-size-sm);
    color: var(--color-label);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .history-actions {
    display: flex;
    gap: 4px;
    flex-shrink: 0;
    opacity: 0;
    transition: opacity 0.15s ease;
  }

  .history-row:hover .history-actions {
    opacity: 1;
  }

  .action-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 28px;
    height: 28px;
    border: none;
    background: transparent;
    border-radius: var(--radius-sm);
    color: var(--color-tertiary-label);
    cursor: pointer;
    transition: all 0.15s ease;
  }

  .action-btn:hover {
    background: var(--color-bg-secondary);
  }

  .copy-btn:hover {
    color: var(--color-blue);
  }

  .delete-btn:hover {
    color: var(--color-red);
  }

  .divider {
    height: 1px;
    background: var(--color-separator);
  }
</style>
