<script lang="ts">
  import { onMount } from 'svelte'
  import { Events } from '@wailsio/runtime'
  import FloatingIndicator from './components/indicator/FloatingIndicator.svelte'
  import SettingsWindow from './components/settings/SettingsWindow.svelte'
  import OnboardingView from './components/onboarding/OnboardingView.svelte'
  import UpdateDialog from './components/update/UpdateDialog.svelte'
  import { engineStatus } from './lib/stores/app'
  import { indicatorVisible, indicatorStatus, indicatorVolume, hotkeyName } from './lib/stores/indicator'
  import { updateAvailable, type UpdateInfo } from './lib/stores/update'
  import { deviceName, engineHardwareInfo } from './lib/stores/app'

  let isIndicatorRoute = $state(window.location.hash === '#/indicator')
  let showUpdateDialog = $state(false)

  onMount(() => {
    Events.On('indicator:show', (ev: any) => {
      indicatorVisible.set(true)
      if (ev?.data?.status) {
        indicatorStatus.set(ev.data.status)
      }
    })

    Events.On('indicator:hide', () => {
      indicatorVisible.set(false)
    })

    Events.On('indicator:status', (ev: any) => {
      const data = ev?.data
      if (data?.status) {
        indicatorStatus.set(data.status)
      }
      if (data?.hotkeyName) {
        hotkeyName.set(data.hotkeyName)
      }
    })

    Events.On('indicator:volume', (ev: any) => {
      indicatorVolume.set(ev?.data ?? 0)
    })

    Events.On('engine:status', (ev: any) => {
      const data = ev?.data
      if (data?.status) {
        engineStatus.set(data.status as any)
      }
      if (data?.hardwareInfo) {
        engineHardwareInfo.set(data.hardwareInfo)
      }
    })

    Events.On('device:changed', (ev: any) => {
      if (ev?.data?.name) {
        deviceName.set(ev.data.name)
      }
    })

    Events.On('update:available', (ev: any) => {
      const data = ev?.data as UpdateInfo
      updateAvailable.set(data)
      showUpdateDialog = true
    })

    window.addEventListener('hashchange', () => {
      isIndicatorRoute = window.location.hash === '#/indicator'
    })
  })
</script>

{#if isIndicatorRoute}
  <FloatingIndicator />
{:else}
  {#if $engineStatus === 'need_model'}
    <OnboardingView />
  {:else}
    <SettingsWindow />
  {/if}

  {#if showUpdateDialog && $updateAvailable}
    <UpdateDialog
      info={$updateAvailable}
      onclose={() => { showUpdateDialog = false }}
    />
  {/if}
{/if}
