# VoiceSnap 语闪 - C# WPF 版本

> 长按说话，松手即输 —— 离线 · 极速 · 精准

## 🚀 为什么选择 C# WPF？

| 对比 | Python PyQt5 | C# WPF (本版本) |
|-----|--------------|-----------------|
| **启动速度** | 3-5 秒 | **0.1-0.3 秒** ⚡ |
| **打包体积** | 500MB+ | **68 MB** (含 .NET 运行时) |
| **动画性能** | 较好 | **原生 GPU 加速** |
| **用户安装** | 需要 Python | **无需安装任何东西** ✅ |

## 📦 发布版本

运行 `build.bat` 后，`publish\` 目录包含：

```
publish/
├── VoiceSnap.exe         # 主程序 (68 MB, 包含 .NET 运行时)
└── PythonBackend/
    └── asr_service.py    # ASR 服务
```

**用户双击 `VoiceSnap.exe` 即可运行！**

> ⚠️ 注意：用户仍需安装 Python 和 FunASR 依赖来运行 ASR 服务

## 🏗️ 架构设计

```
┌─────────────────────────────────────────────────┐
│  C# WPF UI (极速启动 0.1秒)                      │
│  • 浮动指示器 (半透明胶囊)                        │
│  • 系统托盘                                      │
│  • 全局快捷键监听 (H.Hooks)                      │
└───────────────────┬─────────────────────────────┘
                    │ HTTP (localhost:8765)
                    ▼
┌─────────────────────────────────────────────────┐
│  Python FastAPI 后端                             │
│  • FunASR 模型加载                               │
│  • 录音 (sounddevice)                           │
│  • 语音识别                                      │
└─────────────────────────────────────────────────┘
```

## 🛠️ 开发环境

### 环境要求

- Windows 10/11
- .NET 8.0 SDK ([下载](https://dotnet.microsoft.com/download/dotnet/8.0))
- Python 3.9+ (已安装 FunASR 依赖)

### 快速开始

```bash
# 1. 还原依赖
dotnet restore

# 2. 运行 (开发模式)
dotnet run

# 3. 编译发布版
build.bat
```

## 📁 项目结构

```
VoiceSnapWPF/
├── VoiceSnap.csproj          # 项目文件
├── App.xaml                  # 应用入口
├── MainWindow.xaml           # 主窗口 (设置界面)
├── MainWindow.xaml.cs        # 主窗口逻辑
├── FloatingIndicator.xaml    # 浮动指示器
├── FloatingIndicator.xaml.cs # 指示器动画逻辑
├── Assets/
│   └── icon.ico              # 应用图标
├── PythonBackend/
│   └── asr_service.py        # ASR 服务 API
├── publish/                  # 发布输出目录
├── build.bat                 # 编译脚本
└── README.md                 # 本文件
```

## 🔌 API 端点

| 端点 | 方法 | 说明 |
|------|------|------|
| `/health` | GET | 健康检查 |
| `/start_recording` | POST | 开始录音 |
| `/stop_recording` | POST | 停止录音并识别 |
| `/volume` | GET | 获取当前音量 (0-1) |

## 🎯 使用方法

1. 启动程序后，指示器显示 🔵 "加载中"
2. 后端就绪后显示 🟢 "按住Ctrl说话"
3. 在任意输入框中长按 **Ctrl** 键
4. 指示器变 🔴 红，显示声纹波形
5. 松开 Ctrl，指示器变 🟠 "识别中"
6. 识别完成后自动输入文字到光标位置

## 📦 依赖库

- [H.Hooks](https://github.com/HavenDV/H.Hooks) - 全局键盘钩子
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) - 系统托盘
- FastAPI / Uvicorn - Python 后端框架
- FunASR - 语音识别模型

## 📄 许可证

MIT License
