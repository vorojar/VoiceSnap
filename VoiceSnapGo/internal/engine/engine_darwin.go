//go:build darwin

package engine

import (
	"fmt"
	"voicesnap/internal/logger"

	sherpa "github.com/k2-fsa/sherpa-onnx-go/sherpa_onnx"
	_ "github.com/k2-fsa/sherpa-onnx-go-macos"
)

type sherpaEngine struct {
	recognizer *sherpa.OfflineRecognizer
	hwInfo     string
}

func newPlatformEngine() (Engine, error) {
	modelPath := ModelPath()
	tokensPath := TokensPath()

	// Try CoreML first, then CPU fallback
	providers := []struct {
		name     string
		provider string
	}{
		{"CoreML (Apple Neural Engine)", "coreml"},
		{"CPU", "cpu"},
	}

	for _, p := range providers {
		config := sherpa.OfflineRecognizerConfig{}
		config.FeatConfig.SampleRate = 16000
		config.FeatConfig.FeatureDim = 80
		config.ModelConfig.SenseVoice.Model = modelPath
		config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1
		config.ModelConfig.Tokens = tokensPath
		config.ModelConfig.NumThreads = 4
		config.ModelConfig.Provider = p.provider
		config.DecodingMethod = "greedy_search"

		recognizer := sherpa.NewOfflineRecognizer(&config)
		if recognizer != nil {
			info := fmt.Sprintf("SenseVoice · %s", p.name)
			logger.Info("Engine initialized: %s", info)
			return &sherpaEngine{
				recognizer: recognizer,
				hwInfo:     info,
			}, nil
		}
		logger.Info("Failed to init with %s, trying next provider", p.name)
	}

	return nil, fmt.Errorf("failed to initialize sherpa-onnx with any provider")
}

func (e *sherpaEngine) Recognize(samples []float32) (string, error) {
	stream := sherpa.NewOfflineStream(e.recognizer)
	defer sherpa.DeleteOfflineStream(stream)

	stream.AcceptWaveform(16000, samples)

	e.recognizer.Decode(stream)
	result := stream.GetResult()

	return result.Text, nil
}

func (e *sherpaEngine) HardwareInfo() string {
	return e.hwInfo
}

func (e *sherpaEngine) Close() {
	if e.recognizer != nil {
		sherpa.DeleteOfflineRecognizer(e.recognizer)
		e.recognizer = nil
	}
}
