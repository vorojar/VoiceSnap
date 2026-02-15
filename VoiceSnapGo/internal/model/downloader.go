package model

import (
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"strings"

	"voicesnap/internal/logger"
)

// ProgressCallback is called with download progress (percent, downloaded bytes, total bytes).
type ProgressCallback func(percent float64, downloaded, total int64)

// Download downloads and extracts the ASR model.
// It tries the primary URL first, then falls back to the fallback URL.
func Download(primaryURL, fallbackURL, modelsDir string, progress ProgressCallback) error {
	if err := os.MkdirAll(modelsDir, 0755); err != nil {
		return fmt.Errorf("failed to create models dir: %w", err)
	}

	tempFile := filepath.Join(modelsDir, "model_package.tar.bz2")

	// Try primary URL first
	logger.Info("Downloading model from primary URL: %s", primaryURL)
	err := downloadFile(primaryURL, tempFile, progress)
	if err != nil {
		logger.Info("Primary URL failed: %v, trying fallback", err)
		err = downloadFile(fallbackURL, tempFile, progress)
		if err != nil {
			return fmt.Errorf("download failed from both URLs: %w", err)
		}
	}

	// Extract
	logger.Info("Extracting model archive...")
	if err := extractModel(tempFile, modelsDir); err != nil {
		return fmt.Errorf("extraction failed: %w", err)
	}

	// Cleanup temp file
	os.Remove(tempFile)

	logger.Info("Model download and extraction complete")
	return nil
}

func downloadFile(url, destPath string, progress ProgressCallback) error {
	resp, err := http.Get(url)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return fmt.Errorf("HTTP %d: %s", resp.StatusCode, resp.Status)
	}

	totalBytes := resp.ContentLength
	out, err := os.Create(destPath)
	if err != nil {
		return err
	}
	defer out.Close()

	buf := make([]byte, 32*1024)
	var downloaded int64
	lastPercent := -1.0

	for {
		n, readErr := resp.Body.Read(buf)
		if n > 0 {
			if _, writeErr := out.Write(buf[:n]); writeErr != nil {
				return writeErr
			}
			downloaded += int64(n)

			if totalBytes > 0 && progress != nil {
				pct := float64(downloaded) / float64(totalBytes) * 100
				if pct-lastPercent >= 0.5 {
					lastPercent = pct
					progress(pct, downloaded, totalBytes)
				}
			}
		}
		if readErr != nil {
			if readErr == io.EOF {
				break
			}
			return readErr
		}
	}

	return nil
}

func extractModel(archivePath, destDir string) error {
	// Use system tar to extract
	cmd := exec.Command("tar", "-xf", archivePath, "-C", destDir)
	output, err := cmd.CombinedOutput()
	if err != nil {
		return fmt.Errorf("tar extraction failed: %v, output: %s", err, string(output))
	}

	// Rename extracted directory to "sensevoice"
	entries, err := os.ReadDir(destDir)
	if err != nil {
		return err
	}

	for _, entry := range entries {
		if entry.IsDir() && strings.HasPrefix(entry.Name(), "sherpa-onnx-sense-voice") {
			targetDir := filepath.Join(destDir, "sensevoice")
			srcDir := filepath.Join(destDir, entry.Name())

			// Remove existing target
			os.RemoveAll(targetDir)

			if err := os.Rename(srcDir, targetDir); err != nil {
				return fmt.Errorf("failed to rename model dir: %w", err)
			}
			break
		}
	}

	return nil
}
