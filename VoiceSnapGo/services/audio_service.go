package services

import (
	"voicesnap/internal/audio"
	"voicesnap/internal/config"
)

// AudioService provides audio device information to the frontend.
type AudioService struct {
	recorder *audio.Recorder
	cfg      *config.Config
}

func NewAudioService(recorder *audio.Recorder, cfg *config.Config) *AudioService {
	// Apply saved device preference
	if cfg.DeviceName != "" {
		recorder.SetPreferredDevice(cfg.DeviceName)
	}
	return &AudioService{recorder: recorder, cfg: cfg}
}

// GetDeviceName returns the name of the current input device.
func (s *AudioService) GetDeviceName() string {
	if s.recorder == nil {
		return "Default"
	}
	if s.cfg.DeviceName != "" {
		return s.cfg.DeviceName
	}
	return s.recorder.GetDeviceName()
}

// ListInputDevices returns all available audio input devices.
func (s *AudioService) ListInputDevices() []audio.InputDevice {
	if s.recorder == nil {
		return nil
	}
	return s.recorder.ListInputDevices()
}

// SetDevice sets the preferred audio input device by name.
func (s *AudioService) SetDevice(name string) {
	if s.recorder != nil {
		s.recorder.SetPreferredDevice(name)
	}
	s.cfg.DeviceName = name
	config.Save(s.cfg)
}
