//go:build windows

package hotkey

import (
	"syscall"
	"unsafe"
)

var (
	user32              = syscall.NewLazyDLL("user32.dll")
	procGetAsyncKeyState = user32.NewProc("GetAsyncKeyState")
)

type windowsListener struct{}

func newPlatformListener() Listener {
	return &windowsListener{}
}

func getAsyncKeyState(vk int) uint16 {
	ret, _, _ := procGetAsyncKeyState.Call(uintptr(vk))
	return uint16(ret)
}

func (l *windowsListener) IsKeyDown(vk int) bool {
	return getAsyncKeyState(vk)&0x8000 != 0
}

func (l *windowsListener) IsAnyOtherKeyPressed(excludeVK int) bool {
	// Check meaningful keys: A-Z, 0-9, function keys, common keys
	checkKeys := []int{
		0x08, 0x09, 0x0D, 0x1B, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2C, 0x2D, 0x2E,
	}
	// 0-9
	for i := 0x30; i <= 0x39; i++ {
		checkKeys = append(checkKeys, i)
	}
	// A-Z
	for i := 0x41; i <= 0x5A; i++ {
		checkKeys = append(checkKeys, i)
	}
	// F1-F12
	for i := 0x70; i <= 0x7B; i++ {
		checkKeys = append(checkKeys, i)
	}
	// OEM keys
	checkKeys = append(checkKeys, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE)

	for _, k := range checkKeys {
		if k == excludeVK {
			continue
		}
		if getAsyncKeyState(k)&0x8000 != 0 {
			return true
		}
	}

	// Check modifier keys not matching the exclude key
	ctrlKeys := []int{0x11, 0xA2, 0xA3}
	altKeys := []int{0x12, 0xA4, 0xA5}
	shiftKeys := []int{0x10, 0xA0, 0xA1}

	if !contains(ctrlKeys, excludeVK) {
		for _, k := range ctrlKeys {
			if getAsyncKeyState(k)&0x8000 != 0 {
				return true
			}
		}
	}
	if !contains(altKeys, excludeVK) {
		for _, k := range altKeys {
			if getAsyncKeyState(k)&0x8000 != 0 {
				return true
			}
		}
	}
	if !contains(shiftKeys, excludeVK) {
		for _, k := range shiftKeys {
			if getAsyncKeyState(k)&0x8000 != 0 {
				return true
			}
		}
	}

	return false
}

func contains(slice []int, val int) bool {
	for _, v := range slice {
		if v == val {
			return true
		}
	}
	return false
}

// Ensure unsafe is used (needed for syscall alignment)
var _ = unsafe.Sizeof(0)
