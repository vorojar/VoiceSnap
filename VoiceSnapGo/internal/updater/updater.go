package updater

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"time"

	"voicesnap/internal/logger"
)

const versionCheckURL = "http://www.maikami.com/voicesnap/version.json"

// VersionInfo represents remote version metadata.
type VersionInfo struct {
	Version      string `json:"version"`
	DownloadURL  string `json:"downloadUrl"`
	ReleaseNotes string `json:"releaseNotes"`
}

// CheckForUpdate fetches version info from the server.
func CheckForUpdate() (*VersionInfo, error) {
	client := &http.Client{Timeout: 10 * time.Second}
	resp, err := client.Get(versionCheckURL)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	data, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}

	var info VersionInfo
	if err := json.Unmarshal(data, &info); err != nil {
		return nil, err
	}

	if info.Version == "" {
		return nil, fmt.Errorf("empty version in response")
	}

	return &info, nil
}

// IsNewer returns true if remote version is newer than current.
func IsNewer(remoteVersion, currentVersion string) bool {
	return compareVersions(remoteVersion, currentVersion) > 0
}

// PerformUpdate downloads the new version and replaces the current executable.
func PerformUpdate(downloadURL string, progress func(percent int)) error {
	currentExe, err := os.Executable()
	if err != nil {
		return fmt.Errorf("cannot determine executable path: %w", err)
	}

	tempExe := filepath.Join(os.TempDir(), "VoiceSnap_new.exe")
	oldExe := currentExe + ".old"

	// Download new version
	logger.Info("Downloading update from %s", downloadURL)
	if err := downloadWithProgress(downloadURL, tempExe, progress); err != nil {
		return fmt.Errorf("download failed: %w", err)
	}

	// Verify download
	info, err := os.Stat(tempExe)
	if err != nil || info.Size() < 100*1024 {
		os.Remove(tempExe)
		return fmt.Errorf("downloaded file is too small or missing")
	}

	// Replace current exe
	os.Remove(oldExe)
	if err := os.Rename(currentExe, oldExe); err != nil {
		return fmt.Errorf("failed to rename current exe: %w", err)
	}

	if err := copyFile(tempExe, currentExe); err != nil {
		// Rollback
		os.Rename(oldExe, currentExe)
		return fmt.Errorf("failed to copy new exe: %w", err)
	}

	os.Remove(tempExe)

	// Launch new version
	cmd := exec.Command(currentExe)
	cmd.Start()

	return nil
}

// CleanupOldVersion removes the .old backup if it exists.
func CleanupOldVersion() {
	exe, err := os.Executable()
	if err != nil {
		return
	}
	oldExe := exe + ".old"
	if _, err := os.Stat(oldExe); err == nil {
		time.Sleep(2 * time.Second)
		if err := os.Remove(oldExe); err != nil {
			logger.Error("Failed to cleanup old version: %v", err)
		} else {
			logger.Info("Cleaned up old version file")
		}
	}
}

func downloadWithProgress(url, destPath string, progress func(percent int)) error {
	resp, err := http.Get(url)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return fmt.Errorf("HTTP %d", resp.StatusCode)
	}

	total := resp.ContentLength
	out, err := os.Create(destPath)
	if err != nil {
		return err
	}
	defer out.Close()

	buf := make([]byte, 32*1024)
	var downloaded int64

	for {
		n, readErr := resp.Body.Read(buf)
		if n > 0 {
			out.Write(buf[:n])
			downloaded += int64(n)
			if total > 0 && progress != nil {
				progress(int(downloaded * 100 / total))
			}
		}
		if readErr == io.EOF {
			break
		}
		if readErr != nil {
			return readErr
		}
	}

	return nil
}

func copyFile(src, dst string) error {
	in, err := os.Open(src)
	if err != nil {
		return err
	}
	defer in.Close()

	out, err := os.Create(dst)
	if err != nil {
		return err
	}
	defer out.Close()

	_, err = io.Copy(out, in)
	return err
}

func compareVersions(v1, v2 string) int {
	parts1 := strings.Split(v1, ".")
	parts2 := strings.Split(v2, ".")

	maxLen := len(parts1)
	if len(parts2) > maxLen {
		maxLen = len(parts2)
	}

	for i := 0; i < maxLen; i++ {
		var p1, p2 int
		if i < len(parts1) {
			p1, _ = strconv.Atoi(parts1[i])
		}
		if i < len(parts2) {
			p2, _ = strconv.Atoi(parts2[i])
		}
		if p1 != p2 {
			if p1 > p2 {
				return 1
			}
			return -1
		}
	}
	return 0
}
