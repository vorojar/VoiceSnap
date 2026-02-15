//go:build linux

package hotkey

/*
#cgo LDFLAGS: -lX11
#include <X11/Xlib.h>
#include <X11/keysym.h>
#include <string.h>

static Display* getDisplay() {
    static Display* dpy = NULL;
    if (dpy == NULL) {
        dpy = XOpenDisplay(NULL);
    }
    return dpy;
}

int queryKeyState(int keycode) {
    Display* dpy = getDisplay();
    if (dpy == NULL) return 0;
    char keys[32];
    XQueryKeymap(dpy, keys);
    return (keys[keycode / 8] >> (keycode % 8)) & 1;
}

int keysymToKeycode(unsigned long keysym) {
    Display* dpy = getDisplay();
    if (dpy == NULL) return 0;
    return XKeysymToKeycode(dpy, keysym);
}
*/
import "C"

// Windows VK to X11 KeySym mapping
var vkToKeySym = map[int]C.ulong{
	0x11: C.ulong(0xFFE3), // Ctrl -> XK_Control_L
	0xA2: C.ulong(0xFFE3), // L-Ctrl -> XK_Control_L
	0xA3: C.ulong(0xFFE4), // R-Ctrl -> XK_Control_R
	0x12: C.ulong(0xFFE9), // Alt -> XK_Alt_L
	0xA4: C.ulong(0xFFE9), // L-Alt -> XK_Alt_L
	0xA5: C.ulong(0xFFEA), // R-Alt -> XK_Alt_R
	0x10: C.ulong(0xFFE1), // Shift -> XK_Shift_L
	0xA0: C.ulong(0xFFE1), // L-Shift -> XK_Shift_L
	0xA1: C.ulong(0xFFE2), // R-Shift -> XK_Shift_R
	0x14: C.ulong(0xFFE5), // Caps Lock -> XK_Caps_Lock
	0x20: C.ulong(0x0020), // Space -> XK_space
	0x09: C.ulong(0xFF09), // Tab -> XK_Tab
	0x0D: C.ulong(0xFF0D), // Enter -> XK_Return
	0x1B: C.ulong(0xFF1B), // Esc -> XK_Escape
}

type linuxListener struct{}

func newPlatformListener() Listener {
	return &linuxListener{}
}

func (l *linuxListener) IsKeyDown(vk int) bool {
	keySym, ok := vkToKeySym[vk]
	if !ok {
		// A-Z
		if vk >= 0x41 && vk <= 0x5A {
			keySym = C.ulong(vk) // ASCII == X11 keysym for A-Z lowercase
		} else {
			return false
		}
	}
	keycode := C.keysymToKeycode(keySym)
	return C.queryKeyState(keycode) != 0
}

func (l *linuxListener) IsAnyOtherKeyPressed(excludeVK int) bool {
	modifiers := []int{0x11, 0xA2, 0xA3, 0x12, 0xA4, 0xA5, 0x10, 0xA0, 0xA1}
	for _, k := range modifiers {
		if k == excludeVK {
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
