package history

import (
	"encoding/json"
	"os"
	"path/filepath"
	"sync"
	"time"
	"voicesnap/internal/logger"
)

const maxEntries = 50

// Entry represents a single recognition history item.
type Entry struct {
	Text      string `json:"text"`
	Timestamp int64  `json:"timestamp"` // Unix milliseconds
}

// Store manages recognition history with JSON file persistence.
type Store struct {
	mu             sync.Mutex
	entries        []Entry
	retentionDays  int
	path           string
}

type fileData struct {
	RetentionDays *int    `json:"retentionDays"`
	Entries       []Entry `json:"entries"`
}

// New creates a new history store, loading existing data from disk.
func New() *Store {
	s := &Store{
		retentionDays: 30,
		path:          historyPath(),
	}
	s.load()
	s.prune()
	return s
}

// Add inserts a new entry at the top and persists to disk.
func (s *Store) Add(text string) {
	s.mu.Lock()
	defer s.mu.Unlock()

	entry := Entry{
		Text:      text,
		Timestamp: time.Now().UnixMilli(),
	}

	// Prepend
	s.entries = append([]Entry{entry}, s.entries...)

	// Cap at max
	if len(s.entries) > maxEntries {
		s.entries = s.entries[:maxEntries]
	}

	s.save()
}

// GetAll returns all history entries (newest first).
func (s *Store) GetAll() []Entry {
	s.mu.Lock()
	defer s.mu.Unlock()

	result := make([]Entry, len(s.entries))
	copy(result, s.entries)
	return result
}

// Delete removes an entry by its timestamp.
func (s *Store) Delete(timestamp int64) {
	s.mu.Lock()
	defer s.mu.Unlock()

	for i, e := range s.entries {
		if e.Timestamp == timestamp {
			s.entries = append(s.entries[:i], s.entries[i+1:]...)
			s.save()
			return
		}
	}
}

// ClearAll removes all entries.
func (s *Store) ClearAll() {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.entries = nil
	s.save()
}

// GetRetentionDays returns the current retention period.
func (s *Store) GetRetentionDays() int {
	s.mu.Lock()
	defer s.mu.Unlock()
	return s.retentionDays
}

// SetRetentionDays sets the retention period and prunes old entries.
func (s *Store) SetRetentionDays(days int) {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.retentionDays = days
	s.pruneUnlocked()
	s.save()
}

// prune removes entries older than the retention period.
func (s *Store) prune() {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.pruneUnlocked()
}

func (s *Store) pruneUnlocked() {
	if s.retentionDays <= 0 {
		return // 0 = keep forever
	}

	cutoff := time.Now().Add(-time.Duration(s.retentionDays) * 24 * time.Hour).UnixMilli()
	kept := s.entries[:0]
	for _, e := range s.entries {
		if e.Timestamp >= cutoff {
			kept = append(kept, e)
		}
	}
	s.entries = kept
}

func (s *Store) load() {
	data, err := os.ReadFile(s.path)
	if err != nil {
		return
	}

	var fd fileData
	if err := json.Unmarshal(data, &fd); err != nil {
		logger.Error("Failed to parse history: %v", err)
		return
	}

	s.entries = fd.Entries
	if fd.RetentionDays != nil {
		s.retentionDays = *fd.RetentionDays
	}
}

func (s *Store) save() {
	days := s.retentionDays
	fd := fileData{
		RetentionDays: &days,
		Entries:       s.entries,
	}

	data, err := json.MarshalIndent(fd, "", "  ")
	if err != nil {
		logger.Error("Failed to marshal history: %v", err)
		return
	}

	if err := os.WriteFile(s.path, data, 0644); err != nil {
		logger.Error("Failed to save history: %v", err)
	}
}

func historyPath() string {
	exe, err := os.Executable()
	if err != nil {
		return "history.json"
	}
	return filepath.Join(filepath.Dir(exe), "history.json")
}
