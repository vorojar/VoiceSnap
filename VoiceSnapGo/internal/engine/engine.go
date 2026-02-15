package engine

import (
	"fmt"
	"os"
	"path/filepath"
	"voicesnap/internal/logger"
)

// Engine is the interface for ASR engines.
type Engine interface {
	// Recognize takes float32 PCM samples (16kHz mono) and returns the recognized text.
	Recognize(samples []float32) (string, error)
	// HardwareInfo returns a human-readable description of the hardware backend being used.
	HardwareInfo() string
	// Close releases engine resources.
	Close()
}

// ModelDir returns the path to the sensevoice model directory.
func ModelDir() string {
	exe, err := os.Executable()
	if err != nil {
		return filepath.Join(".", "models", "sensevoice")
	}
	return filepath.Join(filepath.Dir(exe), "models", "sensevoice")
}

// ModelPath returns the path to the ONNX model file (prefers int8).
func ModelPath() string {
	dir := ModelDir()
	int8Path := filepath.Join(dir, "model.int8.onnx")
	if _, err := os.Stat(int8Path); err == nil {
		return int8Path
	}
	return filepath.Join(dir, "model.onnx")
}

// TokensPath returns the path to the tokens.txt file.
func TokensPath() string {
	return filepath.Join(ModelDir(), "tokens.txt")
}

// ModelExists checks if both the model and tokens files exist.
func ModelExists() bool {
	if _, err := os.Stat(ModelPath()); err != nil {
		return false
	}
	if _, err := os.Stat(TokensPath()); err != nil {
		return false
	}
	return true
}

// New creates a new platform-specific ASR engine.
// Returns an error if the model files are not found.
func New() (Engine, error) {
	if !ModelExists() {
		return nil, fmt.Errorf("model files not found in %s", ModelDir())
	}

	logger.Info("Loading model from %s", ModelPath())
	return newPlatformEngine()
}
