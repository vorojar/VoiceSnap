//go:build darwin

package hotkey

/*
#cgo CFLAGS: -x objective-c
#cgo LDFLAGS: -framework Cocoa -framework ApplicationServices
#import <Cocoa/Cocoa.h>
#include <ApplicationServices/ApplicationServices.h>
static volatile int g_keysDown[128];
static volatile int g_monitorStarted = 0;

// handleKeyEvent tracks regular key state from NSEvent monitors.
static void handleKeyEvent(unsigned short keyCode, BOOL isDown) {
	if (keyCode >= 128) return;
	g_keysDown[keyCode] = isDown ? 1 : 0;
}

// Start NSEvent global monitors for regular key events.
// Modifier keys (Ctrl/Alt/Shift/Cmd) are handled via polling in isModifierDown().
static void startGlobalMonitor(void) {
	if (g_monitorStarted) return;
	g_monitorStarted = 1;

	NSEventMask keyMask = NSEventMaskKeyDown | NSEventMaskKeyUp;

	[NSEvent addGlobalMonitorForEventsMatchingMask:keyMask handler:^(NSEvent *event) {
		handleKeyEvent([event keyCode], [event type] == NSEventTypeKeyDown);
	}];

	[NSEvent addLocalMonitorForEventsMatchingMask:keyMask handler:^NSEvent*(NSEvent *event) {
		handleKeyEvent([event keyCode], [event type] == NSEventTypeKeyDown);
		return event;
	}];

	// monitors started
}

static int ensureAccessibility(void) {
	NSDictionary* opts = @{(__bridge NSString*)kAXTrustedCheckOptionPrompt: @YES};
	return AXIsProcessTrustedWithOptions((__bridge CFDictionaryRef)opts) ? 1 : 0;
}

static void startKeyMonitor(void) {
	dispatch_async(dispatch_get_main_queue(), ^{
		startGlobalMonitor();
	});
}

// isModifierDown polls the current system modifier flags via CGEventSource.
// This works globally without needing event tap or NSEvent monitor callbacks.
static int isModifierDown(unsigned long long flagMask) {
	CGEventFlags flags = CGEventSourceFlagsState(kCGEventSourceStateHIDSystemState);
	return (flags & flagMask) ? 1 : 0;
}

static int isKeyDown(int keyCode) {
	if (keyCode >= 0 && keyCode < 128) return g_keysDown[keyCode];
	return 0;
}

static int isMonitorStarted(void) {
	return g_monitorStarted;
}
*/
import "C"

import "voicesnap/internal/logger"

// macOS modifier flag masks for CGEventSourceFlagsState polling.
const (
	maskControl  = 0x40000  // kCGEventFlagMaskControl
	maskShift    = 0x20000  // kCGEventFlagMaskShift
	maskOption   = 0x80000  // kCGEventFlagMaskAlternate
	maskCommand  = 0x100000 // kCGEventFlagMaskCommand
	maskCapsLock = 0x10000  // kCGEventFlagMaskAlphaShift
)

// Modifier VK codes that use flag polling instead of key state array.
var vkToFlagMask = map[int]C.ulonglong{
	0x11: maskControl, // Ctrl
	0xA2: maskControl, // L-Ctrl
	0xA3: maskControl, // R-Ctrl
	0x12: maskOption,  // Alt
	0xA4: maskOption,  // L-Alt
	0xA5: maskOption,  // R-Alt
	0x10: maskShift,   // Shift
	0xA0: maskShift,   // L-Shift
	0xA1: maskShift,   // R-Shift
	0x14: maskCapsLock, // Caps Lock
	0x5B: maskCommand, // L-Cmd
	0x5C: maskCommand, // R-Cmd
}

// macOS key code mapping for non-modifier keys.
var vkToMac = map[int]int{
	0x20: 0x31, // Space
	0x09: 0x30, // Tab
	0x0D: 0x24, // Enter/Return
	0x1B: 0x35, // Esc
}

type darwinListener struct{}

func newPlatformListener() Listener {
	trusted := C.ensureAccessibility()
	if trusted == 0 {
		logger.Info("Accessibility permission not granted — hotkey won't work until granted")
	} else {
		logger.Info("Accessibility permission granted")
	}
	C.startKeyMonitor()
	return &darwinListener{}
}

func (l *darwinListener) IsKeyDown(vk int) bool {
	// Modifier keys: poll system flags directly
	if mask, ok := vkToFlagMask[vk]; ok {
		return C.isModifierDown(mask) != 0
	}
	// Regular keys: check NSEvent-tracked state
	macKey, ok := vkToMac[vk]
	if !ok {
		if vk >= 0x41 && vk <= 0x5A {
			macKey = vkToMacAlpha(vk)
		} else {
			return false
		}
	}
	return C.isKeyDown(C.int(macKey)) != 0
}

// sameModifierGroup maps each modifier VK to its group.
// All VKs in the same group share the same CGEventSourceFlagsState mask,
// so pressing any one of them makes all appear "down". We must exclude
// the entire group when checking for combination keys.
var modifierGroups = map[int]int{
	0x11: 1, 0xA2: 1, 0xA3: 1, // Ctrl / L-Ctrl / R-Ctrl
	0x12: 2, 0xA4: 2, 0xA5: 2, // Alt / L-Alt / R-Alt
	0x10: 3, 0xA0: 3, 0xA1: 3, // Shift / L-Shift / R-Shift
	0x5B: 4, 0x5C: 4,           // L-Cmd / R-Cmd
}

func (l *darwinListener) IsAnyOtherKeyPressed(excludeVK int) bool {
	excludeGroup := modifierGroups[excludeVK] // 0 if not a modifier

	modifiers := []int{0x11, 0xA2, 0xA3, 0x12, 0xA4, 0xA5, 0x10, 0xA0, 0xA1}
	for _, k := range modifiers {
		if k == excludeVK {
			continue
		}
		// Skip entire group sharing the same flag mask
		if excludeGroup != 0 && modifierGroups[k] == excludeGroup {
			continue
		}
		if l.IsKeyDown(k) {
			return true
		}
	}
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

func vkToMacAlpha(vk int) int {
	alphaMap := map[int]int{
		0x41: 0x00, 0x42: 0x0B, 0x43: 0x08, 0x44: 0x02,
		0x45: 0x0E, 0x46: 0x03, 0x47: 0x05, 0x48: 0x04,
		0x49: 0x22, 0x4A: 0x26, 0x4B: 0x28, 0x4C: 0x25,
		0x4D: 0x2E, 0x4E: 0x2D, 0x4F: 0x1F, 0x50: 0x23,
		0x51: 0x0C, 0x52: 0x0F, 0x53: 0x01, 0x54: 0x11,
		0x55: 0x20, 0x56: 0x09, 0x57: 0x0D, 0x58: 0x07,
		0x59: 0x10, 0x5A: 0x06,
	}
	if code, ok := alphaMap[vk]; ok {
		return code
	}
	return 0
}
