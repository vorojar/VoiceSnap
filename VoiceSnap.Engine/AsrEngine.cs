using System;
using System.Collections.Generic;
using System.Linq;
using SherpaOnnx;

namespace VoiceSnap.Engine
{
    public class AsrEngine : IDisposable
    {
        private OfflineRecognizer? _recognizer;
        private bool _isInitialized = false;
        private string _hardwareInfo = "Unknown";

        public bool IsInitialized => _isInitialized;
        public string HardwareInfo => _hardwareInfo;
        public string ShortHardwareInfo => _hardwareInfo.Contains("GPU") ? "GPU" : "CPU";

        public void Initialize(string modelPath, string tokensPath, string? encoderPath = null, string? decoderPath = null)
        {
            // 尝试使用 DirectML 加速
            try
            {
                InitializeInternal(modelPath, tokensPath, "directml");
                _hardwareInfo = "GPU 加速 (DirectML)";
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DirectML 初始化失败，回退到 CPU: {ex.Message}");
            }

            // 回退到 CPU
            try
            {
                InitializeInternal(modelPath, tokensPath, "cpu");
                _hardwareInfo = "标准模式 (CPU)";
            }
            catch (Exception ex)
            {
                throw new Exception($"引擎初始化失败: {ex.Message}", ex);
            }
        }

        private void InitializeInternal(string modelPath, string tokensPath, string provider)
        {
            var config = new OfflineRecognizerConfig();
            
            // 配置 SenseVoice 模型
            config.ModelConfig.SenseVoice.Model = modelPath;
            config.ModelConfig.Tokens = tokensPath;
            config.ModelConfig.NumThreads = 4;
            config.ModelConfig.Debug = 0;
            config.ModelConfig.Provider = provider;
            config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;

            _recognizer = new OfflineRecognizer(config);
            _isInitialized = true;
        }

        public string Recognize(float[] samples, int sampleRate = 16000)
        {
            if (_recognizer == null) throw new InvalidOperationException("引擎未初始化");

            var stream = _recognizer.CreateStream();
            stream.AcceptWaveform(sampleRate, samples);
            _recognizer.Decode(stream);
            
            var result = stream.Result;
            return result.Text;
        }

        public void Dispose()
        {
            _recognizer?.Dispose();
        }
    }
}
