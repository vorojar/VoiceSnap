package main

import "syscall"

func isChinese() bool {
	kernel32 := syscall.NewLazyDLL("kernel32.dll")
	proc := kernel32.NewProc("GetUserDefaultUILanguage")
	langID, _, _ := proc.Call()
	return (langID & 0x3FF) == 0x04 // LANG_CHINESE
}
