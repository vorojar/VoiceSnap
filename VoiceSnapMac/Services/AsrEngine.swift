import Foundation
import SherpaOnnx // 确保已添加 Swift Package

class AsrEngine {
    private var recognizer: OfflineRecognizer?
    private var isInitialized = false
    
    init() {
        initialize()
    }
    
    private func initialize() {
        // 模型路径查找逻辑
        // 在 macOS App 中，模型通常放在 Bundle.main.resourcePath 下
        guard let modelPath = Bundle.main.path(forResource: "model", ofType: "onnx", inDirectory: "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17"),
              let tokensPath = Bundle.main.path(forResource: "tokens", ofType: "txt", inDirectory: "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17")
        else {
            print("Error: Model files not found in Bundle.")
            return
        }
        
        // 配置 Sherpa-Onnx
        // 注意：这里使用 SenseVoice 配置，与 Windows 版保持一致
        let featConfig = OfflineFeatureExtractorConfig(
            sampleRate: 16000,
            featureDim: 80
        )
        
        var modelConfig = OfflineModelConfig()
        modelConfig.senseVoice.model = modelPath
        modelConfig.tokens = tokensPath
        modelConfig.numThreads = 4
        modelConfig.debug = 0
        modelConfig.provider = "cpu" // macOS 上也可以尝试 "coreml" 如果支持
        modelConfig.senseVoice.useInverseTextNormalization = true
        
        let config = OfflineRecognizerConfig(
            featConfig: featConfig,
            modelConfig: modelConfig
        )
        
        do {
            self.recognizer = try OfflineRecognizer(config: config)
            self.isInitialized = true
            print("Sherpa-Onnx initialized successfully.")
        } catch {
            print("Failed to initialize Sherpa-Onnx: \(error)")
        }
    }
    
    func recognize(audioData: [Float], sampleRate: Int = 16000) -> String {
        guard let recognizer = recognizer, isInitialized else {
            return "引擎未初始化"
        }
        
        do {
            let stream = try recognizer.createStream()
            stream.acceptWaveform(samples: audioData, sampleRate: sampleRate)
            try recognizer.decode(stream: stream)
            let result = stream.result
            return result.text
        } catch {
            print("Recognition failed: \(error)")
            return ""
        }
    }
}
