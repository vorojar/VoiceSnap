package hotkey

import "fmt"

// Listener provides hotkey state polling functionality.
type Listener interface {
	// IsKeyDown returns true if the given virtual key is currently pressed.
	IsKeyDown(vk int) bool
	// IsAnyOtherKeyPressed returns true if any key other than the given vk is pressed.
	IsAnyOtherKeyPressed(excludeVK int) bool
}

// New creates a new platform-specific hotkey listener.
func New() Listener {
	return newPlatformListener()
}

// GetKeyName returns the display name for a virtual key code.
func GetKeyName(vk int) string {
	switch vk {
	case 0x11:
		return "Ctrl"
	case 0xA2:
		return "L-Ctrl"
	case 0xA3:
		return "R-Ctrl"
	case 0x12:
		return "Alt"
	case 0xA4:
		return "L-Alt"
	case 0xA5:
		return "R-Alt"
	case 0x10:
		return "Shift"
	case 0xA0:
		return "L-Shift"
	case 0xA1:
		return "R-Shift"
	case 0x14:
		return "Caps Lock"
	case 0x20:
		return "Space"
	case 0x09:
		return "Tab"
	case 0x0D:
		return "Enter"
	case 0x5B:
		return "L-Win"
	case 0x5C:
		return "R-Win"
	case 0x1B:
		return "Esc"
	default:
		if vk >= 0x41 && vk <= 0x5A {
			return string(rune(vk))
		}
		if vk >= 0x30 && vk <= 0x39 {
			return string(rune(vk))
		}
		if vk >= 0x70 && vk <= 0x7B {
			return fmt.Sprintf("F%d", vk-0x70+1)
		}
		return "Unknown"
	}
}
