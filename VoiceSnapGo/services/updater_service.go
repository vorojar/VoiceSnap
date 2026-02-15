package services

import (
	"voicesnap/internal/updater"
)

// UpdaterService provides update checking and downloading to the frontend.
type UpdaterService struct {
	currentVersion string
}

func NewUpdaterService(version string) *UpdaterService {
	return &UpdaterService{currentVersion: version}
}

// CheckForUpdate checks if a newer version is available.
// Returns version info if an update is available, nil otherwise.
func (s *UpdaterService) CheckForUpdate() (*updater.VersionInfo, error) {
	info, err := updater.CheckForUpdate()
	if err != nil {
		return nil, err
	}

	if updater.IsNewer(info.Version, s.currentVersion) {
		return info, nil
	}

	return nil, nil
}

// GetCurrentVersion returns the current application version.
func (s *UpdaterService) GetCurrentVersion() string {
	return s.currentVersion
}

// PerformUpdate downloads and applies the update.
func (s *UpdaterService) PerformUpdate(downloadURL string) error {
	return updater.PerformUpdate(downloadURL, nil)
}
