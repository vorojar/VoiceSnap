//go:build darwin

package startup

import (
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"text/template"
)

const plistTemplate = `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.voicesnap.app</string>
    <key>ProgramArguments</key>
    <array>
        <string>{{.ExePath}}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>
`

func plistPath() string {
	home, _ := os.UserHomeDir()
	return filepath.Join(home, "Library", "LaunchAgents", "com.voicesnap.app.plist")
}

func isEnabled() bool {
	_, err := os.Stat(plistPath())
	return err == nil
}

func setEnabled(enable bool) error {
	path := plistPath()

	if !enable {
		exec.Command("launchctl", "unload", path).Run()
		return os.Remove(path)
	}

	exe, err := os.Executable()
	if err != nil {
		return err
	}

	dir := filepath.Dir(path)
	os.MkdirAll(dir, 0755)

	f, err := os.Create(path)
	if err != nil {
		return fmt.Errorf("failed to create plist: %w", err)
	}
	defer f.Close()

	tmpl := template.Must(template.New("plist").Parse(plistTemplate))
	if err := tmpl.Execute(f, struct{ ExePath string }{exe}); err != nil {
		return err
	}

	return exec.Command("launchctl", "load", path).Run()
}
