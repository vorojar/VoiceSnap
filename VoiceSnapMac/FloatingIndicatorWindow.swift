import SwiftUI
import AppKit

class FloatingIndicatorWindow {
    static let shared = FloatingIndicatorWindow()
    
    private var window: NSWindow?
    
    private init() {
        setupWindow()
    }
    
    private func setupWindow() {
        // 创建一个无边框、透明背景的窗口
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 200, height: 60),
            styleMask: [.borderless, .nonactivatingPanel], // nonactivatingPanel 避免抢夺焦点
            backing: .buffered,
            defer: false
        )
        
        window.level = .floating // 浮在最上层
        window.backgroundColor = .clear
        window.isOpaque = false
        window.hasShadow = true
        window.ignoresMouseEvents = true // 鼠标穿透
        
        // 设置 SwiftUI 内容
        let hostingView = NSHostingView(rootView: FloatingView())
        window.contentView = hostingView
        
        self.window = window
    }
    
    func show() {
        guard let window = window else { return }
        
        // 获取鼠标当前位置
        let mouseLocation = NSEvent.mouseLocation
        // 注意：macOS 坐标系原点在左下角，需要转换或者直接使用
        // 简单起见，我们先显示在屏幕中央或者鼠标附近
        // 这里简单居中显示在鼠标附近
        let windowSize = window.frame.size
        let origin = CGPoint(x: mouseLocation.x + 20, y: mouseLocation.y - windowSize.height - 20)
        
        window.setFrameOrigin(origin)
        window.orderFront(nil)
    }
    
    func hide() {
        window?.orderOut(nil)
    }
}

struct FloatingView: View {
    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: "mic.fill")
                .foregroundColor(.white)
                .symbolEffect(.pulse, isActive: true)
            Text("正在聆听...")
                .foregroundColor(.white)
                .font(.system(size: 14, weight: .medium))
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 10)
        .background(
            Capsule()
                .fill(Color(hex: "3F51B5"))
                .shadow(radius: 4)
        )
    }
}
