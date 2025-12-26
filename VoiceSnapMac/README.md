# VoiceSnap for macOS

这是一个使用原生 Swift 和 SwiftUI 开发的 macOS 版本 VoiceSnap。

## 开发环境准备

1.  **硬件**: MacBook / Mac Mini / iMac (推荐 M1/M2/M3 芯片，推理速度更快)
2.  **软件**: Xcode 15+

## 如何开始

由于我无法在 Windows 上生成完整的 `.xcodeproj` 项目文件，请按照以下步骤操作：

1.  打开 **Xcode**。
2.  点击 **"Create New Project..."**。
3.  选择 **macOS** -> **App**。
4.  Product Name 输入: `VoiceSnapMac`。
5.  Interface 选择: **SwiftUI**。
6.  Language 选择: **Swift**。
7.  创建项目后，将本项目文件夹中的 `.swift` 文件复制到你的 Xcode 项目文件夹中（替换默认生成的 `ContentView.swift` 和 `VoiceSnapMacApp.swift`）。

## 依赖库 (Sherpa-Onnx)

你需要添加 `sherpa-onnx` 的 Swift 包。

1.  在 Xcode 中，点击菜单栏 **File** -> **Add Package Dependencies...**。
2.  在搜索框输入: `https://github.com/k2-fsa/sherpa-onnx`。
3.  点击 **Add Package**。

## 权限设置 (非常重要)

macOS 对隐私权限要求极高，你需要在 `Info.plist` 中添加以下权限，否则 App 会崩溃：

1.  **Privacy - Microphone Usage Description**: "需要访问麦克风以进行语音识别" (用于录音)。
2.  **Privacy - Accessibility Usage Description**: "需要辅助功能权限以监听全局快捷键" (用于监听 Ctrl 键)。

此外，为了监听全局键盘事件（即使 App 在后台），你需要：
1.  在 Xcode 项目设置 -> **Signing & Capabilities** -> **App Sandbox** 中，**取消勾选 App Sandbox** (或者在 Sandbox 中勾选 "Input Monitoring" 并处理复杂的签名问题，建议开发阶段先关闭 Sandbox)。

## 文件结构说明

*   `VoiceSnapApp.swift`: 程序入口，负责管理菜单栏图标 (MenuBarExtra) 和主窗口。
*   `ContentView.swift`: 主界面 UI，保持与 Windows 版一致的视觉风格。
*   `Services/AsrEngine.swift`: 封装 Sherpa-Onnx 语音识别逻辑。
*   `Services/AudioRecorder.swift`: 负责麦克风录音。
*   `Services/HotkeyService.swift`: 负责监听全局按键 (Ctrl)。
