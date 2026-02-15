package main

import (
	"context"
	"sync"
	"sync/atomic"
	"time"

	"voicesnap/internal/audio"
	"voicesnap/internal/config"
	"voicesnap/internal/engine"
	"voicesnap/internal/hotkey"
	"voicesnap/internal/input"
	"voicesnap/internal/logger"
	"voicesnap/internal/overlay"
	"voicesnap/internal/sound"
	"voicesnap/services"

	"github.com/wailsapp/wails/v3/pkg/application"
	"github.com/wailsapp/wails/v3/pkg/events"
)

const (
	appVersion = "2.0.0"
	appName    = "VoiceSnap"
)

// App holds all application state and orchestration logic.
type App struct {
	ctx    context.Context
	cancel context.CancelFunc
	wg     sync.WaitGroup

	cfg      *config.Config
	recorder *audio.Recorder
	eng      engine.Engine
	hk       hotkey.Listener
	paster   input.Paster

	wailsApp       *application.App
	settingsWindow *application.WebviewWindow
	indicator      overlay.Overlay

	// State
	mu                sync.Mutex
	isRecording       bool
	isFreetalking     bool
	hotkeyActive      bool
	hotkeyPressTime   time.Time
	isCombination     bool
	isRecordingHotkey bool
	hideGen           atomic.Uint64
	lastStopTime      time.Time
}

func RunApp() error {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	app := &App{
		ctx:    ctx,
		cancel: cancel,
	}

	// Load config
	cfg, err := config.Load()
	if err != nil {
		logger.Error("Failed to load config: %v", err)
		cfg = config.Default()
	}
	app.cfg = cfg

	// Initialize audio recorder
	app.recorder = audio.NewRecorder()

	// Initialize platform-specific hotkey listener
	app.hk = hotkey.New()

	// Initialize platform-specific paster
	app.paster = input.NewPaster()

	// Create services for Wails bindings
	appService := services.NewAppService(app.cfg, appVersion)
	configService := services.NewConfigService(app.cfg)
	engineService := services.NewEngineService()
	hotkeyService := services.NewHotkeyService(app.cfg)
	updaterService := services.NewUpdaterService(appVersion)
	audioService := services.NewAudioService(app.recorder, app.cfg)

	// Create Wails application
	wailsApp := application.New(application.Options{
		Name: appName,
		Icon: appIcon,
		Services: []application.Service{
			application.NewService(appService),
			application.NewService(configService),
			application.NewService(engineService),
			application.NewService(hotkeyService),
			application.NewService(updaterService),
			application.NewService(audioService),
		},
		Assets: application.AssetOptions{
			Handler: application.AssetFileServerFS(assets),
		},
		OnShutdown: func() {
			app.cleanup()
		},
	})

	app.wailsApp = wailsApp

	// Create settings window (visible on startup, close → hide to tray)
	app.settingsWindow = wailsApp.Window.NewWithOptions(application.WebviewWindowOptions{
		Name:             "settings",
		Title:            "VoiceSnap",
		Width:            820,
		Height:           580,
		URL:              "/",
		Hidden:           false,
		BackgroundColour: application.NewRGB(242, 242, 247), // #F2F2F7 Apple grouped bg
		Windows: application.WindowsWindow{
			Theme: application.SystemDefault,
		},
	})

	// Intercept window close: hide to tray instead of closing
	app.settingsWindow.RegisterHook(events.Common.WindowClosing, func(e *application.WindowEvent) {
		e.Cancel()
		app.settingsWindow.Hide()
	})

	// Create native overlay indicator (Win32 layered window, per-pixel alpha)
	app.indicator = overlay.New()

	// Create system tray
	tray := wailsApp.SystemTray.New()
	tray.SetLabel(appName)
	tray.SetIcon(appIcon)

	showLabel, exitLabel := "Show Settings", "Exit"
	if isChinese() {
		showLabel, exitLabel = "显示设置", "退出"
	}

	trayMenu := wailsApp.Menu.New()
	trayMenu.Add(showLabel).OnClick(func(data *application.Context) {
		app.showSettings()
	})
	trayMenu.AddSeparator()
	trayMenu.Add(exitLabel).OnClick(func(data *application.Context) {
		app.cleanup()
		wailsApp.Quit()
	})
	tray.SetMenu(trayMenu)

	tray.OnClick(func() {
		app.toggleSettings()
	})

	// Start background goroutines
	app.wg.Add(1)
	go app.hotkeyLoop()

	// Initialize engine asynchronously
	go app.initEngine()

	// Volume callback → native overlay
	app.recorder.OnVolume(func(vol float64) {
		app.indicator.SetVolume(vol)
	})

	// Device change callback
	app.recorder.OnDeviceChange(func(name string) {
		wailsApp.Event.Emit("device:changed", map[string]string{"name": name})
	})

	// Store services references for orchestration callbacks
	appService.SetApp(app)

	logger.Info("Application initialized, starting Wails")
	return wailsApp.Run()
}

