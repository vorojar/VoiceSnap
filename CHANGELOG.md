# Changelog

## v2.0.0 (2026-02-15) — Go 跨平台重写

VoiceSnap 2.0 使用 Go + Wails v3 + Svelte 从零重写，目标是更小体积、更优体验、未来跨平台。

### 重写亮点

- **全新技术栈**：从 C# WPF/.NET 8 迁移至 Go + Wails v3 + Svelte 5
- **体积缩减 57%**：运行文件从 ~80 MB 降至 ~34 MB（不含模型）
- **内存优化**：~400 MB（含模型），相比 WPF 版 ~500 MB
- **Apple Design UI**：全新设计语言，白色卡片 + #f2f2f7 分组背景 + SVG 图标
- **原生浮动指示器**：Win32 GDI+ Layered Window，无焦点抢夺

### 新功能

- **两种输入模式**：长按说话 + 短按自由说话（WPF 版仅长按）
- **录音音效反馈**：马林巴风格双音提示（开始升调、完成降调），可在设置中关闭
- **Escape 取消录音**：录音中按 Esc 立即取消，比组合键更直觉
- **剪贴板保护**：粘贴前保存旧剪贴板内容，粘贴后自动恢复
- **麦克风设备选择**：下拉菜单列出所有输入设备，点击切换，选择自动保存
- **中英双语界面**：自动跟随系统语言，托盘菜单同步本地化
- **自定义快捷键**：支持 Ctrl / Alt / Shift / 功能键 / 字母键 / 空格等

### 技术细节

| 模块 | 实现 |
|---|---|
| 音频录制 | malgo (miniaudio)，16kHz/16-bit/mono PCM |
| 语音识别 | sherpa-onnx SenseVoice，DirectML GPU → CPU 自动回退 |
| 全局热键 | GetAsyncKeyState 30ms 轮询，支持长按/短按/组合键过滤 |
| 文字粘贴 | Win32 剪贴板 + SendInput Ctrl+V，失败回退 Unicode SendInput |
| 浮动指示器 | Win32 GDI+ Layered Window，WS_EX_NOACTIVATE + TOPMOST |
| 音效合成 | Go 程序化生成 WAV (48kHz, 基频+谐波+指数衰减)，winmm.dll PlaySound |
| 前端 | Svelte 5 + Vite 6 + TypeScript，~80 KB 产物 |
| 配置 | JSON 格式，兼容 WPF 版 config.json |

### 与 v1.x WPF 版对比

| | v2.0 Go | v1.3.2 WPF |
|---|---|---|
| 运行文件体积 | ~34 MB | ~80 MB |
| 运行内存 | ~400 MB | ~500 MB |
| 自由说话模式 | 有 | 无 |
| 录音音效 | 有（可关闭） | 无 |
| Esc 取消 | 有 | 无 |
| 剪贴板保护 | 有 | 无 |
| 麦克风切换 | 有 | 无 |
| 多语言 UI | 中/英 | 中/英 |
| 跨平台 | 计划中 | 仅 Windows |

---

## v1.3.2 (2026-01-21) — WPF 版

- 全链路异步化：识别+粘贴移至后台线程，彻底消除 UI 卡顿
- 彻底移除 Python 依赖：纯原生 C# + C++ 引擎
- 原生剪贴板重构：Win32 API，解决 CLIPBRD_E_CANT_OPEN
- 新增"模拟打字"模式：解决同花顺等终端无法粘贴
- 焦点保护：WS_EX_NOACTIVATE
- 自动集成 VC++ Redistributable DLL

## v1.3.1 (2026-01-02) — WPF 版

- 修复剪贴板偶发锁定
- 修复 Ctrl 键与粘贴操作冲突
- 修复 ONNX Stream 内存泄漏
- 新增安全粘贴机制
- 新增空闲 30 秒自动内存回收

## v1.3.0 — WPF 版

- 新增 DirectML 硬件加速
- 新增应用内自动更新
- 新增 VAD 能量检测
- 新增音频设备热插拔支持
- 新增双地址模型下载备份
