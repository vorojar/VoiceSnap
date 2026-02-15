package logger

import (
	"fmt"
	"os"
	"path/filepath"
	"sync"
	"time"
)

var (
	mu      sync.Mutex
	logFile *os.File
)

// Init initializes the file logger. Log file is created next to the executable.
func Init() {
	exe, err := os.Executable()
	if err != nil {
		exe = "."
	}
	dir := filepath.Dir(exe)
	path := filepath.Join(dir, "app.log")

	f, err := os.OpenFile(path, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0644)
	if err != nil {
		fmt.Fprintf(os.Stderr, "failed to open log file: %v\n", err)
		return
	}
	logFile = f
}

func write(level, format string, args ...interface{}) {
	msg := fmt.Sprintf(format, args...)
	ts := time.Now().Format("15:04:05")
	line := fmt.Sprintf("[%s] %s %s\n", ts, level, msg)

	mu.Lock()
	defer mu.Unlock()

	if logFile != nil {
		logFile.WriteString(line)
	}
	fmt.Fprint(os.Stderr, line)
}

// Info logs an informational message.
func Info(format string, args ...interface{}) {
	write("INFO", format, args...)
}

// Error logs an error message.
func Error(format string, args ...interface{}) {
	write("ERROR", format, args...)
}

// Close closes the log file.
func Close() {
	mu.Lock()
	defer mu.Unlock()
	if logFile != nil {
		logFile.Close()
		logFile = nil
	}
}
