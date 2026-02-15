//go:build darwin

package hotkey

/*
#cgo LDFLAGS: -framework CoreGraphics -framework Carbon
#include <CoreGraphics/CoreGraphics.h>
#include <Carbon/Carbon.h>

int isKeyDown(int keyCode) {
    CGEventSourceRef source = CGEventSourceCreate(kCGEventSourceStateCombinedSessionState);
    if (source == NULL) return 0;
    bool down = CGEventSourceKeyState(source, (CGKeyCode)keyCode);
    CFRelease(source);
    return down ? 1 : 0;
}
*/
import "C"

// macOS key code mapping from VK (Windows-style) to macOS CGKeyCode.
var vkToMac = map[int]int{
	0x11: 0x3B, // Ctrl -> kVK_Control
	0xA2: 0x3B, // L-Ctrl -> kVK_Control
	0xA3: 0x3E, // R-Ctrl -> kVK_RightControl
	0x12: 0x3A, // Alt -> kVK_Option
	0xA4: 0x3A, // L-Alt -> kVK_Option
	0xA5: 0x3D, // R-Alt -> kVK_RightOption
	0x10: 0x38, // Shift -> kVK_Shift
	0xA0: 0x38, // L-Shift -> kVK_Shift
	0xA1: 0x3C, // R-Shift -> kVK_RightShift
	0x14: 0x39, // Caps Lock
	0x20: 0x31, // Space
	0x09: 0x30, // Tab
	0x0D: 0x24, // Enter/Return
	0x5B: 0x37, // L-Cmd (Win)
	0x5C: 0x36, // R-Cmd
	0x1B: 0x35, // Esc
}

type darwinListener struct{}

func newPlatformListener() Listener {
	return &darwinListener{}
}

func (l *darwinListener) IsKeyDown(vk int) bool {
	macKey, ok := vkToMac[vk]
	if !ok {
		// Try direct mapping for A-Z (0x41-0x5A)
		if vk >= 0x41 && vk <= 0x5A {
			macKey = vkToMacAlpha(vk)
		} else {
			return false
		}
	}
	return C.isKeyDown(C.int(macKey)) != 0
}

func (l *darwinListener) IsAnyOtherKeyPressed(excludeVK int) bool {
	// Check common modifier keys
	modifiers := []int{0x11, 0xA2, 0xA3, 0x12, 0xA4, 0xA5, 0x10, 0xA0, 0xA1}
	for _, k := range modifiers {
		if k == excludeVK {
			continue
		}
		if l.IsKeyDown(k) {
			return true
		}
	}
	// Check A-Z
	for vk := 0x41; vk <= 0x5A; vk++ {
		if vk == excludeVK {
			continue
		}
		if l.IsKeyDown(vk) {
			return true
		}
	}
	return false
}

// vkToMacAlpha converts Windows A-Z VK codes to macOS key codes.
func vkToMacAlpha(vk int) int {
	// macOS keycode layout (QWERTY)
	alphaMap := map[int]int{
		0x41: 0x00, // A
		0x42: 0x0B, // B
		0x43: 0x08, // C
		0x44: 0x02, // D
		0x45: 0x0E, // E
		0x46: 0x03, // F
		0x47: 0x05, // G
		0x48: 0x04, // H
		0x49: 0x22, // I
		0x4A: 0x26, // J
		0x4B: 0x28, // K
		0x4C: 0x25, // L
		0x4D: 0x2E, // M
		0x4E: 0x2D, // N
		0x4F: 0x1F, // O
		0x50: 0x23, // P
		0x51: 0x0C, // Q
		0x52: 0x0F, // R
		0x53: 0x01, // S
		0x54: 0x11, // T
		0x55: 0x20, // U
		0x56: 0x09, // V
		0x57: 0x0D, // W
		0x58: 0x07, // X
		0x59: 0x10, // Y
		0x5A: 0x06, // Z
	}
	if code, ok := alphaMap[vk]; ok {
		return code
	}
	return 0
}
