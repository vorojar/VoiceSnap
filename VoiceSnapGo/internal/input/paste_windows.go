//go:build windows

package input

import (
	"fmt"
	"syscall"
	"time"
	"unsafe"
)

var (
	user32               = syscall.NewLazyDLL("user32.dll")
	kernel32             = syscall.NewLazyDLL("kernel32.dll")
	procOpenClipboard    = user32.NewProc("OpenClipboard")
	procCloseClipboard   = user32.NewProc("CloseClipboard")
	procEmptyClipboard   = user32.NewProc("EmptyClipboard")
	procSetClipboardData = user32.NewProc("SetClipboardData")
	procGetClipboardData = user32.NewProc("GetClipboardData")
	procIsClipboardFormatAvailable = user32.NewProc("IsClipboardFormatAvailable")
	procSendInput        = user32.NewProc("SendInput")
	procGlobalAlloc      = kernel32.NewProc("GlobalAlloc")
	procGlobalLock       = kernel32.NewProc("GlobalLock")
	procGlobalUnlock     = kernel32.NewProc("GlobalUnlock")
	procGlobalSize       = kernel32.NewProc("GlobalSize")
)

const (
	cfUnicodeText    = 13
	gmemMoveable     = 0x0002
	inputKeyboard    = 1
	keyeventfKeyUp   = 0x0002
	keyeventfUnicode = 0x0004
	vkControl        = 0x11
	vkV              = 0x56
)

type keyboardInput struct {
	wVk         uint16
	wScan       uint16
	dwFlags     uint32
	time        uint32
	dwExtraInfo uintptr
}

type input struct {
	inputType uint32
	ki        keyboardInput
	padding   [8]byte // Alignment padding
}

type windowsPaster struct{}

func newPlatformPaster() Paster {
	return &windowsPaster{}
}

func (p *windowsPaster) Paste(text string) error {
	// Save original clipboard content
	oldClip := getClipboardText()

	if err := setClipboard(text); err != nil {
		return err
	}

	// Simulate Ctrl+V
	inputs := []input{
		makeKeyInput(vkControl, 0, 0),
		makeKeyInput(vkV, 0, 0),
		makeKeyInput(vkV, 0, keyeventfKeyUp),
		makeKeyInput(vkControl, 0, keyeventfKeyUp),
	}

	ret, _, err := procSendInput.Call(
		uintptr(len(inputs)),
		uintptr(unsafe.Pointer(&inputs[0])),
		uintptr(unsafe.Sizeof(inputs[0])),
	)
	if ret == 0 {
		return fmt.Errorf("SendInput failed: %v", err)
	}

	// Restore original clipboard after a short delay (let Ctrl+V complete)
	if oldClip != nil {
		go func() {
			time.Sleep(300 * time.Millisecond)
			setClipboard(*oldClip)
		}()
	}

	return nil
}

func (p *windowsPaster) TypeText(text string) error {
	runes := []rune(text)
	inputs := make([]input, len(runes)*2)

	for i, r := range runes {
		// Key down
		inputs[i*2] = input{
			inputType: inputKeyboard,
			ki: keyboardInput{
				wVk:     0,
				wScan:   uint16(r),
				dwFlags: keyeventfUnicode,
			},
		}
		// Key up
		inputs[i*2+1] = input{
			inputType: inputKeyboard,
			ki: keyboardInput{
				wVk:     0,
				wScan:   uint16(r),
				dwFlags: keyeventfUnicode | keyeventfKeyUp,
			},
		}
	}

	ret, _, err := procSendInput.Call(
		uintptr(len(inputs)),
		uintptr(unsafe.Pointer(&inputs[0])),
		uintptr(unsafe.Sizeof(inputs[0])),
	)
	if ret == 0 {
		return fmt.Errorf("SendInput (unicode) failed: %v", err)
	}
	return nil
}

// getClipboardText reads the current clipboard text, returns nil if empty or non-text.
func getClipboardText() *string {
	ret, _, _ := procIsClipboardFormatAvailable.Call(cfUnicodeText)
	if ret == 0 {
		return nil
	}

	for i := 0; i < 5; i++ {
		ret, _, _ := procOpenClipboard.Call(0)
		if ret != 0 {
			hMem, _, _ := procGetClipboardData.Call(cfUnicodeText)
			if hMem == 0 {
				procCloseClipboard.Call()
				return nil
			}

			size, _, _ := procGlobalSize.Call(hMem)
			if size == 0 {
				procCloseClipboard.Call()
				return nil
			}

			ptr, _, _ := procGlobalLock.Call(hMem)
			if ptr == 0 {
				procCloseClipboard.Call()
				return nil
			}

			// Read UTF-16 string
			text := syscall.UTF16ToString((*[1 << 20]uint16)(unsafe.Pointer(ptr))[:size/2])
			procGlobalUnlock.Call(hMem)
			procCloseClipboard.Call()
			return &text
		}
		time.Sleep(30 * time.Millisecond)
	}
	return nil
}

func setClipboard(text string) error {
	// Retry opening clipboard up to 10 times
	for i := 0; i < 10; i++ {
		ret, _, _ := procOpenClipboard.Call(0)
		if ret != 0 {
			defer procCloseClipboard.Call()

			procEmptyClipboard.Call()

			utf16 := syscall.StringToUTF16(text)
			size := len(utf16) * 2

			hMem, _, _ := procGlobalAlloc.Call(gmemMoveable, uintptr(size))
			if hMem == 0 {
				return fmt.Errorf("GlobalAlloc failed")
			}

			ptr, _, _ := procGlobalLock.Call(hMem)
			if ptr == 0 {
				return fmt.Errorf("GlobalLock failed")
			}

			// Copy UTF-16 data
			src := unsafe.Pointer(&utf16[0])
			dst := unsafe.Pointer(ptr)
			copy((*[1 << 30]byte)(dst)[:size], (*[1 << 30]byte)(src)[:size])

			procGlobalUnlock.Call(hMem)
			procSetClipboardData.Call(cfUnicodeText, hMem)

			return nil
		}
		time.Sleep(50 * time.Millisecond)
	}
	return fmt.Errorf("failed to open clipboard after 10 attempts")
}

func makeKeyInput(vk uint16, scan uint16, flags uint32) input {
	return input{
		inputType: inputKeyboard,
		ki: keyboardInput{
			wVk:     vk,
			wScan:   scan,
			dwFlags: flags,
		},
	}
}
