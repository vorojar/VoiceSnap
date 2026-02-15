package services

import (
	"voicesnap/internal/config"
	"voicesnap/internal/startup"
)

// ConfigService provides configuration read/write methods to the frontend.
type ConfigService struct {
	cfg *config.Config
}

func NewConfigService(cfg *config.Config) *ConfigService {
	return &ConfigService{cfg: cfg}
}

// GetConfig returns the current configuration.
func (s *ConfigService) GetConfig() *config.Config {
	return s.cfg
}

// SetAutoHide sets the auto-hide indicator preference.
func (s *ConfigService) SetAutoHide(enabled bool) {
	s.cfg.AutoHide = enabled
	config.Save(s.cfg)
}

// GetAutoHide returns the auto-hide preference.
func (s *ConfigService) GetAutoHide() bool {
	return s.cfg.AutoHide
}

// IsStartupEnabled returns whether the app is set to start at login.
func (s *ConfigService) IsStartupEnabled() bool {
	return startup.IsEnabled()
}

// SetStartupEnabled enables or disables start at login.
func (s *ConfigService) SetStartupEnabled(enabled bool) error {
	return startup.SetEnabled(enabled)
}
