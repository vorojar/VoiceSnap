//go:build darwin

package input

/*
#cgo LDFLAGS: -framework AppKit -framework CoreGraphics -framework Carbon
#include <AppKit/AppKit.h>
#include <CoreGraphics/CoreGraphics.h>
#include <Carbon/Carbon.h>

void setClipboardText(const char* text) {
    NSPasteboard* pb = [NSPasteboard generalPasteboard];
    [pb clearContents];
    NSString* str = [NSString stringWithUTF8String:text];
    [pb setString:str forType:NSPasteboardTypeString];
}

void simulateCmdV() {
    CGEventSourceRef source = CGEventSourceCreate(kCGEventSourceStateCombinedSessionState);

    // Cmd down + V down
    CGEventRef cmdDown = CGEventCreateKeyboardEvent(source, (CGKeyCode)0x09, true); // V key
    CGEventSetFlags(cmdDown, kCGEventFlagMaskCommand);
    CGEventPost(kCGHIDEventTap, cmdDown);

    // Cmd down + V up
    CGEventRef cmdUp = CGEventCreateKeyboardEvent(source, (CGKeyCode)0x09, false);
    CGEventSetFlags(cmdUp, kCGEventFlagMaskCommand);
    CGEventPost(kCGHIDEventTap, cmdUp);

    CFRelease(cmdDown);
    CFRelease(cmdUp);
    CFRelease(source);
}

void typeUnicodeChar(unsigned int codepoint) {
    CGEventSourceRef source = CGEventSourceCreate(kCGEventSourceStateCombinedSessionState);
    UniChar chars[2];
    int len = 0;

    if (codepoint <= 0xFFFF) {
        chars[0] = (UniChar)codepoint;
        len = 1;
    } else {
        codepoint -= 0x10000;
        chars[0] = (UniChar)(0xD800 + (codepoint >> 10));
        chars[1] = (UniChar)(0xDC00 + (codepoint & 0x3FF));
        len = 2;
    }

    CGEventRef keyDown = CGEventCreateKeyboardEvent(source, 0, true);
    CGEventKeyboardSetUnicodeString(keyDown, len, chars);
    CGEventPost(kCGHIDEventTap, keyDown);

    CGEventRef keyUp = CGEventCreateKeyboardEvent(source, 0, false);
    CGEventKeyboardSetUnicodeString(keyUp, len, chars);
    CGEventPost(kCGHIDEventTap, keyUp);

    CFRelease(keyDown);
    CFRelease(keyUp);
    CFRelease(source);
}
*/
import "C"

import "unsafe"

type darwinPaster struct{}

func newPlatformPaster() Paster {
	return &darwinPaster{}
}

func (p *darwinPaster) Paste(text string) error {
	cstr := C.CString(text)
	defer C.free(unsafe.Pointer(cstr))
	C.setClipboardText(cstr)
	C.simulateCmdV()
	return nil
}

func (p *darwinPaster) TypeText(text string) error {
	for _, r := range text {
		C.typeUnicodeChar(C.uint(r))
	}
	return nil
}
