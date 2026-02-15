//go:build linux

package engine

import (
	"fmt"
	"voicesnap/internal/logger"

	sherpa "github.com/k2-fsa/sherpa-onnx-go/sherpa_onnx"
	_ "github.com/k2-fsa/sherpa-onnx-go-linux"
)

type sherpaEngine struct {
	recognizer *sherpa.OfflineRecognizer
	hwInfo     string
}

func newPlatformEngine() (Engine, error) {
	modelPath := ModelPath()
	tokensPath := TokensPath()

	// Linux: CPU only
	config := sherpa.OfflineRecognizerConfig{}
	config.FeatConfig.SampleRate = 16000
	config.FeatConfig.FeatureDim = 80
	config.ModelConfig.SenseVoice.Model = modelPath
	config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1
	config.ModelConfig.Tokens = tokensPath
	config.ModelConfig.NumThreads = 4
	config.ModelConfig.Provider = "cpu"
	config.DecodingMethod = "greedy_search"

	recognizer := sherpa.NewOfflineRecognizer(&config)
	if recognizer != nil {
		info := "SenseVoice · CPU"
		logger.Info("Engine initialized: %s", info)
		return &sherpaEngine{
			recognizer: recognizer,
			hwInfo:     info,
		}, nil
	}

	return nil, fmt.Errorf("failed to initialize sherpa-onnx on Linux")
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
