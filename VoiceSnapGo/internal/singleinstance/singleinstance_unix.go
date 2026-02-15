//go:build !windows

package singleinstance

import (
	"fmt"
	"os"
	"path/filepath"
	"syscall"
)

type unixLock struct {
	file *os.File
}

// Acquire tries to acquire a file lock. Returns error if another instance holds it.
func Acquire() (Lock, error) {
	lockPath := filepath.Join(os.TempDir(), "voicesnap.lock")
	f, err := os.OpenFile(lockPath, os.O_CREATE|os.O_RDWR, 0600)
	if err != nil {
		return nil, err
	}

	err = syscall.Flock(int(f.Fd()), syscall.LOCK_EX|syscall.LOCK_NB)
	if err != nil {
		f.Close()
		return nil, fmt.Errorf("another instance is already running")
	}

	return &unixLock{file: f}, nil
}

func (l *unixLock) Release() {
	if l.file != nil {
		syscall.Flock(int(l.file.Fd()), syscall.LOCK_UN)
		l.file.Close()
		l.file = nil
	}
}
