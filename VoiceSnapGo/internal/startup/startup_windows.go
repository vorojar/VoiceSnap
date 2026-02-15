//go:build windows

package startup

import (
	"fmt"
	"os"

	"golang.org/x/sys/windows/registry"
)

const registryKey = `SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
const appName = "VoiceSnap"

func isEnabled() bool {
	key, err := registry.OpenKey(registry.CURRENT_USER, registryKey, registry.QUERY_VALUE)
	if err != nil {
		return false
	}
	defer key.Close()

	val, _, err := key.GetStringValue(appName)
	return err == nil && val != ""
}

func setEnabled(enable bool) error {
	key, err := registry.OpenKey(registry.CURRENT_USER, registryKey, registry.SET_VALUE)
	if err != nil {
		return fmt.Errorf("failed to open registry key: %w", err)
	}
	defer key.Close()

	if enable {
		exe, err := os.Executable()
		if err != nil {
			return err
		}
		return key.SetStringValue(appName, fmt.Sprintf(`"%s"`, exe))
	}

	return key.DeleteValue(appName)
}
