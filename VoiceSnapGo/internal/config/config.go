package config

import (
	"encoding/json"
	"os"
	"path/filepath"
	"voicesnap/internal/logger"
)

// Config holds the application configuration, compatible with the WPF version's config.json.
type Config struct {
	HotkeyVK                int    `json:"HotkeyVK"`
	AutoHide                bool   `json:"AutoHide"`
	SoundFeedback           bool   `json:"SoundFeedback"`
	DeviceName              string `json:"DeviceName,omitempty"`
	IndicatorX              int    `json:"IndicatorX,omitempty"`
	IndicatorY              int    `json:"IndicatorY,omitempty"`
	ModelDownloadUrl        string `json:"ModelDownloadUrl"`
	FallbackModelDownloadUrl string `json:"FallbackModelDownloadUrl"`
}

// Default returns a default configuration.
func Default() *Config {
	return &Config{
		HotkeyVK:                0xA3, // Right Ctrl
		AutoHide:                true,
		SoundFeedback:           true,
		ModelDownloadUrl:        "http://www.maikami.com/voicesnap/sensevoice.zip",
		FallbackModelDownloadUrl: "https://modelscope.cn/models/sherpa-onnx/sherpa-onnx-sense-voice-zh-en-ja-ko-yue/resolve/master/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17.tar.bz2",
	}
}

func configPath() string {
	exe, err := os.Executable()
	if err != nil {
		return "config.json"
	}
	return filepath.Join(filepath.Dir(exe), "config.json")
}

// Load reads the config from disk. Returns default config on error.
func Load() (*Config, error) {
	path := configPath()
	data, err := os.ReadFile(path)
	if err != nil {
		if os.IsNotExist(err) {
			cfg := Default()
			Save(cfg)
			return cfg, nil
		}
		return nil, err
	}

	cfg := Default()
	if err := json.Unmarshal(data, cfg); err != nil {
		return nil, err
	}
	return cfg, nil
}

// Save writes the config to disk.
func Save(cfg *Config) {
	data, err := json.MarshalIndent(cfg, "", "  ")
	if err != nil {
		logger.Error("Failed to marshal config: %v", err)
		return
	}
	if err := os.WriteFile(configPath(), data, 0644); err != nil {
		logger.Error("Failed to save config: %v", err)
	}
}
