package input

// Paster provides clipboard and keyboard simulation functionality.
type Paster interface {
	// Paste writes text to clipboard and simulates Ctrl+V (Cmd+V on macOS).
	Paste(text string) error
	// TypeText simulates typing each character via Unicode input events (fallback).
	TypeText(text string) error
}

// NewPaster creates a new platform-specific paster.
func NewPaster() Paster {
	return newPlatformPaster()
}
