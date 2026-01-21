using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Linq;

// 避免与 WinForms 命名空间冲突
using Color = System.Windows.Media.Color;
using Clipboard = System.Windows.Clipboard;
using Application = System.Windows.Application;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using ColorConverter = System.Windows.Media.ColorConverter;
using RadioButton = System.Windows.Controls.RadioButton;
using VoiceSnap.Engine;
using Res = VoiceSnap.Properties.Resources;

namespace VoiceSnap
{
    public class AppConfig
    {
        public int HotkeyVK { get; set; } = 0x11; // 默认 Ctrl
        public bool AutoHide { get; set; } = true;
        public string ModelDownloadUrl { get; set; } = "http://www.maikami.com/voicesnap/sensevoice.zip";
        public string FallbackModelDownloadUrl { get; set; } = "https://modelscope.cn/models/sherpa-onnx/sherpa-onnx-sense-voice-zh-en-ja-ko-yue/resolve/master/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17.tar.bz2";
    }

    public partial class MainWindow : Window
    {
        // Win32 API 用于检测键盘状态
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        
        // Win32 API 用于模拟按键（不依赖消息循环）
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        
        private const int VK_CONTROL = 0x11;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const byte VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        // Win32 API for Clipboard
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        private const uint CF_UNICODETEXT = 13;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);
        private const uint GMEM_MOVEABLE = 0x0002;

