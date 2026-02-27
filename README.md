# VoiceSnap 语闪

> 长按说话，松手即输 —— 离线 · 极速 · 跨平台

![VoiceSnap 语闪](screenshot.png)

VoiceSnap 是一款离线语音转文字工具。按住快捷键说话，松开即识别并输入文字到任意应用。无需联网，无需注册，开箱即用。

## 核心特性

- **完全离线** — SenseVoice 模型本地推理，无数据上传
- **DirectML GPU 加速** — 自动检测显卡，CPU 自动回退
- **两种输入模式** — 长按说话 + 短按自由说话
- **自定义快捷键** — 支持 Ctrl / Alt / Shift / 功能键 / 字母键
- **麦克风选择** — 下拉切换输入设备
- **录音音效** — 马林巴风格提示音（可关闭）
- **录音计时** — 指示器实时显示录音时长（0:00 → 0:03 → ...）
- **Escape 取消** — 录音中按 Esc 立即取消
- **剪贴板保护** — 粘贴后自动恢复原剪贴板内容
- **识别历史** — 自动保存最近 50 条，可复制、删除、设置保留时长
- **语气词过滤** — 自动去除"嗯、啊、呃"及 um/uh/hmm 等填充词
- **静音自动停止** — 自由说话模式下连续 3 秒无声自动结束并识别
- **中英混合优化** — 自动在中英文之间加空格，句首大写
- **DPI 自适应** — 指示器根据系统缩放自动调整大小
- **指示器可拖拽** — 拖动浮动指示器到任意位置，自动记忆
- **中英双语界面** — 自动跟随系统语言
- **应用内更新** — 自动检查新版本并提示下载
- **系统托盘常驻** — 关闭窗口不退出
- **开机自启** — 可选

## 快速开始

### 用户

1. 从 [Releases](https://github.com/vorojar/VoiceSnap/releases) 下载最新版本
2. **Windows 10/11**：解压到任意目录，双击 `voicesnap.exe`
3. **Windows 7**：下载 Win7 专用版 `VoiceSnap_Setup.exe`（6.7 MB），双击解压即用
4. **macOS**：打开 `.dmg`，拖入 Applications，首次启动需授予辅助功能权限
5. 首次启动自动下载语音模型（~152 MB）
6. 就绪后，在任意输入框中按住 **右 Ctrl**（macOS: **右 Command**）说话，松开即输入

### 开发者

```bash
# 环境要求
# - Go 1.22+ (Win7 版需 Go 1.20)
# - Node.js 20+
# - CGO 编译器 (Windows: LLVM MinGW UCRT, macOS: Xcode Command Line Tools)

# 克隆
git clone https://github.com/vorojar/VoiceSnap.git
cd VoiceSnap/VoiceSnapGo

# 安装前端依赖
cd frontend && npm install && cd ..

# 开发模式
wails3 dev

# 生产构建 (Windows)
windres voicesnap.rc -o voicesnap.syso
CGO_ENABLED=1 go build -ldflags "-H windowsgui -s -w" -o voicesnap.exe .

# 生产构建 (macOS)
CGO_ENABLED=1 go build -o voicesnap .

# 生产构建 (Windows 7)
cd ../VoiceSnapWin7
windres voicesnap.rc -o voicesnap.syso
GOROOT=/path/to/go1.20 CGO_ENABLED=1 go build -ldflags "-H windowsgui -s -w" -o voicesnap_win7.exe .
bash build_sfx.sh portable  # 生成 7z 自解压包
```

## 使用方法

1. 系统托盘出现 VoiceSnap 图标，引擎加载完成后就绪
2. **长按模式**：按住快捷键说话，松开即识别并粘贴
3. **自由说话**：短按一下开始，连续说话，再短按一下停止
4. **取消录音**：录音中按 Esc
5. 右键托盘图标可打开设置或退出

## 运行文件

**Windows 10/11:**

| 文件 | 大小 | 说明 |
|---|---|---|
| `voicesnap.exe` | ~15 MB | 主程序 |
| `onnxruntime.dll` | ~15 MB | ONNX Runtime |
| `sherpa-onnx-c-api.dll` | ~4 MB | sherpa-onnx C API |
| `sherpa-onnx-cxx-api.dll` | ~248 KB | sherpa-onnx C++ API |
| `models/sensevoice/` | ~152 MB | 语音模型（首次自动下载） |

**Windows 7 (64-bit):**

| 文件 | 大小 | 说明 |
|---|---|---|
| `VoiceSnap_Setup.exe` | ~6.7 MB | 7z 自解压包（含 exe + 91 个 DLL） |
| `models/sensevoice/` | ~152 MB | 语音模型（首次启动自动下载） |

**macOS:**

| 文件 | 大小 | 说明 |
|---|---|---|
| `VoiceSnap.app` | ~15 MB | 应用包（含 dylib） |
| `models/sensevoice/` | ~152 MB | 语音模型（首次自动下载） |

## 项目结构

```
VoiceSnapGo/                # 主版本 (Windows 10/11 + macOS)
├── main.go                 # 入口：单实例 + Wails 启动
├── app.go                  # 编排：热键 → 录音 → 识别 → 粘贴
├── internal/               # 核心包（音频/引擎/热键/粘贴/...）
├── services/               # Wails 服务（前端绑定）
└── frontend/               # Svelte + Vite

VoiceSnapWin7/              # Windows 7 专用精简版
├── main.go                 # 入口：单实例
├── app.go                  # 编排（无 Wails 依赖）
├── tray_windows.go         # 原生 Win32 系统托盘
├── download_windows.go     # 模型自动下载 + 进度条
├── hotkey_dialog_windows.go # 热键设置对话框
├── screen_windows.go       # 屏幕定位 + 语言检测
├── build_sfx.sh            # 7z 自解压打包脚本
└── internal/               # 核心包（复用自 VoiceSnapGo）
```

## 技术栈

| 组件 | 技术 |
|---|---|
| 桌面框架 | [Wails v3](https://wails.io/) |
| 前端 | [Svelte 5](https://svelte.dev/) + Vite + TypeScript |
| 音频录制 | [malgo](https://github.com/gen2brain/malgo) (miniaudio) |
| 语音识别 | [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) (SenseVoice) |
| 全局热键 | Windows: GetAsyncKeyState 轮询 / macOS: NSEvent + CGEventSource |
| 文字输入 | Windows: 剪贴板 + SendInput / macOS: 剪贴板 + CGEvent |
| 浮动指示器 | Windows: GDI+ Layered Window / macOS: AppKit + CoreGraphics |
| 音效 | 程序合成 WAV + 平台原生播放 |

## 许可证

MIT License

## 致谢

- [SherpaOnnx](https://github.com/k2-fsa/sherpa-onnx) — 语音识别引擎
- [Wails](https://wails.io/) — Go 桌面应用框架
- [SenseVoice](https://github.com/FunAudioLLM/SenseVoice) — 语音模型
