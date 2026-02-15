<script lang="ts">
  import { onMount, onDestroy } from 'svelte'
  import { indicatorVisible, indicatorStatus, indicatorVolume, hotkeyName, type IndicatorStatus } from '../../lib/stores/indicator'

  let barHeights = $state([14, 14, 14, 14, 14])
  let realVolumes = $state([0, 0, 0, 0, 0])
  let animationTime = 0
  let rafId: number | null = null
  let status: IndicatorStatus = $state('hidden')
  let statusText = $state('')
  let barColor = $state('#007AFF')
  let key = $state('Ctrl')

  const unsubVisible = indicatorVisible.subscribe(() => {})
  const unsubStatus = indicatorStatus.subscribe(s => {
    status = s
    updateVisuals(s)
  })
  const unsubVolume = indicatorVolume.subscribe(vol => {
    for (let i = 0; i < 4; i++) realVolumes[i] = realVolumes[i + 1]
    realVolumes[4] = vol
  })
  const unsubHotkey = hotkeyName.subscribe(k => { key = k })

  const AppleBlue   = '#007AFF'
  const AppleGreen  = '#34C759'
  const AppleRed    = '#FF3B30'
  const AppleOrange = '#FF9500'

  function updateVisuals(s: IndicatorStatus) {
    switch (s) {
      case 'loading':
        barColor = AppleBlue;  statusText = '加载中'; break
      case 'ready':
        barColor = AppleGreen; statusText = `按住${key}说话`; break
      case 'recording':
        barColor = AppleRed;   statusText = ''; break
      case 'processing':
        barColor = AppleOrange; statusText = '识别中'; break
      case 'done':
        barColor = AppleGreen; statusText = '完成'; break
      case 'cancelled':
        barColor = AppleOrange; statusText = '已取消'; break
      case 'no_voice':
        barColor = AppleOrange; statusText = '无语音'; break
      case 'no_content':
        barColor = AppleOrange; statusText = '无内容'; break
      case 'error':
        barColor = AppleRed;   statusText = '错误'; break
      default:
        barColor = AppleBlue;  statusText = ''
    }
  }

  function animate() {
    animationTime += 0.016

    switch (status) {
      case 'loading': {
        for (let i = 0; i < 5; i++) {
          const pulse = Math.sin(animationTime * 2 + i * 0.3) * 0.5 + 0.5
          barHeights[i] = 10 + pulse * 20
        }
        break
      }
      case 'ready':
      case 'done': {
        for (let i = 0; i < 5; i++) barHeights[i] = barHeights[i] * 0.85 + 14 * 0.15
        break
      }
      case 'recording': {
        const hasRealData = realVolumes.some(v => v > 0.01)
        for (let i = 0; i < 5; i++) {
          let target: number
          if (hasRealData) {
            target = 8 + realVolumes[i] * 32
          } else {
            const wave = Math.sin(animationTime * 4 + i * 0.8) * 0.5 + 0.5
            const random = Math.sin(animationTime * 7 + i * 1.5) * 0.3
            target = 10 + (wave + random) * 25
          }
          barHeights[i] = barHeights[i] * 0.6 + target * 0.4
        }
        break
      }
      case 'processing': {
        for (let i = 0; i < 5; i++) {
          const wave = Math.sin(animationTime * 3 + i * 0.6) * 0.5 + 0.5
          barHeights[i] = 12 + wave * 18
        }
        break
      }
      default: {
        for (let i = 0; i < 5; i++) barHeights[i] = barHeights[i] * 0.85 + 14 * 0.15
      }
    }

    rafId = requestAnimationFrame(animate)
  }

  onMount(() => {
    rafId = requestAnimationFrame(animate)
  })

  onDestroy(() => {
    if (rafId !== null) cancelAnimationFrame(rafId)
    unsubVisible(); unsubStatus(); unsubVolume(); unsubHotkey()
  })
</script>

<!-- Window IS the capsule: 170x48, solid dark bg, frameless -->
<div class="capsule">
  <div class="bars">
    {#each barHeights as h}
      <div class="bar" style="height:{Math.round(h)}px;background:{barColor}"></div>
    {/each}
  </div>
  {#if statusText}
    <span class="text">{statusText}</span>
  {/if}
</div>

<style>
  /* Native overlay replaced this component — no global overrides */

  /* Capsule fills entire window */
  .capsule {
    width: 170px;
    height: 48px;
    border-radius: 24px;
    background: rgba(28, 28, 30, 0.85);
    border: 1px solid rgba(255, 255, 255, 0.125);
    display: flex;
    align-items: center;
    justify-content: center;
    box-sizing: border-box;
  }

  .bars {
    display: flex;
    align-items: center;
    gap: 4px;
    height: 40px;
    flex-shrink: 0;
  }

  .bar {
    width: 5px;
    border-radius: 2.5px;
    min-height: 6px;
    max-height: 40px;
    will-change: height;
  }

  .text {
    margin-left: 12px;
    color: #B4B4B4;
    font-family: "Segoe UI", -apple-system, sans-serif;
    font-size: 12px;
    font-weight: 400;
    white-space: nowrap;
    user-select: none;
    flex-shrink: 0;
  }
</style>