// hotkeyLoop polls the hotkey state at 30ms intervals.
func (a *App) hotkeyLoop() {
	defer a.wg.Done()

	ticker := time.NewTicker(30 * time.Millisecond)
	defer ticker.Stop()

	for {
		select {
		case <-a.ctx.Done():
			return
		case <-ticker.C:
			a.pollHotkey()
		}
	}
}

func (a *App) pollHotkey() {
	a.mu.Lock()
	defer a.mu.Unlock()

	if a.eng == nil || a.isRecordingHotkey {
		return
	}

	// Escape cancels any active recording
	if a.cfg.HotkeyVK != 0x1B && (a.isRecording || a.isFreetalking) && a.hk.IsKeyDown(0x1B) {
		a.isFreetalking = false
		a.isRecording = false
		a.lastStopTime = time.Now()
		a.recorder.Stop()
		logger.Info("Recording cancelled (Escape)")
		a.indicator.SetStatus(overlay.StatusCancelled, "已取消")
		if a.cfg.SoundFeedback {
			sound.PlayCancel()
		}
		a.delayedHide(1000)
		return
	}

	isDown := a.hk.IsKeyDown(a.cfg.HotkeyVK)

	if isDown {
		if !a.hotkeyActive {
			// Key just pressed
			a.hotkeyActive = true
			a.isCombination = false
			a.hotkeyPressTime = time.Now()

			// If in free talk mode, stop on next press
			if a.isFreetalking {
				a.stopFreetalkLocked()
				return
			}
		} else {
			// Key held down - check for combination keys
			if !a.isCombination && a.hk.IsAnyOtherKeyPressed(a.cfg.HotkeyVK) {
				a.isCombination = true
				if a.isRecording {
					a.stopRecordingLocked(true)
				}
			}

			// If held > 300ms without combo, start hold-to-talk recording
			if !a.isRecording && !a.isCombination && time.Since(a.hotkeyPressTime) > 300*time.Millisecond && time.Since(a.lastStopTime) > 500*time.Millisecond {
				a.startRecordingLocked()
			}
		}
	} else if a.hotkeyActive {
		// Key released
		a.hotkeyActive = false

		if a.isRecording {
			// Hold-to-talk: release stops recording
			a.stopRecordingLocked(a.isCombination)
		} else if !a.isCombination && time.Since(a.hotkeyPressTime) < 300*time.Millisecond && time.Since(a.lastStopTime) > 500*time.Millisecond {
			// Short tap (<300ms): start free talk mode
			a.startFreetalkLocked()
		}
	}
}

func (a *App) startRecordingLocked() {
	if a.isRecording {
		return
	}
	a.isRecording = true
	a.hideGen.Add(1) // cancel any pending delayed hide

	logger.Info("Recording started")
	a.positionIndicatorCenter()
	a.indicator.SetStatus(overlay.StatusRecording, "")
	a.indicator.Show()
	if a.cfg.SoundFeedback {
		sound.PlayStart()
	}

	if err := a.recorder.Start(); err != nil {
		logger.Error("Failed to start recording: %v", err)
		a.isRecording = false
		return
	}
}

