package audio

import (
	"math"
	"sync"
	"voicesnap/internal/logger"

	"github.com/gen2brain/malgo"
)

const (
	sampleRate     = 16000
	channels       = 1
	bitsPerSample  = 16
	silenceThreshold = 0.05
)

// InputDevice represents an audio input device.
type InputDevice struct {
	Name      string `json:"name"`
	IsDefault bool   `json:"isDefault"`
}

// Recorder captures audio from the default input device using malgo (miniaudio).
type Recorder struct {
	mu sync.Mutex

	ctx     *malgo.AllocatedContext
	device  *malgo.Device

	// PCM buffer (16-bit signed, little-endian)
	pcmBuf []byte

	// Preferred device name (empty = system default)
	preferredDevice string

	// State
	isRecording      bool
	maxVolume        float64
	currentVolume    float64
	volumeCallback   func(float64)
	deviceChangeCb   func(string)
}

// NewRecorder creates a new audio recorder backed by malgo.
func NewRecorder() *Recorder {
	ctxConfig := malgo.ContextConfig{}
	ctx, err := malgo.InitContext(nil, ctxConfig, nil)
	if err != nil {
		logger.Error("Failed to init malgo context: %v", err)
		return &Recorder{}
	}
	return &Recorder{ctx: ctx}
}

// OnVolume registers a callback for real-time volume updates.
func (r *Recorder) OnVolume(fn func(float64)) {
	r.mu.Lock()
	defer r.mu.Unlock()
	r.volumeCallback = fn
}

// OnDeviceChange registers a callback for audio device changes.
func (r *Recorder) OnDeviceChange(fn func(string)) {
	r.mu.Lock()
	defer r.mu.Unlock()
	r.deviceChangeCb = fn
}

// ListInputDevices returns all available audio capture devices.
func (r *Recorder) ListInputDevices() []InputDevice {
	if r.ctx == nil {
		return nil
	}
	infos, err := r.ctx.Context.Devices(malgo.Capture)
	if err != nil {
		logger.Error("Failed to list devices: %v", err)
		return nil
	}
	var result []InputDevice
	for i, info := range infos {
		result = append(result, InputDevice{
			Name:      info.Name(),
			IsDefault: i == 0,
		})
	}
	return result
}

// SetPreferredDevice sets the preferred device by name. Empty string = system default.
func (r *Recorder) SetPreferredDevice(name string) {
	r.mu.Lock()
	defer r.mu.Unlock()
	r.preferredDevice = name
	logger.Info("Preferred device set to: %q", name)
}

// Start begins recording from the preferred (or default) capture device.
func (r *Recorder) Start() error {
	r.mu.Lock()
	defer r.mu.Unlock()

	if r.isRecording {
		return nil
	}

	r.pcmBuf = nil
	r.maxVolume = 0
	r.currentVolume = 0

	deviceConfig := malgo.DefaultDeviceConfig(malgo.Capture)
	deviceConfig.Capture.Format = malgo.FormatS16
	deviceConfig.Capture.Channels = channels
	deviceConfig.SampleRate = sampleRate
	deviceConfig.PeriodSizeInMilliseconds = 50

	// Use preferred device if set
	if r.preferredDevice != "" {
		infos, err := r.ctx.Context.Devices(malgo.Capture)
		if err == nil {
			for _, info := range infos {
				if info.Name() == r.preferredDevice {
					deviceConfig.Capture.DeviceID = info.ID.Pointer()
					break
				}
			}
		}
	}

	callbacks := malgo.DeviceCallbacks{
		Data: func(outputSamples, inputSamples []byte, framecount uint32) {
			r.onData(inputSamples)
		},
	}

	device, err := malgo.InitDevice(r.ctx.Context, deviceConfig, callbacks)
	if err != nil {
		return err
	}

	if err := device.Start(); err != nil {
		device.Uninit()
		return err
	}

	r.device = device
	r.isRecording = true
	logger.Info("Recording started (16kHz/16-bit/mono)")
	return nil
}

// Stop stops recording and discards audio data.
func (r *Recorder) Stop() {
	r.mu.Lock()
	defer r.mu.Unlock()

	r.stopDevice()
	r.pcmBuf = nil
}

// StopAndGetSamples stops recording and returns the captured audio as float32 samples.
func (r *Recorder) StopAndGetSamples() []float32 {
	r.mu.Lock()
	defer r.mu.Unlock()

	r.stopDevice()

	if len(r.pcmBuf) < 2 {
		return nil
	}

	// Convert 16-bit PCM to float32
	numSamples := len(r.pcmBuf) / 2
	samples := make([]float32, numSamples)
	for i := 0; i < numSamples; i++ {
		sample := int16(r.pcmBuf[i*2]) | int16(r.pcmBuf[i*2+1])<<8
		samples[i] = float32(sample) / 32768.0
	}

	logger.Info("Recording stopped, %d samples captured", numSamples)
	r.pcmBuf = nil
	return samples
}

// HasVoiceActivity returns true if the max volume exceeded the silence threshold.
func (r *Recorder) HasVoiceActivity() bool {
	r.mu.Lock()
	defer r.mu.Unlock()
	return r.maxVolume > silenceThreshold
}

// GetDeviceName returns the name of the current capture device.
func (r *Recorder) GetDeviceName() string {
	if r.ctx == nil {
		return "Default"
	}
	devices, err := r.ctx.Context.Devices(malgo.Capture)
	if err != nil || len(devices) == 0 {
		return "Default"
	}
	return devices[0].Name()
}

// Close releases all audio resources.
func (r *Recorder) Close() {
	r.mu.Lock()
	defer r.mu.Unlock()

	r.stopDevice()
	if r.ctx != nil {
		r.ctx.Free()
		r.ctx = nil
	}
}

func (r *Recorder) stopDevice() {
	if r.device != nil {
		r.device.Stop()
		r.device.Uninit()
		r.device = nil
	}
	r.isRecording = false
}

func (r *Recorder) onData(input []byte) {
	r.mu.Lock()

	if !r.isRecording {
		r.mu.Unlock()
		return
	}

	// Append raw PCM data
	r.pcmBuf = append(r.pcmBuf, input...)

	// Calculate RMS volume
	numSamples := len(input) / 2
	if numSamples == 0 {
		r.mu.Unlock()
		return
	}

	var sum float64
	for i := 0; i < len(input)-1; i += 2 {
		sample := int16(input[i]) | int16(input[i+1])<<8
		normalized := float64(sample) / 32768.0
		sum += normalized * normalized
	}
	rms := math.Sqrt(sum / float64(numSamples))
	volume := math.Min(1.0, rms*8)

	r.currentVolume = volume
	if volume > r.maxVolume {
		r.maxVolume = volume
	}

	cb := r.volumeCallback
	r.mu.Unlock()

	// Invoke callback outside lock to prevent deadlock with app mutex
	if cb != nil {
		cb(volume)
	}
}
