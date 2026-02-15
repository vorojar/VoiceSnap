package services

import (
	"voicesnap/internal/audio"
)

// AudioService provides audio device information to the frontend.
type AudioService struct {
	recorder *audio.Recorder
}

func NewAudioService(recorder *audio.Recorder) *AudioService {
	return &AudioService{recorder: recorder}
}

// GetDeviceName returns the name of the current input device.
func (s *AudioService) GetDeviceName() string {
	if s.recorder == nil {
		return "Default"
	}
	return s.recorder.GetDeviceName()
}
