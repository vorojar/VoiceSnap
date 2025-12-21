# VoiceSnap 语闪

<div align="center">

**极速离线语音输入工具**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-brightgreen.svg)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)]()

</div>

---

## 🌟 简介

VoiceSnap 语闪是一款基于 **Sherpa-ONNX** 和 **SenseVoice** 构建的高性能离线语音输入软件。它完全运行在本地，无需联网，保护您的隐私，并且响应速度极快。

## ✨ 核心特性

| 特性 | 描述 |
|------|------|
| ⚡ **极速响应** | 基于 C# 原生开发，启动仅需 0.1 秒，内存占用低 |
| 🔒 **完全离线** | 内置 SenseVoice 高精度大模型，数据不出本地 |
| 🎯 **精准识别** | 支持中、英、日、韩、粤语混合识别，自动添加标点 |
| 🎈 **极简交互** | 独特的「长按说话，松手即输」模式，不打断工作流 |

## 📥 下载安装

1. 从 [Releases](../../releases) 页面下载最新版本
2. 解压到任意目录
3. 双击运行 `VoiceSnap.exe`
4. 首次运行会自动下载模型（约 200MB）

> **系统要求**: Windows 10 / Windows 11

## 🎮 使用方法

1. **启动软件** - 成功启动后，屏幕右下角会出现一个半透明的胶囊指示器，显示「按住Ctrl说话」
2. **长按说话** - 在任意可输入文字的地方（微信、Word、浏览器等），按住 **Ctrl** 键不放
3. **语音输入** - 指示器变红并显示波形时，开始说话
4. **松手即输** - 说完后松开 Ctrl 键，文字将自动输入到光标位置

## ⚙️ 设置说明

- **托盘图标**: 在系统托盘区（右下角时间旁）可以找到 VoiceSnap 的图标
- **右键菜单**: 右键点击托盘图标可以退出软件
- **左键点击**: 打开设置面板，可以：
  - 修改触发按键（支持 Ctrl, Alt, Shift, CapsLock 等）
  - 设置开机自启
  - 开启/关闭自动隐藏指示器

## ❓ 常见问题

<details>
<summary><b>Q: 为什么按住 Ctrl 没有反应？</b></summary>

请检查指示器状态是否为「按住Ctrl说话」。如果是「加载中」，请稍等。如果软件未启动，请检查托盘区是否有图标。
</details>

<details>
<summary><b>Q: 识别准确率不高？</b></summary>

请尽量使用清晰的普通话，并靠近麦克风。软件默认使用系统默认录音设备，请在系统声音设置中确认默认麦克风是否正确。
</details>

<details>
<summary><b>Q: 杀毒软件误报？</b></summary>

由于软件涉及全局按键监听和模拟键盘输入，可能会被部分杀毒软件误判。本软件完全开源且安全，请放心添加信任。
</details>

## 🏗️ 技术栈

- **开发框架**: .NET 8 + WPF
- **语音引擎**: [Sherpa-ONNX](https://github.com/k2-fsa/sherpa-onnx)
- **语音模型**: [SenseVoice](https://github.com/FunAudioLLM/SenseVoice) (by FunAudioLLM)

## 📁 项目结构

```
VoiceSnap/
├── VoiceSnap.Engine/      # C# 原生语音引擎封装
├── VoiceSnapWPF/          # WPF 主应用程序
└── VoiceSnap_Release/     # 发布版本
    ├── VoiceSnap.exe      # 主程序
    ├── models/            # SenseVoice 模型文件
    └── config.json        # 配置文件
```

## 📜 开源许可

本项目基于 [MIT License](LICENSE) 开源。

---

**祝您使用愉快！** 🎉
