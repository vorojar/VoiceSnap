package services

import (
	"voicesnap/internal/config"
)

// AppOrchestrator is the interface for app-level orchestration callbacks.
type AppOrchestrator interface {
	SetRecordingHotkey(recording bool)
	UpdateHotkeyVK(vk int)
}

// AppService provides application lifecycle methods to the frontend.
type AppService struct {
	cfg     *config.Config
	version string
	app     AppOrchestrator
}

func NewAppService(cfg *config.Config, version string) *AppService {
	return &AppService{cfg: cfg, version: version}
}

// SetApp sets the orchestrator reference (called after creation).
func (s *AppService) SetApp(app AppOrchestrator) {
	s.app = app
}

// GetVersion returns the application version string.
func (s *AppService) GetVersion() string {
	return s.version
}

// GetAppName returns the application name.
func (s *AppService) GetAppName() string {
	return "VoiceSnap"
}
