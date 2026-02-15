package main

import (
	"os"
	"voicesnap/internal/logger"
	"voicesnap/internal/singleinstance"
)

func main() {
	// Initialize logger
	logger.Init()
	logger.Info("VoiceSnap starting...")

	// Single instance check
	lock, err := singleinstance.Acquire()
	if err != nil {
		logger.Info("Another instance is already running, exiting")
		os.Exit(0)
	}
	defer lock.Release()

	// Start the Wails application
	if err := RunApp(); err != nil {
		logger.Error("Application error: %v", err)
		os.Exit(1)
	}
}
