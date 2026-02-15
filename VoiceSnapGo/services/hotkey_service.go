package services

import (
	"voicesnap/internal/config"
	"voicesnap/internal/hotkey"
)

// HotkeyService provides hotkey configuration methods to the frontend.
type HotkeyService struct {
	cfg *config.Config
	app AppOrchestrator
}

func NewHotkeyService(cfg *config.Config) *HotkeyService {
	return &HotkeyService{cfg: cfg}
}

// SetApp sets the app orchestrator reference.
func (s *HotkeyService) SetApp(app AppOrchestrator) {
	s.app = app
}

// GetCurrentHotkey returns the current hotkey VK code.
func (s *HotkeyService) GetCurrentHotkey() int {
	return s.cfg.HotkeyVK
}

// GetHotkeyName returns the display name for the current hotkey.
func (s *HotkeyService) GetHotkeyName() string {
	return hotkey.GetKeyName(s.cfg.HotkeyVK)
}

// SetHotkey updates the hotkey VK code.
func (s *HotkeyService) SetHotkey(vk int) {
	s.cfg.HotkeyVK = vk
	config.Save(s.cfg)
	if s.app != nil {
		s.app.UpdateHotkeyVK(vk)
	}
}

// StartRecordingHotkey enters hotkey recording mode (pauses hotkey polling).
func (s *HotkeyService) StartRecordingHotkey() {
	if s.app != nil {
		s.app.SetRecordingHotkey(true)
	}
}

// StopRecordingHotkey exits hotkey recording mode.
func (s *HotkeyService) StopRecordingHotkey() {
	if s.app != nil {
		s.app.SetRecordingHotkey(false)
	}
}

// GetKeyName returns the display name for a given VK code.
func (s *HotkeyService) GetKeyName(vk int) string {
	return hotkey.GetKeyName(vk)
}
