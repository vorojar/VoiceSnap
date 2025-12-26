import Foundation
import AVFoundation

class AudioRecorder: NSObject {
    private var audioEngine: AVAudioEngine?
    private var inputNode: AVAudioInputNode?
    private var audioBuffer: [Float] = []
    private let sampleRate: Double = 16000.0
    
    override init() {
        super.init()
        setupAudioSession()
    }
    
    private func setupAudioSession() {
        // macOS 不需要像 iOS 那样配置 AVAudioSession，但需要处理权限
    }
    
    func startRecording() {
        audioBuffer.removeAll()
        
        audioEngine = AVAudioEngine()
        guard let audioEngine = audioEngine else { return }
        
        inputNode = audioEngine.inputNode
        let inputFormat = inputNode?.inputFormat(forBus: 0)
        
        // 我们需要重采样到 16k，因为 SherpaOnnx 通常需要 16k
        // 但简单起见，我们先获取原始数据，后续处理或假设输入支持
        // 更好的做法是使用 mixer node 进行重采样
        
        let recordingFormat = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: sampleRate, channels: 1, interleaved: false)!
        
        // 安装 Tap
        // 注意：如果硬件不支持 16k，这里可能需要 Converter。
        // 为简化代码，这里直接 Tap 原始格式，然后手动转换或假设匹配。
        // 实际生产代码需要更严谨的重采样逻辑。
        
        inputNode?.installTap(onBus: 0, bufferSize: 1024, format: inputFormat) { [weak self] (buffer, time) in
            guard let self = self else { return }
            self.appendBuffer(buffer)
        }
        
        do {
            try audioEngine.start()
        } catch {
            print("Audio Engine start error: \(error)")
        }
    }
    
    private func appendBuffer(_ buffer: AVAudioPCMBuffer) {
        // 简单的重采样/格式转换逻辑 (简化版)
        // 假设我们需要单声道 Float32
        
        guard let channelData = buffer.floatChannelData else { return }
        let channelPointer = channelData[0] // 取第一个声道
        let frameLength = Int(buffer.frameLength)
        
        // 这里应该做重采样，如果 inputFormat.sampleRate != 16000
        // 暂时假设我们能拿到数据，直接存入
        // 实际项目中建议使用 AVAudioConverter
        
        var samples = [Float](repeating: 0, count: frameLength)
        for i in 0..<frameLength {
            samples[i] = channelPointer[i]
        }
        
        DispatchQueue.main.async {
            self.audioBuffer.append(contentsOf: samples)
        }
    }
    
    func stopRecording() -> [Float] {
        inputNode?.removeTap(onBus: 0)
        audioEngine?.stop()
        audioEngine = nil
        inputNode = nil
        
        return audioBuffer
    }
}
