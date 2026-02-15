//go:build linux

package input

import (
	"fmt"
	"os/exec"
)

type linuxPaster struct{}

func newPlatformPaster() Paster {
	return &linuxPaster{}
}

func (p *linuxPaster) Paste(text string) error {
	// Write to clipboard using xclip
	cmd := exec.Command("xclip", "-selection", "clipboard")
	cmd.Stdin = stringReader(text)
	if err := cmd.Run(); err != nil {
		// Fallback to xsel
		cmd = exec.Command("xsel", "--clipboard", "--input")
		cmd.Stdin = stringReader(text)
		if err := cmd.Run(); err != nil {
			return fmt.Errorf("clipboard write failed (tried xclip and xsel): %v", err)
		}
	}

	// Simulate Ctrl+V using xdotool
	cmd = exec.Command("xdotool", "key", "--clearmodifiers", "ctrl+v")
	if err := cmd.Run(); err != nil {
		return fmt.Errorf("xdotool key ctrl+v failed: %v", err)
	}

	return nil
}

func (p *linuxPaster) TypeText(text string) error {
	// Use xdotool to type text directly
	cmd := exec.Command("xdotool", "type", "--clearmodifiers", "--", text)
	if err := cmd.Run(); err != nil {
		return fmt.Errorf("xdotool type failed: %v", err)
	}
	return nil
}

type stringReaderImpl struct {
	s string
	i int
}

func stringReader(s string) *stringReaderImpl {
	return &stringReaderImpl{s: s}
}

func (r *stringReaderImpl) Read(p []byte) (n int, err error) {
	if r.i >= len(r.s) {
		return 0, fmt.Errorf("EOF")
	}
	n = copy(p, r.s[r.i:])
	r.i += n
	return n, nil
}
