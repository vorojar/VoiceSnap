<script lang="ts">
  interface Props {
    checked: boolean
    onchange?: (checked: boolean) => void
    disabled?: boolean
  }

  let { checked = $bindable(), onchange, disabled = false }: Props = $props()

  function toggle() {
    if (disabled) return
    checked = !checked
    onchange?.(checked)
  }
</script>

<button
  class="toggle"
  class:active={checked}
  class:disabled
  onclick={toggle}
  role="switch"
  aria-checked={checked}
>
  <div class="knob"></div>
</button>

<style>
  /* WPF: 36x20, macOS-style toggle */
  .toggle {
    position: relative;
    width: 36px;
    height: 20px;
    border-radius: 10px;
    background: #D1D1D6;
    border: none;
    cursor: pointer;
    transition: background var(--transition-fast);
    padding: 0;
    flex-shrink: 0;
  }

  .toggle.active {
    background: var(--color-green);
  }

  .toggle.disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  .knob {
    position: absolute;
    top: 2px;
    left: 2px;
    width: 16px;
    height: 16px;
    border-radius: 8px;
    background: white;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.15);
    transition: transform var(--transition-fast);
  }

  .toggle.active .knob {
    transform: translateX(16px);
  }
</style>
