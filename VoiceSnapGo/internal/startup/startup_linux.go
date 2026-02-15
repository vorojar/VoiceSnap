//go:build linux

package startup

import (
	"fmt"
	"os"
	"path/filepath"
)

const desktopEntry = `[Desktop Entry]
Type=Application
Name=VoiceSnap
Exec=%s
Hidden=false
X-GNOME-Autostart-enabled=true
`

func autostartPath() string {
	configDir, err := os.UserConfigDir()
	if err != nil {
		home, _ := os.UserHomeDir()
		configDir = filepath.Join(home, ".config")
	}
	return filepath.Join(configDir, "autostart", "voicesnap.desktop")
}

func isEnabled() bool {
	_, err := os.Stat(autostartPath())
	return err == nil
}

func setEnabled(enable bool) error {
	path := autostartPath()

	if !enable {
		return os.Remove(path)
	}

	exe, err := os.Executable()
	if err != nil {
		return err
	}

	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0755); err != nil {
		return err
	}

	content := fmt.Sprintf(desktopEntry, exe)
	return os.WriteFile(path, []byte(content), 0644)
}