func (a *App) stopRecordingLocked(cancel bool) {
	if !a.isRecording {
		return
	}
	a.isRecording = false
	a.lastStopTime = time.Now()

	if cancel {
		a.recorder.Stop()
		logger.Info("Recording cancelled (combination key)")
		a.indicator.SetStatus(overlay.StatusCancelled, "已取消")
		if a.cfg.SoundFeedback {
			sound.PlayCancel()
		}
		a.delayedHide(1000)
		return
	}

	hasVoice := a.recorder.HasVoiceActivity()
	samples := a.recorder.StopAndGetSamples()

	a.indicator.SetStatus(overlay.StatusProcessing, "识别中")

	// Run recognition in background
	go func() {
		if !hasVoice {
			logger.Info("No voice activity detected")
			a.indicator.SetStatus(overlay.StatusNoVoice, "无语音")
			a.delayedHide(1500)
			return
		}

		if samples == nil || len(samples) == 0 {
			a.indicator.SetStatus(overlay.StatusNoContent, "无内容")
			a.delayedHide(1500)
			return
		}

		text, err := a.eng.Recognize(samples)
		if err != nil {
			logger.Error("Recognition failed: %v", err)
			a.indicator.SetStatus(overlay.StatusError, "错误")
			a.delayedHide(2000)
			return
		}

		if text == "" {
			a.indicator.SetStatus(overlay.StatusNoContent, "无内容")
			a.delayedHide(1500)
			return
		}

		logger.Info("Recognized: %s", text)

		// Wait for hotkey to be fully released (max 500ms)
		for i := 0; i < 50; i++ {
			if !a.hk.IsKeyDown(a.cfg.HotkeyVK) {
				break
			}
			time.Sleep(10 * time.Millisecond)
		}
		time.Sleep(50 * time.Millisecond)

		// Paste text
		if err := a.paster.Paste(text); err != nil {
			logger.Error("Paste failed, trying fallback: %v", err)
			if err := a.paster.TypeText(text); err != nil {
				logger.Error("Fallback type also failed: %v", err)
			}
		}

		a.indicator.SetStatus(overlay.StatusDone, "完成")
		if a.cfg.SoundFeedback {
			sound.PlayDone()
		}
		a.delayedHide(2000)
	}()
}

func (a *App) startFreetalkLocked() {
	if a.isRecording || a.isFreetalking {
		return
	}
	a.isFreetalking = true
	a.isRecording = true
	a.hideGen.Add(1)

	logger.Info("Free talk started")
	a.positionIndicatorCenter()
	a.indicator.SetStatus(overlay.StatusFreetalking, "说话中")
	a.indicator.Show()
	if a.cfg.SoundFeedback {
		sound.PlayStart()
	}

	if err := a.recorder.Start(); err != nil {
		logger.Error("Failed to start free talk recording: %v", err)
		a.isFreetalking = false
		a.isRecording = false
		return
	}
}

func (a *App) stopFreetalkLocked() {
	if !a.isFreetalking {
		return
	}
	a.isFreetalking = false
	a.isRecording = false
	a.lastStopTime = time.Now()

	hasVoice := a.recorder.HasVoiceActivity()
	samples := a.recorder.StopAndGetSamples()

	logger.Info("Free talk stopped")
	a.indicator.SetStatus(overlay.StatusProcessing, "识别中")

	go func() {
		if !hasVoice {
			logger.Info("No voice activity detected")
			a.indicator.SetStatus(overlay.StatusNoVoice, "无语音")
			a.delayedHide(1500)
			return
		}

		if samples == nil || len(samples) == 0 {
			a.indicator.SetStatus(overlay.StatusNoContent, "无内容")
			a.delayedHide(1500)
			return
		}

		text, err := a.eng.Recognize(samples)
		if err != nil {
			logger.Error("Recognition failed: %v", err)
			a.indicator.SetStatus(overlay.StatusError, "错误")
			a.delayedHide(2000)
			return
		}

		if text == "" {
			a.indicator.SetStatus(overlay.StatusNoContent, "无内容")
			a.delayedHide(1500)
			return
		}

		logger.Info("Recognized (free talk): %s", text)

		// Wait for hotkey to be fully released (max 500ms)
		for i := 0; i < 50; i++ {
			if !a.hk.IsKeyDown(a.cfg.HotkeyVK) {
				break
			}
			time.Sleep(10 * time.Millisecond)
		}
		time.Sleep(50 * time.Millisecond)

		if err := a.paster.Paste(text); err != nil {
			logger.Error("Paste failed, trying fallback: %v", err)
			if err := a.paster.TypeText(text); err != nil {
				logger.Error("Fallback type also failed: %v", err)
			}
		}

		a.indicator.SetStatus(overlay.StatusDone, "完成")
		if a.cfg.SoundFeedback {
			sound.PlayDone()
		}
		a.delayedHide(2000)
	}()
}

