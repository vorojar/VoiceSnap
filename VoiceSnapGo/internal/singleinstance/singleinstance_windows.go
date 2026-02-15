//go:build windows

package singleinstance

import (
	"fmt"
	"syscall"
	"unsafe"
)

var (
	kernel32         = syscall.NewLazyDLL("kernel32.dll")
	procCreateMutex  = kernel32.NewProc("CreateMutexW")
	procReleaseMutex = kernel32.NewProc("ReleaseMutex")
	procCloseHandle  = kernel32.NewProc("CloseHandle")
)

const errorAlreadyExists = 183

type windowsLock struct {
	handle syscall.Handle
}

// Acquire tries to acquire a named mutex. Returns error if another instance holds it.
func Acquire() (Lock, error) {
	name, err := syscall.UTF16PtrFromString("Global\\VoiceSnapSingleInstance")
	if err != nil {
		return nil, err
	}

	handle, _, err := procCreateMutex.Call(0, 0, uintptr(unsafe.Pointer(name)))
	if handle == 0 {
		return nil, fmt.Errorf("CreateMutex failed: %v", err)
	}

	if err.(syscall.Errno) == errorAlreadyExists {
		procCloseHandle.Call(handle)
		return nil, fmt.Errorf("another instance is already running")
	}

	return &windowsLock{handle: syscall.Handle(handle)}, nil
}

func (l *windowsLock) Release() {
	if l.handle != 0 {
		procReleaseMutex.Call(uintptr(l.handle))
		procCloseHandle.Call(uintptr(l.handle))
		l.handle = 0
	}
}
