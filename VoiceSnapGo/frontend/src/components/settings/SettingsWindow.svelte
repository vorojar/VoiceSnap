<script lang="ts">
  import { currentPage } from '../../lib/stores/app'
  import { t } from '../../lib/i18n'
  import GeneralPage from './GeneralPage.svelte'
  import AboutPage from './AboutPage.svelte'

  const navItems = [
    { id: 'general', label: () => t('nav.home') },
    { id: 'about', label: () => t('nav.about') },
  ]

  let page = $state('general')
  const unsubPage = currentPage.subscribe(p => { page = p })

  function navigate(id: string) {
    currentPage.set(id)
  }
</script>

<div class="settings-layout">
  <!-- Sidebar -->
  <nav class="sidebar">
    <div class="sidebar-header">
      <div class="app-icon">V</div>
      <span class="app-name">VoiceSnap</span>
    </div>

    <div class="nav-items">
      {#each navItems as item}
        <button
          class="nav-item"
          class:active={page === item.id}
          onclick={() => navigate(item.id)}
        >
          <span class="nav-icon">
            {#if item.id === 'general'}
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round" stroke-linejoin="round">
                <path d="M2.5 7.5L8 3l5.5 4.5"/>
                <path d="M4 6.5V13h3v-3.5h2V13h3V6.5"/>
              </svg>
            {:else if item.id === 'about'}
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round">
                <circle cx="8" cy="8" r="6"/>
                <path d="M8 7.5V11"/>
                <circle cx="8" cy="5.5" r="0.6" fill="currentColor" stroke="none"/>
              </svg>
            {/if}
          </span>
          <span class="nav-label">{item.label()}</span>
        </button>
      {/each}
    </div>
  </nav>

  <!-- Content -->
  <main class="content">
    {#if page === 'general'}
      <GeneralPage />
    {:else if page === 'about'}
      <AboutPage />
    {/if}
  </main>
</div>

<style>
  .settings-layout {
    display: flex;
    height: 100vh;
    overflow: hidden;
  }

  .sidebar {
    width: var(--sidebar-width);
    display: flex;
    flex-direction: column;
    padding: var(--spacing-lg);
    padding-top: 40px;
    border-right: 1px solid var(--color-separator);
    -webkit-app-region: drag;
  }

  .sidebar-header {
    display: flex;
    align-items: center;
    gap: var(--spacing-sm);
    margin-bottom: var(--spacing-xl);
    -webkit-app-region: no-drag;
  }

  .app-icon {
    width: 24px;
    height: 24px;
    background: var(--color-blue);
    border-radius: 6px;
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    font-weight: 700;
    font-size: 11px;
  }

  .app-name {
    font-weight: 600;
    font-size: var(--font-size-lg);
  }

  .nav-items {
    display: flex;
    flex-direction: column;
    gap: 2px;
    -webkit-app-region: no-drag;
  }

  .nav-item {
    display: flex;
    align-items: center;
    gap: var(--spacing-sm);
    padding: var(--spacing-sm) var(--spacing-md);
    border-radius: var(--radius-sm);
    border: none;
    background: transparent;
    cursor: pointer;
    font-size: var(--font-size-base);
    color: var(--color-secondary-label);
    transition: all var(--transition-fast);
    text-align: left;
    width: 100%;
    position: relative;
  }

  .nav-item:hover {
    background: rgba(0, 0, 0, 0.04);
    color: var(--color-label);
  }

  .nav-item.active {
    background: rgba(0, 122, 255, 0.08);
    color: var(--color-blue);
  }

  .nav-item.active::before {
    content: '';
    position: absolute;
    left: 0;
    top: 6px;
    bottom: 6px;
    width: 3px;
    border-radius: 2px;
    background: var(--color-blue);
  }

  .nav-icon {
    width: 20px;
    height: 16px;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .nav-label {
    font-weight: 500;
  }

  .content {
    flex: 1;
    overflow-y: auto;
    padding: 20px;
  }
</style>
