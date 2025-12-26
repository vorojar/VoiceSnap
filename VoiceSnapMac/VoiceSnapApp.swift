import SwiftUI
import AppKit

@main
struct VoiceSnapApp: App {
    // 保持对各个 Service 的引用
    @StateObject private var appState = AppState()
    
    var body: some Scene {
        // 主窗口
        WindowGroup {
            ContentView()
                .environmentObject(appState)
                .preferredColorScheme(.light) // 强制浅色或根据需求调整
                .background(Color(hex: "3F51B5")) // 保持与 Windows 版一致的主题色
        }
        .windowStyle(.hiddenTitleBar) // 隐藏默认标题栏，使用自定义的
        .commands {
            // 可以在这里添加菜单栏命令
            CommandGroup(replacing: .newItem) { }
        }
        
        // 菜单栏图标 (系统托盘)
        MenuBarExtra("VoiceSnap", systemImage: "mic.fill") {
            Button("显示主窗口") {
                NSApp.activate(ignoringOtherApps: true)
                // 这里需要通过 NSApp 找到主窗口并显示，SwiftUI 的 WindowGroup 管理比较自动
            }
            Divider()
            Button("退出") {
                NSApplication.shared.terminate(nil)
            }
        }
    }
}

// 全局状态管理
class AppState: ObservableObject {
    @Published var isRecording = false
    @Published var recognizedText = ""
    @Published var statusMessage = "按住 Ctrl 说话"
    
    // 初始化服务
    let asrEngine = AsrEngine()
    let audioRecorder = AudioRecorder()
    let hotkeyService = HotkeyService()
    
    init() {
        setupBindings()
    }
    
    private func setupBindings() {
        // 绑定按键事件
        hotkeyService.onKeyDown = { [weak self] in
            self?.startRecording()
        }
        
        hotkeyService.onKeyUp = { [weak self] in
            self?.stopRecording()
        }
    }
    
    func startRecording() {
        guard !isRecording else { return }
        isRecording = true
        statusMessage = "正在聆听..."
        audioRecorder.startRecording()
        
        // 显示浮窗 (Floating Indicator)
        FloatingIndicatorWindow.shared.show()
    }
    
    func stopRecording() {
        guard isRecording else { return }
        isRecording = false
        statusMessage = "识别中..."
        
        let audioData = audioRecorder.stopRecording()
        FloatingIndicatorWindow.shared.hide()
        
        // 异步识别
        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            guard let self = self else { return }
            let text = self.asrEngine.recognize(audioData: audioData)
            
            DispatchQueue.main.async {
                self.recognizedText = text
                self.statusMessage = "识别完成"
                // 模拟输入到当前活动窗口
                InputSimulator.simulateString(text)
            }
        }
    }
}

// 颜色扩展
extension Color {
    init(hex: String) {
        let hex = hex.trimmingCharacters(in: CharacterSet.alphanumerics.inverted)
        var int: UInt64 = 0
        Scanner(string: hex).scanHexInt64(&int)
        let a, r, g, b: UInt64
        switch hex.count {
        case 3: // RGB (12-bit)
            (a, r, g, b) = (255, (int >> 8) * 17, (int >> 4 & 0xF) * 17, (int & 0xF) * 17)
        case 6: // RGB (24-bit)
            (a, r, g, b) = (255, int >> 16, int >> 8 & 0xFF, int & 0xFF)
        case 8: // ARGB (32-bit)
            (a, r, g, b) = (int >> 24, int >> 16 & 0xFF, int >> 8 & 0xFF, int & 0xFF)
        default:
            (a, r, g, b) = (1, 1, 1, 0)
        }

        self.init(
            .sRGB,
            red: Double(r) / 255,
            green: Double(g) / 255,
            blue:  Double(b) / 255,
            opacity: Double(a) / 255
        )
    }
}