func (a *App) delayedHide(delayMs int) {
	if a.cfg.AutoHide {
		gen := a.hideGen.Load()
		go func() {
			time.Sleep(time.Duration(delayMs) * time.Millisecond)
			if a.hideGen.Load() == gen {
				a.indicator.Hide()
			}
		}()
	}
}

func (a *App) initEngine() {
	logger.Info("Initializing ASR engine...")
	a.wailsApp.Event.Emit("engine:status", map[string]interface{}{
		"status": "loading",
	})

	eng, err := engine.New()
	if err != nil {
		logger.Error("Engine initialization failed: %v", err)
		a.wailsApp.Event.Emit("engine:status", map[string]interface{}{
			"status": "need_model",
			"error":  err.Error(),
		})
		return
	}

	a.mu.Lock()
	a.eng = eng
	a.mu.Unlock()

	logger.Info("ASR engine ready: %s", eng.HardwareInfo())
	a.wailsApp.Event.Emit("engine:status", map[string]interface{}{
		"status":       "ready",
		"hardwareInfo": eng.HardwareInfo(),
	})

	// Show indicator briefly
	keyName := hotkey.GetKeyName(a.cfg.HotkeyVK)
	a.positionIndicatorCenter()
	a.indicator.SetStatus(overlay.StatusReady, "按住"+keyName+"说话")
	a.indicator.Show()
	a.delayedHide(2000)
}

// positionIndicatorCenter places the indicator at bottom-center of the primary screen,
// 100px from the bottom edge — matching WPF behavior.
func (a *App) positionIndicatorCenter() {
	screen := a.wailsApp.Screen.GetPrimary()
	if screen == nil {
		return
	}
	x := screen.Bounds.X + (screen.Bounds.Width-170)/2
	y := screen.Bounds.Y + screen.Bounds.Height - 48 - 100
	a.indicator.SetPosition(x, y)
}

func (a *App) showSettings() {
	if a.settingsWindow != nil {
		a.settingsWindow.Show()
		a.settingsWindow.Focus()
	}
}

func (a *App) toggleSettings() {
	if a.settingsWindow != nil {
		if a.settingsWindow.IsVisible() {
			a.settingsWindow.Hide()
		} else {
			a.settingsWindow.Show()
			a.settingsWindow.Focus()
		}
	}
}

func (a *App) cleanup() {
	logger.Info("Cleaning up...")
	a.cancel()
	if a.indicator != nil {
		a.indicator.Close()
	}
	if a.recorder != nil {
		a.recorder.Close()
	}
	if a.eng != nil {
		a.eng.Close()
	}
	a.wg.Wait()
	logger.Info("Cleanup complete")
}

// SetRecordingHotkey is called by the hotkey service when entering hotkey recording mode.
func (a *App) SetRecordingHotkey(recording bool) {
	a.mu.Lock()
	defer a.mu.Unlock()
	a.isRecordingHotkey = recording
}

// UpdateHotkeyVK updates the hotkey virtual key code.
func (a *App) UpdateHotkeyVK(vk int) {
	a.mu.Lock()
	defer a.mu.Unlock()
	a.cfg.HotkeyVK = vk
	config.Save(a.cfg)
}

// GetEngine returns the current engine (may be nil).
func (a *App) GetEngine() engine.Engine {
	a.mu.Lock()
	defer a.mu.Unlock()
	return a.eng
}

// SetEngine sets the engine after model download.
func (a *App) SetEngine(eng engine.Engine) {
	a.mu.Lock()
	defer a.mu.Unlock()
	a.eng = eng
}
