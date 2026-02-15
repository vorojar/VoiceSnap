package sound

import (
	"encoding/binary"
	"math"
	"sync"
	"syscall"
	"unsafe"
)

var (
	winmm        = syscall.NewLazyDLL("winmm.dll")
	playSoundW   = winmm.NewProc("PlaySoundW")
	sndMemory    uintptr = 0x0004
	sndAsync     uintptr = 0x0001
	sndNoDefault uintptr = 0x0002

	startWAV  []byte
	doneWAV   []byte
	cancelWAV []byte
	once      sync.Once
)

const (
	sRate    = 48000
	bitDepth = 16
	nCh      = 1
)

func init() {
	once.Do(func() {
		// Ascending "deng-deng ↑" — matches Typeless record-start profile
		startWAV = genMalletPair(392.0, 523.0, 0.13, 0.28, 0.015)
		// Descending "deng-deng ↓" — matches Typeless record-end profile
		doneWAV = genMalletPair(392.0, 294.0, 0.13, 0.28, 0.015)
		// Cancel: single low soft tap
		cancelWAV = genMalletSingle(294.0, 0.20)
	})
}

func PlayStart()  { go playWAV(startWAV) }
func PlayDone()   { go playWAV(doneWAV) }
func PlayCancel() { go playWAV(cancelWAV) }

func playWAV(data []byte) {
	if len(data) == 0 {
		return
	}
	playSoundW.Call(
		uintptr(unsafe.Pointer(&data[0])),
		0,
		sndMemory|sndAsync|sndNoDefault,
	)
}

// genMalletPair generates two mallet-like tones (like a marimba) with a gap.
// Mimics the Typeless "deng-deng" pattern:
//   - 20ms leading silence
//   - Tone 1: fast attack (3ms) + exponential decay over dur1
//   - gap of silence
//   - Tone 2: fast attack (3ms) + exponential decay over dur2 (longer tail)
//   - 20ms trailing silence
func genMalletPair(freq1, freq2, dur1, dur2, gap float64) []byte {
	leadSilence := int(0.02 * sRate)
	n1 := int(dur1 * sRate)
	nGap := int(gap * sRate)
	n2 := int(dur2 * sRate)
	trailSilence := int(0.02 * sRate)
	total := leadSilence + n1 + nGap + n2 + trailSilence

	samples := make([]int16, total)

	// Tone 1
	off := leadSilence
	for i := 0; i < n1; i++ {
		t := float64(i) / sRate
		env := malletEnvelope(i, n1, 6.0) // faster decay
		val := malletOsc(freq1, t, env)
		samples[off+i] = int16(clamp(val*32767, -32767, 32767))
	}

	// Tone 2
	off = leadSilence + n1 + nGap
	for i := 0; i < n2; i++ {
		t := float64(i) / sRate
		env := malletEnvelope(i, n2, 4.5) // slower decay, longer tail
		val := malletOsc(freq2, t, env)
		samples[off+i] = int16(clamp(val*32767, -32767, 32767))
	}

	return buildWAV(samples)
}

// genMalletSingle generates a single mallet tap.
func genMalletSingle(freq, dur float64) []byte {
	lead := int(0.02 * sRate)
	n := int(dur * sRate)
	trail := int(0.02 * sRate)
	total := lead + n + trail

	samples := make([]int16, total)
	for i := 0; i < n; i++ {
		t := float64(i) / sRate
		env := malletEnvelope(i, n, 5.0)
		val := malletOsc(freq, t, env) * 0.8
		samples[lead+i] = int16(clamp(val*32767, -32767, 32767))
	}
	return buildWAV(samples)
}

// malletOsc produces a marimba-like oscillator: fundamental + 2nd harmonic + 3rd harmonic.
// Volume is low (~14% of full scale, matching Typeless -17dB).
func malletOsc(freq, t, env float64) float64 {
	fundamental := math.Sin(2 * math.Pi * freq * t)
	harmonic2 := math.Sin(2*math.Pi*freq*2*t) * 0.15
	harmonic3 := math.Sin(2*math.Pi*freq*3*t) * 0.05
	return (fundamental + harmonic2 + harmonic3) * env * 0.135
}

// malletEnvelope: 3ms attack, then exponential decay.
// decayRate controls how fast it fades (higher = faster).
func malletEnvelope(i, total int, decayRate float64) float64 {
	attackSamples := sRate * 3 / 1000 // 3ms
	t := float64(i) / sRate

	var env float64
	if i < attackSamples {
		// Fast smooth attack (sine curve for no click)
		env = math.Sin(math.Pi / 2 * float64(i) / float64(attackSamples))
	} else {
		// Exponential decay from peak
		decayT := t - float64(attackSamples)/sRate
		env = math.Exp(-decayRate * decayT)
	}

	// Gentle fade-out at the very end to avoid any click
	fadeOut := sRate * 5 / 1000 // 5ms
	if i > total-fadeOut {
		remaining := float64(total - i)
		env *= remaining / float64(fadeOut)
	}

	return env
}

func clamp(v, lo, hi float64) float64 {
	if v < lo {
		return lo
	}
	if v > hi {
		return hi
	}
	return v
}

func buildWAV(samples []int16) []byte {
	dataSize := len(samples) * 2
	fileSize := 44 + dataSize

	buf := make([]byte, fileSize)
	copy(buf[0:4], "RIFF")
	binary.LittleEndian.PutUint32(buf[4:8], uint32(fileSize-8))
	copy(buf[8:12], "WAVE")

	copy(buf[12:16], "fmt ")
	binary.LittleEndian.PutUint32(buf[16:20], 16)
	binary.LittleEndian.PutUint16(buf[20:22], 1)
	binary.LittleEndian.PutUint16(buf[22:24], nCh)
	binary.LittleEndian.PutUint32(buf[24:28], sRate)
	binary.LittleEndian.PutUint32(buf[28:32], sRate*nCh*bitDepth/8)
	binary.LittleEndian.PutUint16(buf[32:34], nCh*bitDepth/8)
	binary.LittleEndian.PutUint16(buf[34:36], bitDepth)

	copy(buf[36:40], "data")
	binary.LittleEndian.PutUint32(buf[40:44], uint32(dataSize))

	for i, s := range samples {
		binary.LittleEndian.PutUint16(buf[44+i*2:44+i*2+2], uint16(s))
	}

	return buf
}
