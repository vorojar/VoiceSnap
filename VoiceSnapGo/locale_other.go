//go:build !windows

package main

import (
	"os"
	"strings"
)

func isChinese() bool {
	for _, env := range []string{"LANG", "LC_ALL", "LC_MESSAGES"} {
		if v := os.Getenv(env); strings.Contains(v, "zh") {
			return true
		}
	}
	return false
}