        // Win32 API for SendInput
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }

        private const int INPUT_KEYBOARD = 1;

        // Window Styles
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;
        
        private readonly HttpClient _httpClient;
        
        // 浮动指示器
        private readonly FloatingIndicator _indicator;
        
        // 原生引擎
        private AsrEngine? _nativeEngine;
        private bool _useNativeEngine = false;
        
        // 音频录制器
        private readonly AudioRecorder _audioRecorder;

        // 状态
        private bool _hotkeyActive = false;
        private bool _isHotkeyCombination = false;
        private DateTime _hotkeyPressTime = DateTime.MinValue;
        private bool _isRecording = false;
        private System.Windows.Threading.DispatcherTimer? _ctrlStateTimer;
        private System.Windows.Threading.DispatcherTimer? _idleGcTimer;
        private DateTime _lastActivityTime = DateTime.Now;
        
        // 自定义快捷键
        private int _currentHotkeyVK = 0x11; // 默认 VK_CONTROL
        private bool _isRecordingHotkey = false;
        private string _modelDownloadUrl = "http://www.maikami.com/voicesnap/sensevoice.zip";
        private string _fallbackModelDownloadUrl = "https://modelscope.cn/models/sherpa-onnx/sherpa-onnx-sense-voice-zh-en-ja-ko-yue/resolve/master/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17.tar.bz2";
        private bool _isExiting = false;
        private bool _isOnboarding = false;

        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public MainWindow()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(600) }; // 增加超时时间用于大文件下载
            _indicator = new FloatingIndicator();
            _audioRecorder = new AudioRecorder();

            try
            {
                App.Log("MainWindow 启动中...");
                InitializeComponent();

                // 加载配置
                LoadConfig();
                
                _audioRecorder.VolumeUpdated += volume =>
                {
                    Dispatcher.BeginInvoke(() => _indicator?.UpdateVolume(volume));
                };
                _audioRecorder.DeviceChanged += () =>
                {
                    Dispatcher.BeginInvoke(() => {
                        DeviceLabel.Text = $"{Res.InputDevice} {_audioRecorder.GetDeviceName()}";
                        App.Log("检测到默认音频设备变更: " + DeviceLabel.Text);
                    });
                };

                LoadIcon();

                // 启动永久状态轮询
                StartPermanentCtrlTimer();

                // 启动空闲内存回收定时器
                StartIdleGcTimer();

                // 尝试初始化原生引擎 (如果存在模型)
                InitializeNativeEngine();

                // 注意：StartPythonBackend 现在由 InitializeNativeEngine 在失败时触发，
                // 不再在构造函数中直接启动，以避免竞争状态。

                DeviceLabel.Text = $"{Res.InputDevice} {_audioRecorder.GetDeviceName()}";
                string initialKey = GetKeyName(_currentHotkeyVK);
                TrayIcon.ToolTipText = string.Format(Res.TrayTooltip, initialKey);
                _indicator.SetHotkeyName(initialKey);
                HotkeyLabel.Text = initialKey;
                
                // 检查开机启动状态
                CheckStartupStatus();

                // 设置版本号
                VersionLabel.Text = $"{Res.VersionPrefix} {GetCurrentVersion()}";

                // 清理旧版本文件
                CleanupOldVersion();

                // 后台检查更新
                _ = CheckForUpdateAsync();

                // 确保指示器窗口不偷焦点
                var helper = new System.Windows.Interop.WindowInteropHelper(_indicator);
                IntPtr hWnd = helper.Handle;
                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOPMOST);

                App.Log("MainWindow 初始化完成");
            }
            catch (Exception ex)
            {
                App.LogError("MainWindow 初始化失败", ex);
            }
        }

        private void StartPermanentCtrlTimer()
        {
            _ctrlStateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            _ctrlStateTimer.Tick += (s, e) =>
            {
                if (!_useNativeEngine || _isRecordingHotkey) return;

                // 物理检测自定义按键状态
                bool isKeyDown = (GetAsyncKeyState(_currentHotkeyVK) & 0x8000) != 0;

                if (isKeyDown)
                {
                    if (!_hotkeyActive)
                    {
                        // 刚按下
                        _hotkeyActive = true;
                        _isHotkeyCombination = false;
                        _hotkeyPressTime = DateTime.Now;
                    }
                    else
                    {
                        // 持续按住中，检测是否有其他键按下（组合键判定）
                        if (!_isHotkeyCombination && IsAnyOtherKeyPressed())
                        {
                            _isHotkeyCombination = true;
                            if (_isRecording)
                            {
                                StopRecording(cancel: true);
                            }
                        }

                        // 如果按住超过 300ms 且不是组合键，且还没开始录音，则开始录音
                        if (!_isRecording && !_isHotkeyCombination && (DateTime.Now - _hotkeyPressTime).TotalMilliseconds > 300)
                        {
                            StartRecording();
                        }
                    }
                }
                else if (_hotkeyActive)
                {
                    // 刚松开
                    _hotkeyActive = false;
                    if (_isRecording)
                    {
                        StopRecording(cancel: _isHotkeyCombination);
                    }
                }
            };
            _ctrlStateTimer.Start();
            App.Log("Ctrl 状态轮询定时器已启动 (支持组合键避让)");
        }

        /// <summary>
        /// 检测是否有除当前热键以外的按键被按下
        /// </summary>
        private bool IsAnyOtherKeyPressed()
        {
            // 只检查有意义的组合键：A-Z, 0-9, 以及常用功能键
            // 这样可以避开系统内部的一些虚拟按键干扰
            int[] checkKeys = {
                0x08, 0x09, 0x0D, 0x1B, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2C, 0x2D, 0x2E,
                0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
                0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A,
                0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B,
                0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE
            };

            foreach (int i in checkKeys)
            {
                if (i == _currentHotkeyVK) continue;
                if ((GetAsyncKeyState(i) & 0x8000) != 0) return true;
            }
            
            // 还要检查其他的修饰键，如果热键是 Ctrl，那么按下 Alt 或 Shift 也算组合键
            int[] ctrlKeys = { 0x11, 0xA2, 0xA3 };
            int[] altKeys = { 0x12, 0xA4, 0xA5 };
            int[] shiftKeys = { 0x10, 0xA0, 0xA1 };

            if (!ctrlKeys.Contains(_currentHotkeyVK)) {
                if (ctrlKeys.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0)) return true;
            }
            if (!altKeys.Contains(_currentHotkeyVK)) {
                if (altKeys.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0)) return true;
            }
            if (!shiftKeys.Contains(_currentHotkeyVK)) {
                if (shiftKeys.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0)) return true;
            }

            return false;
        }

        /// <summary>
        /// 启动空闲内存回收定时器，30秒无操作后静默释放内存
        /// </summary>
        private void StartIdleGcTimer()
        {
            _idleGcTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10) // 每 10 秒检查一次
            };
            _idleGcTimer.Tick += (s, e) =>
            {
                // 30 秒无活动时静默触发内存回收
                if ((DateTime.Now - _lastActivityTime).TotalSeconds > 30)
                {
                    _lastActivityTime = DateTime.Now; // 重置，避免连续触发
                    Task.Run(() =>
                    {
                        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
                        GC.WaitForPendingFinalizers();
                        // 静默执行，不记录日志
                    });
                }
            };
            _idleGcTimer.Start();
        }

        private void StartRecording()
        {
            if (_isRecording) return;
            _isRecording = true;
            _lastActivityTime = DateTime.Now; // 更新活动时间，用于空闲 GC 计时

            Dispatcher.Invoke(() => {
                _indicator?.ShowIndicator(FloatingIndicator.IndicatorStatus.Recording);
                UpdateRecordingStatus($"🔴 {Res.StatusRecording}", "Red");
            });

            Task.Run(() =>
            {
                try
                {
                    _audioRecorder.StartRecording();
                }
                catch (Exception ex)
                {
                    App.LogError("启动录音失败", ex);
                }
            });
        }

        private void StopRecording(bool cancel = false)
        {
            if (!_isRecording) return;
            _isRecording = false;

            if (cancel)
            {
                _audioRecorder.StopRecordingRaw(); // 停止并丢弃数据
                Dispatcher.Invoke(() => {
                    _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                    UpdateRecordingStatus($"✓ {Res.StatusCancelled}", "Orange");
                    if (AutoHideCheckbox.IsChecked == true) _indicator?.DelayedHide(1000);
                });
                return;
            }

            // 先在UI线程采集必要数据
            bool hasVoice = _audioRecorder.HasVoiceActivity();
            byte[]? rawData = _audioRecorder.StopRecordingRaw();
            bool autoHide = false;
            Dispatcher.Invoke(() => {
                autoHide = AutoHideCheckbox.IsChecked == true;
                UpdateRecordingStatus($"⌛ {Res.StatusRecognizing}", "Orange");
                _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Processing);
            });

            // 全链路异步：识别 + 粘贴 都在后台线程执行，UI绝不阻塞
            Task.Run(() =>
            {
                try
                {
                    if (_useNativeEngine && _nativeEngine != null)
                    {
                        if (!hasVoice)
                        {
                            Dispatcher.Invoke(() => {
                                _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                                UpdateRecordingStatus($"✓ {Res.StatusNoVoice}", "Orange");
                                if (autoHide) _indicator?.DelayedHide(1500);
                            });
                            return;
                        }

                        if (rawData != null && rawData.Length > 0)
                        {
                            float[] samples = BytesToFloats(rawData);
                            string text = _nativeEngine.Recognize(samples); // 耗时操作，现在在后台线程

                            if (!string.IsNullOrEmpty(text))
                            {
                                SafePasteText(text);
                                Dispatcher.Invoke(() => {
                                    _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                                    UpdateRecordingStatus($"✓ {Res.StatusInputDone}", "Green");
                                });
                            }
                            else
                            {
                                Dispatcher.Invoke(() => {
                                    _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                                    UpdateRecordingStatus($"✓ {Res.StatusNoContent}", "Orange");
                                });
                            }
                        }
                        return;
                    }

                    // 如果没有原生引擎
                    Dispatcher.Invoke(() => {
                        _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                        UpdateRecordingStatus($"✓ {Res.StatusEngineNotReady}", "Red");
                    });
                }
                catch (Exception ex)
                {
                    App.LogError("识别过程出错", ex);
                    Dispatcher.Invoke(() => {
                        _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                        UpdateRecordingStatus($"✗ {Res.StatusError}", "Red");
                    });
                }
                finally
                {
                    Dispatcher.Invoke(() => {
                        if (autoHide)
                        {
                            _indicator?.DelayedHide(2000);
                        }
                    });
                }
            });
        }

        private void InitializeNativeEngine()
        {
            string modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "sensevoice");
            string modelPath = Path.Combine(modelDir, "model.int8.onnx");
            if (!File.Exists(modelPath)) modelPath = Path.Combine(modelDir, "model.onnx");
            string tokensPath = Path.Combine(modelDir, "tokens.txt");

            // 1. 首先检查模型是否存在 (同步检查，极快)
            if (!File.Exists(modelPath) || !File.Exists(tokensPath))
            {
                App.Log("未找到原生模型文件，进入初始化模式");
                ShowOnboarding();
                return;
            }

            // 2. 模型存在，立即显示加载状态
            Dispatcher.Invoke(() => {
                _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Loading);
                _indicator?.ShowIndicator(FloatingIndicator.IndicatorStatus.Loading);
                UpdateStatus(Res.StatusLoading, "Orange");
            });

            // 3. 在后台线程执行沉重的初始化工作
            Task.Run(() =>
            {
                try
                {
                    var engine = new AsrEngine();
                    engine.Initialize(modelPath, tokensPath);
                    
                    _nativeEngine = engine;
                    _useNativeEngine = true;
                    App.Log("原生引擎初始化成功");
                    
                    Dispatcher.Invoke(() => {
                        if (_isOnboarding)
                        {
                            System.Media.SystemSounds.Asterisk.Play();
                            InitialView.Visibility = Visibility.Collapsed;
                            this.ShowInTaskbar = false;
                            this.Hide();
                            NavGeneral.IsEnabled = true;
                            NavHotkeys.IsEnabled = true;
                            NavAbout.IsEnabled = true;
                        }

                        _isOnboarding = false;
                        UpdateStatus($"✓ {Res.StatusReady} · {_nativeEngine.ShortHardwareInfo}", "Green");
                        
                        // 在关于页面显示详细硬件信息
                        Dispatcher.Invoke(() => {
                            EngineModeLabel.Text = _nativeEngine.HardwareInfo;
                        });
                        
                        _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                        _indicator?.ShowIndicator(FloatingIndicator.IndicatorStatus.Ready);

                        if (AutoHideCheckbox.IsChecked == true)
                        {
                            _indicator?.DelayedHide(2000);
                        }
                    });
                }
                catch (Exception ex)
                {
                    App.LogError("原生引擎初始化失败", ex);
                    Dispatcher.Invoke(() => {
                        InitStatusLabel.Text = Res.EngineFailed;
                        InitDetailLabel.Text = ex.Message;
                    });
                }
            });
        }

        private void ShowOnboarding()
        {
            if (_isOnboarding) return;
            _isOnboarding = true;

            Dispatcher.Invoke(() => {
                this.Visibility = Visibility.Visible;
                this.ShowInTaskbar = true;
                this.Show();
                this.Activate();

                InitialView.Visibility = Visibility.Visible;
                NavGeneral.IsEnabled = false;
                NavHotkeys.IsEnabled = false;
                NavAbout.IsEnabled = false;
                
                UpdateStatus(Res.StatusInitializing, "Orange");
                _indicator?.Hide(); // 初始化期间隐藏指示器
                
                _ = StartOnboardingAsync();
            });
        }

        private async Task StartOnboardingAsync()
        {
            try
            {
                string modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
                if (!Directory.Exists(modelsDir)) Directory.CreateDirectory(modelsDir);

                string tempFile = Path.Combine(modelsDir, "model_package.tar.bz2");
                
                Dispatcher.Invoke(() => InitStatusLabel.Text = Res.InitSyncModel);

                bool success = false;
                try
                {
                    App.Log($"尝试从主地址下载: {_modelDownloadUrl}");
                    await DownloadFileWithProgressAsync(_modelDownloadUrl, tempFile);
                    success = true;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    App.Log("主地址 404，尝试备用地址...");
                    await DownloadFileWithProgressAsync(_fallbackModelDownloadUrl, tempFile);
                    success = true;
                }
                catch (Exception ex)
                {
                    App.Log($"主地址下载失败 ({ex.Message})，尝试备用地址...");
                    await DownloadFileWithProgressAsync(_fallbackModelDownloadUrl, tempFile);
                    success = true;
                }

                if (!success) throw new Exception(Res.DownloadFailed);

                Dispatcher.Invoke(() => {
                    InitStatusLabel.Text = Res.InitOptimizing;
                    // 自定义进度条不支持 IsIndeterminate，显示满进度表示正在处理
                    InitProgressFill.Width = InitProgressBarContainer.ActualWidth;
                    InitDetailLabel.Text = Res.InitExtracting;
                });

                await Task.Run(() => ExtractModel(tempFile, modelsDir));

                if (File.Exists(tempFile)) File.Delete(tempFile);

                Dispatcher.Invoke(() => {
                    InitProgressFill.Width = InitProgressBarContainer.ActualWidth;
                    InitDetailLabel.Text = Res.InitComplete;
                });

                await Task.Delay(1000);
                InitializeNativeEngine();
            }
            catch (Exception ex)
            {
                App.LogError("初始化流程失败", ex);
                Dispatcher.Invoke(() => {
                    InitStatusLabel.Text = Res.InitFailed;
                    InitDetailLabel.Text = ex.Message;
                    InitProgressFill.Background = System.Windows.Media.Brushes.Red;
                });
            }
        }

        private async Task DownloadFileWithProgressAsync(string url, string destinationPath)
        {
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    var isMoreToRead = true;

                    var lastUpdate = DateTime.MinValue;
                    do
                    {
                        var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            if (canReportProgress && (DateTime.Now - lastUpdate).TotalMilliseconds > 100)
                            {
                                lastUpdate = DateTime.Now;
                                var progress = (double)totalRead / totalBytes * 100;
                                Dispatcher.BeginInvoke(new Action(() => {
                                    InitProgressFill.Width = InitProgressBarContainer.ActualWidth * progress / 100;
                                    InitDetailLabel.Text = $"{progress:F1}% ({totalRead / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB)";
                                }));
                            }
                        }
                    } while (isMoreToRead);
                    
                    // 最后确保更新到 100%
                    if (canReportProgress)
                    {
                        Dispatcher.Invoke(() => {
                            InitProgressFill.Width = InitProgressBarContainer.ActualWidth;
                            InitDetailLabel.Text = $"100% ({totalBytes / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB)";
                        });
                    }
                }
            }
        }

        private void ExtractModel(string archivePath, string destinationDir)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "tar.exe",
                Arguments = $"-xf \"{archivePath}\" -C \"{destinationDir}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process != null)
                {
                    try { process.PriorityClass = ProcessPriorityClass.High; } catch { }
                    process.WaitForExit();
                }
                
                if (process?.ExitCode != 0)
                {
                    string error = process?.StandardError.ReadToEnd() ?? "未知错误";
                    throw new Exception($"解压失败 (ExitCode: {process?.ExitCode}): {error}");
                }
            }

            string extractedDir = Directory.GetDirectories(destinationDir)
                .FirstOrDefault(d => Path.GetFileName(d).StartsWith("sherpa-onnx-sense-voice"));

            if (extractedDir != null)
            {
                string targetDir = Path.Combine(destinationDir, "sensevoice");
                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
                Directory.Move(extractedDir, targetDir);
            }
        }

        private float[] BytesToFloats(byte[] bytes)
        {
            float[] floats = new float[bytes.Length / 2];
            for (int i = 0; i < floats.Length; i++)
            {
                short sample = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                floats[i] = sample / 32768f;
            }
            return floats;
        }

        /// <summary>
        /// 使用 Win32 API 写入剪贴板，比 WPF 原生更稳定
        /// </summary>
        private bool Win32SetClipboard(string text)
        {
            for (int i = 0; i < 10; i++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        EmptyClipboard();
                        IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)((text.Length + 1) * 2));
                        if (hGlobal == IntPtr.Zero) return false;

                        IntPtr lpString = GlobalLock(hGlobal);
                        if (lpString != IntPtr.Zero)
                        {
                            Marshal.Copy(text.ToCharArray(), 0, lpString, text.Length);
                            Marshal.WriteInt16(lpString, text.Length * 2, 0); // Null terminator
                            GlobalUnlock(hGlobal);
                            SetClipboardData(CF_UNICODETEXT, hGlobal);
                        }
                        return true;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                System.Threading.Thread.Sleep(50);
            }
            return false;
        }

        /// <summary>
        /// 使用 SendInput 模拟打字，彻底绕过剪贴板
        /// </summary>
        private void NativeType(string text)
        {
            var inputs = new INPUT[text.Length * 2];
            for (int i = 0; i < text.Length; i++)
            {
                // Key Down
                inputs[i * 2] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = text[i],
                            dwFlags = KEYEVENTF_UNICODE,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
                // Key Up
                inputs[i * 2 + 1] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = text[i],
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
            }
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// 安全粘贴：等待用户物理松开按键后再执行
        /// 策略：先剪贴板，失败自动降级模拟打字
        /// </summary>
        private void SafePasteText(string text)
        {
            // 1. 等待用户物理松开触发键（最多等 500ms）
            for (int i = 0; i < 50; i++)
            {
                if ((GetAsyncKeyState(_currentHotkeyVK) & 0x8000) == 0) break;
                Thread.Sleep(10);
            }
            Thread.Sleep(50);

            // 2. 先尝试剪贴板
            if (Win32SetClipboard(text))
            {
                var inputs = new INPUT[4];
                inputs[0] = CreateKeyInput(VK_CONTROL, 0);
                inputs[1] = CreateKeyInput(VK_V, 0);
                inputs[2] = CreateKeyInput(VK_V, KEYEVENTF_KEYUP);
                inputs[3] = CreateKeyInput(VK_CONTROL, KEYEVENTF_KEYUP);
                SendInput(4, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            else
            {
                // 3. 剪贴板失败，降级模拟打字
                NativeType(text);
            }
        }

        private INPUT CreateKeyInput(ushort vk, uint flags)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = flags,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        private void LoadIcon()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/icon.ico", UriKind.RelativeOrAbsolute);
                Icon = new BitmapImage(iconUri);
                var resourceStream = Application.GetResourceStream(iconUri);
                if (resourceStream != null)
                {
                    using (var stream = resourceStream.Stream)
                    {
                        TrayIcon.Icon = new System.Drawing.Icon(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"加载嵌入图标失败: {ex.Message}");
            }
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                PageGeneral.Visibility = rb == NavGeneral ? Visibility.Visible : Visibility.Collapsed;
                PageHotkeys.Visibility = rb == NavHotkeys ? Visibility.Visible : Visibility.Collapsed;
                PageAbout.Visibility = rb == NavAbout ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void HotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingHotkey = true;
            HotkeyLabel.Text = "...";
            HotkeyHint.Text = Res.PressAnyKey;
            HotkeyHint.Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 255));
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (_isRecordingHotkey)
            {
                int vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key);
                if (e.Key == System.Windows.Input.Key.System)
                {
                    vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.SystemKey);
                }

                if (vk > 0)
                {
                    _currentHotkeyVK = vk;
                    _isRecordingHotkey = false;
                    string keyName = GetKeyName(vk);
                    HotkeyLabel.Text = keyName;
                    HotkeyHint.Text = Res.HotkeyUpdated;
                    HotkeyHint.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
                    TrayIcon.ToolTipText = string.Format(Res.TrayTooltip, keyName);
                    _indicator.SetHotkeyName(keyName);
                    SaveConfig();
                    e.Handled = true;
                    return;
                }
            }
            base.OnPreviewKeyDown(e);
        }

        private string GetKeyName(int vk)
        {
            switch (vk)
            {
                case 0x11: return "Ctrl";
                case 0xA2: return "L-Ctrl";
                case 0xA3: return "R-Ctrl";
                case 0x12: return "Alt";
                case 0xA4: return "L-Alt";
                case 0xA5: return "R-Alt";
                case 0x10: return "Shift";
                case 0xA0: return "L-Shift";
                case 0xA1: return "R-Shift";
                case 0x14: return "Caps Lock";
                case 0x20: return Res.KeySpace;
                case 0x09: return "Tab";
                case 0x0D: return Res.KeyEnter;
                case 0x5B: return "L-Win";
                case 0x5C: return "R-Win";
                case 0x1B: return "Esc";
                default:
                    var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey(vk);
                    return key.ToString();
            }
        }

        private void UpdateStatus(string text, string colorName)
        {
            Dispatcher.Invoke(() =>
            {
                // 清理状态前缀符号
                string cleanText = text.Replace("✓ ", "").Replace("🔴 ", "").Replace("⌛ ", "").Replace("✓", "").Replace("✗ ", "");
                StatusLabel.Text = cleanText.Trim();
                var color = (Color)ColorConverter.ConvertFromString(colorName);
                StatusDot.Fill = new SolidColorBrush(color);
            });
        }

        private void UpdateRecordingStatus(string text, string colorName)
        {
            Dispatcher.Invoke(() =>
            {
                // 使用颜色名称判断状态，避免依赖文本内容
                if (colorName == "Red") StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                else if (colorName == "Orange") StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 149, 0));
                else if (colorName == "Green") StatusDot.Fill = new SolidColorBrush(Color.FromRgb(52, 199, 89));
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_isExiting)
            {
                e.Cancel = true;
                this.ShowInTaskbar = false;
                this.Visibility = Visibility.Hidden;
                Hide();
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Visible;
            this.ShowInTaskbar = true;
            this.WindowState = WindowState.Normal;
            this.Show();
            this.Activate();
        }

        private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.ShowInTaskbar = false;
                this.Visibility = Visibility.Hidden;
                this.Hide();
            }
            else
            {
                this.Visibility = Visibility.Visible;
                this.ShowInTaskbar = true;
                this.WindowState = WindowState.Normal;
                this.Show();
                this.Activate();
            }
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e) => CleanupAndExit();

        private void AutoHideCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                SaveConfig();
                if (AutoHideCheckbox.IsChecked == true)
                {
                    if (!_isRecording)
                    {
                        _indicator?.HideIndicator();
                    }
                }
                else
                {
                    _indicator?.ShowIndicator(_useNativeEngine ? FloatingIndicator.IndicatorStatus.Ready : FloatingIndicator.IndicatorStatus.Loading);
                }
            }
        }

        private void StartupCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                SetStartup(StartupCheckbox.IsChecked == true);
            }
        }

        private void CheckStartupStatus()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null)
                    {
                        string value = key.GetValue("VoiceSnap") as string;
                        StartupCheckbox.IsChecked = !string.IsNullOrEmpty(value);
                    }
                }
            }
            catch { }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (enable)
                    {
                        string path = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue("VoiceSnap", $"\"{path}\"");
                    }
                    else
                    {
                        key.DeleteValue("VoiceSnap", false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("设置开机启动失败: " + ex.Message);
            }
        }

        private void GithubButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.maikami.com/voicesnap/",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusLabel.Text = Res.CheckingUpdate;
            UpdateStatusLabel.Visibility = Visibility.Visible;

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                string json = await client.GetStringAsync(VersionCheckUrl);
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json);

                if (versionInfo == null || string.IsNullOrEmpty(versionInfo.version))
                {
                    UpdateStatusLabel.Text = Res.VersionInfoFailed;
                    return;
                }

                string currentVersion = GetCurrentVersion();
                App.Log($"手动检查更新 - 当前版本: {currentVersion}, 远程版本: {versionInfo.version}");

                if (CompareVersions(versionInfo.version, currentVersion) > 0)
                {
                    UpdateStatusLabel.Visibility = Visibility.Collapsed;
                    // 发现新版本，显示更新对话框
                    var dialog = new UpdateDialog
                    {
                        Version = versionInfo.version,
                        ReleaseNotes = versionInfo.releaseNotes,
                        DownloadUrl = versionInfo.downloadUrl
                    };
                    dialog.ShowDialog();
                }
                else
                {
                    UpdateStatusLabel.Text = $"✓ {Res.IsLatestVersion}";
                    UpdateStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(52, 199, 89)); // Green
                }
            }
            catch (Exception ex)
            {
                App.Log($"手动检查更新失败: {ex.Message}");
                UpdateStatusLabel.Text = Res.CheckUpdateFailed;
                UpdateStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48)); // Red
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        _currentHotkeyVK = config.HotkeyVK;
                        AutoHideCheckbox.IsChecked = config.AutoHide;
                        _modelDownloadUrl = config.ModelDownloadUrl ?? _modelDownloadUrl;
                        _fallbackModelDownloadUrl = config.FallbackModelDownloadUrl ?? _fallbackModelDownloadUrl;
                    }
                }
                else
                {
                    // 如果配置文件不存在，立即创建一个默认的
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                App.Log($"加载配置失败: {ex.Message}");
            }
        }

        private void SaveConfig()
        {
            try
            {
                var config = new AppConfig
                {
                    HotkeyVK = _currentHotkeyVK,
                    AutoHide = AutoHideCheckbox.IsChecked ?? true,
                    ModelDownloadUrl = _modelDownloadUrl,
                    FallbackModelDownloadUrl = _fallbackModelDownloadUrl
                };
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                App.Log($"保存配置失败: {ex.Message}");
            }
        }

        private void CleanupAndExit()
        {
            _isExiting = true;
            _ctrlStateTimer?.Stop();
            _indicator?.Close();
            TrayIcon.Dispose();
            Application.Current.Shutdown();
        }

        private class RecognitionResponse { public string? text { get; set; } }

        // ========== 自动更新相关 ==========

        private class VersionInfo
        {
            public string version { get; set; } = "";
            public string downloadUrl { get; set; } = "";
            public string releaseNotes { get; set; } = "";
        }

        private const string VersionCheckUrl = "http://www.maikami.com/voicesnap/version.json";

        private void CleanupOldVersion()
        {
            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string oldExe = currentExe + ".old";
                
                if (File.Exists(oldExe))
                {
                    // 延迟删除，确保旧进程完全退出
                    Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        try
                        {
                            File.Delete(oldExe);
                            App.Log("已清理旧版本文件");
                        }
                        catch (Exception ex)
                        {
                            App.Log($"清理旧版本失败: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                App.Log($"检查旧版本文件时出错: {ex.Message}");
            }
        }

        private async Task CheckForUpdateAsync()
        {
            try
            {
                // 延迟 5 秒再检查，避免影响启动速度
                await Task.Delay(5000);

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                string json = await client.GetStringAsync(VersionCheckUrl);
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json);

                if (versionInfo == null || string.IsNullOrEmpty(versionInfo.version))
                {
                    App.Log("版本信息无效");
                    return;
                }

                string currentVersion = GetCurrentVersion();
                App.Log($"当前版本: {currentVersion}, 远程版本: {versionInfo.version}");

                if (CompareVersions(versionInfo.version, currentVersion) > 0)
                {
                    // 发现新版本，在 UI 线程显示自定义对话框
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var dialog = new UpdateDialog
                            {
                                Version = versionInfo.version,
                                ReleaseNotes = versionInfo.releaseNotes,
                                DownloadUrl = versionInfo.downloadUrl
                            };
                            dialog.ShowDialog();
                            // 下载和更新现在由 UpdateDialog 内部处理
                        }
                        catch (Exception ex)
                        {
                            App.LogError("显示更新对话框失败", ex);
                        }
                    });
                }
                else
                {
                    App.Log("当前已是最新版本");
                }
            }
            catch (Exception ex)
            {
                App.Log($"检查更新失败: {ex.Message}");
            }
        }

        private string GetCurrentVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

        private int CompareVersions(string v1, string v2)
        {
            try
            {
                var parts1 = v1.Split('.').Select(int.Parse).ToArray();
                var parts2 = v2.Split('.').Select(int.Parse).ToArray();

                for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
                {
                    int p1 = i < parts1.Length ? parts1[i] : 0;
                    int p2 = i < parts2.Length ? parts2[i] : 0;
                    if (p1 != p2) return p1.CompareTo(p2);
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task PerformUpdateAsync(string downloadUrl)
        {
            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(currentExe))
                {
                    System.Windows.MessageBox.Show(Res.CannotGetPath, Res.UpdateFailed, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string tempExe = Path.Combine(Path.GetTempPath(), "VoiceSnap_new.exe");
                string oldExe = currentExe + ".old";

                // 显示下载进度
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus(Res.DownloadingUpdate, "Orange");
                });

                // 下载新版本
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempExe, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                var totalRead = 0L;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (totalBytes > 0)
                    {
                        int progress = (int)(totalRead * 100 / totalBytes);
                        Dispatcher.Invoke(() =>
                        {
                            UpdateStatus($"{Res.DownloadingUpdate} {progress}%", "Orange");
                        });
                    }
                }

                fileStream.Close();

                // 验证下载的文件
                if (!File.Exists(tempExe) || new FileInfo(tempExe).Length < 1024 * 100) // 至少 100KB
                {
                    System.Windows.MessageBox.Show(Res.InvalidDownload, Res.UpdateFailed, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    UpdateStatus(Res.ApplyingUpdate, "Orange");
                });

                // 重命名当前 exe 为 .old
                if (File.Exists(oldExe))
                {
                    File.Delete(oldExe);
                }
                File.Move(currentExe, oldExe);

                // 复制新 exe 到当前位置
                File.Copy(tempExe, currentExe, true);

                // 删除临时文件
                File.Delete(tempExe);

                // 启动新版本
                Process.Start(new ProcessStartInfo
                {
                    FileName = currentExe,
                    UseShellExecute = true
                });

                // 退出当前程序
                Dispatcher.Invoke(() =>
                {
                    CleanupAndExit();
                });
            }
            catch (Exception ex)
            {
                App.LogError("更新失败", ex);
                Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"{Res.UpdateFailed}: {ex.Message}", Res.UpdateError, MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatus($"✓ {Res.StatusReady}", "Green");
                });
            }
        }
    }
}
