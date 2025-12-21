using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
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
        private const int VK_CONTROL = 0x11;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        
        // Python 后端服务
        private Process? _pythonProcess;
        private readonly HttpClient _httpClient;
        private const string BackendUrl = "http://127.0.0.1:8765";
        private bool _backendReady = false;

        // 浮动指示器
        private readonly FloatingIndicator _indicator;
        
        // 原生引擎
        private AsrEngine? _nativeEngine;
        private bool _useNativeEngine = false;
        
        // 音频录制器
        private readonly AudioRecorder _audioRecorder;

        // 状态
        private bool _hotkeyActive = false;
        private bool _isRecording = false;
        private System.Windows.Threading.DispatcherTimer? _ctrlStateTimer;
        
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
            try
            {
                App.Log("MainWindow 启动中...");
                InitializeComponent();

                // 加载配置
                LoadConfig();

                _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(600) }; // 增加超时时间用于大文件下载
                _indicator = new FloatingIndicator();
                
                _audioRecorder = new AudioRecorder();
                _audioRecorder.VolumeUpdated += volume =>
                {
                    Dispatcher.BeginInvoke(() => _indicator?.UpdateVolume(volume));
                };
                _audioRecorder.DeviceChanged += () =>
                {
                    Dispatcher.BeginInvoke(() => {
                        DeviceLabel.Text = "输入设备: " + _audioRecorder.GetDeviceName();
                        App.Log("检测到默认音频设备变更: " + DeviceLabel.Text);
                    });
                };

                LoadIcon();

                // 启动永久状态轮询
                StartPermanentCtrlTimer();

                // 尝试初始化原生引擎 (如果存在模型)
                InitializeNativeEngine();

                // 注意：StartPythonBackend 现在由 InitializeNativeEngine 在失败时触发，
                // 不再在构造函数中直接启动，以避免竞争状态。

                DeviceLabel.Text = "输入设备: " + _audioRecorder.GetDeviceName();
                string initialKey = GetKeyName(_currentHotkeyVK);
                TrayIcon.ToolTipText = $"VoiceSnap 语闪 - 长按 {initialKey} 说话";
                _indicator.SetHotkeyName(initialKey);
                HotkeyLabel.Text = initialKey;
                
                // 检查开机启动状态
                CheckStartupStatus();

                // 设置版本号
                VersionLabel.Text = $"版本 {GetCurrentVersion()}";

                // 清理旧版本文件
                CleanupOldVersion();

                // 后台检查更新
                _ = CheckForUpdateAsync();

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
                if (!_backendReady || _isRecordingHotkey) return;

                // 物理检测自定义按键状态
                bool isKeyDown = (GetAsyncKeyState(_currentHotkeyVK) & 0x8000) != 0;

                if (isKeyDown && !_hotkeyActive)
                {
                    _hotkeyActive = true;
                    StartRecording();
                }
                else if (!isKeyDown && _hotkeyActive)
                {
                    _hotkeyActive = false;
                    StopRecording();
                }
            };
            _ctrlStateTimer.Start();
            App.Log("Ctrl 状态轮询定时器已启动");
        }

        private void StartRecording()
        {
            if (_isRecording) return;
            _isRecording = true;

            Dispatcher.Invoke(() => {
                _indicator?.ShowIndicator(FloatingIndicator.IndicatorStatus.Recording);
                UpdateRecordingStatus("🔴 录音中...", "Red");
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

        private async void StopRecording()
        {
            if (!_isRecording) return;
            _isRecording = false;

            Dispatcher.Invoke(() => {
                UpdateRecordingStatus("⌛ 正在识别...", "Orange");
                _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Processing);
            });

            try
            {
                if (_useNativeEngine && _nativeEngine != null)
                {
                    bool hasVoice = _audioRecorder.HasVoiceActivity();
                    byte[]? rawData = _audioRecorder.StopRecordingRaw();

                    if (!hasVoice)
                    {
                        Dispatcher.Invoke(() => {
                            _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                            UpdateRecordingStatus("✓ 未检测到语音", "Orange");
                            if (AutoHideCheckbox.IsChecked == true) _indicator?.DelayedHide(1500);
                        });
                        return;
                    }

                    if (rawData != null && rawData.Length > 0)
                    {
                        float[] samples = BytesToFloats(rawData);
                        string text = _nativeEngine.Recognize(samples);
                        
                        if (!string.IsNullOrEmpty(text))
                        {
                            Dispatcher.Invoke(() => {
                                Clipboard.SetText(text);
                                System.Windows.Forms.SendKeys.SendWait("^v");
                                
                                _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                                UpdateRecordingStatus("✓ 已输入 (原生)", "Green");
                            });
                        }
                    }
                    return;
                }

                byte[] audioData = _audioRecorder.StopRecording();
                if (audioData == null || audioData.Length < 100)
                {
                    Dispatcher.Invoke(() => {
                        _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                        if (AutoHideCheckbox.IsChecked == true)
                        {
                            _indicator?.DelayedHide(1000);
                        }
                        UpdateRecordingStatus("✓ 已就绪", "Green");
                    });
                    return;
                }

                // 发送到后端识别
                var content = new ByteArrayContent(audioData);
                var response = await _httpClient.PostAsync($"{BackendUrl}/recognize", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var resultJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<RecognitionResponse>(resultJson);
                    
                    if (!string.IsNullOrEmpty(result?.text))
                    {
                        string text = result.text.Trim();
                        if (text.Length > 0)
                        {
                            Dispatcher.Invoke(() => {
                                Clipboard.SetText(text);
                                System.Windows.Forms.SendKeys.SendWait("^v");
                                
                                _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                                UpdateRecordingStatus("✓ 已输入", "Green");
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("识别过程出错", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => {
                    if (AutoHideCheckbox.IsChecked == true)
                    {
                        _indicator?.DelayedHide(2000);
                    }
                });
            }
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
                UpdateStatus("正在加载引擎...", "Orange");
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
                        _backendReady = true; // 允许热键触发
                        
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
                        UpdateStatus($"✓ 已就绪 · {_nativeEngine.ShortHardwareInfo}", "Green");
                        
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
                    if (!_isOnboarding)
                    {
                        // 原生失败且不在 Onboarding 模式，尝试回退到 Python
                        _ = StartPythonBackend();
                    }
                    else
                    {
                        Dispatcher.Invoke(() => {
                            InitStatusLabel.Text = "引擎加载失败";
                            InitDetailLabel.Text = ex.Message;
                        });
                    }
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
                
                UpdateStatus("正在初始化...", "Orange");
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
                
                Dispatcher.Invoke(() => InitStatusLabel.Text = "正在同步离线语音大脑...");
                
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

                if (!success) throw new Exception("所有下载地址均失效");

                Dispatcher.Invoke(() => {
                    InitStatusLabel.Text = "正在优化本地硬件加速 (这可能需要 1-2 分钟)...";
                    // 自定义进度条不支持 IsIndeterminate，显示满进度表示正在处理
                    InitProgressFill.Width = InitProgressBarContainer.ActualWidth;
                    InitDetailLabel.Text = "正在解压，请稍候...";
                });

                await Task.Run(() => ExtractModel(tempFile, modelsDir));

                if (File.Exists(tempFile)) File.Delete(tempFile);

                Dispatcher.Invoke(() => {
                    InitProgressFill.Width = InitProgressBarContainer.ActualWidth;
                    InitDetailLabel.Text = "初始化完成！";
                });

                await Task.Delay(1000);
                InitializeNativeEngine();
            }
            catch (Exception ex)
            {
                App.LogError("初始化流程失败", ex);
                Dispatcher.Invoke(() => {
                    InitStatusLabel.Text = "初始化失败";
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

        private async Task StartPythonBackend()
        {
            UpdateStatus("正在启动后端...", "Orange");
            Dispatcher.Invoke(() => {
                _indicator?.ShowIndicator(FloatingIndicator.IndicatorStatus.Loading);
            });
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string pythonScript = Path.Combine(baseDir, "asr_service.py");
                
                if (!File.Exists(pythonScript))
                {
                    pythonScript = Path.Combine(baseDir, "PythonBackend", "asr_service.py");
                }

                if (!File.Exists(pythonScript))
                {
                    App.Log("未找到 Python 脚本，使用模拟模式");
                    OnBackendReady();
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{pythonScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = Path.GetDirectoryName(pythonScript)
                };

                _pythonProcess = new Process { StartInfo = startInfo };
                _pythonProcess.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null && e.Data.Contains("Backend ready"))
                    {
                        Dispatcher.Invoke(OnBackendReady);
                    }
                };
                
                _pythonProcess.Start();
                _pythonProcess.BeginOutputReadLine();
                
                await WaitForBackend();
            }
            catch (Exception ex)
            {
                App.LogError("启动后端失败", ex);
                OnBackendReady();
            }
        }

        private async Task WaitForBackend()
        {
            for (int i = 0; i < 60; i++)
            {
                try
                {
                    var response = await _httpClient.GetStringAsync($"{BackendUrl}/health");
                    if (response.Contains("\"model_loaded\":true"))
                    {
                        OnBackendReady();
                        return;
                    }
                }
                catch { }
                await Task.Delay(1000);
            }
        }

        private void OnBackendReady()
        {
            if (_backendReady) return;
            _backendReady = true;
            UpdateStatus("✓ 模型已就绪", "Green");
            Dispatcher.Invoke(() => {
                _indicator?.ShowIndicator(FloatingIndicator.IndicatorStatus.Ready);
                if (AutoHideCheckbox.IsChecked == true)
                {
                    _indicator?.DelayedHide(2000);
                }
            });
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
            HotkeyHint.Text = "请按下键盘上的任意键...";
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
                    HotkeyHint.Text = "快捷键已更新。";
                    HotkeyHint.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
                    TrayIcon.ToolTipText = $"VoiceSnap 语闪 - 长按 {keyName} 说话";
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
                case 0xA2: return "左 Ctrl";
                case 0xA3: return "右 Ctrl";
                case 0x12: return "Alt";
                case 0xA4: return "左 Alt";
                case 0xA5: return "右 Alt";
                case 0x10: return "Shift";
                case 0xA0: return "左 Shift";
                case 0xA1: return "右 Shift";
                case 0x14: return "Caps Lock";
                case 0x20: return "空格";
                case 0x09: return "Tab";
                case 0x0D: return "回车";
                case 0x5B: return "左 Win";
                case 0x5C: return "右 Win";
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
                string cleanText = text.Replace("✓ ", "").Replace("模型状态: ", "").Replace("🔴 ", "").Replace("⌛ ", "").Replace("✓", "");
                StatusLabel.Text = cleanText.Trim();
                var color = (Color)ColorConverter.ConvertFromString(colorName);
                StatusDot.Fill = new SolidColorBrush(color);
            });
        }

        private void UpdateRecordingStatus(string text, string colorName)
        {
            Dispatcher.Invoke(() =>
            {
                if (text.Contains("录音")) StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 59, 48)); // Red
                else if (text.Contains("识别")) StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 149, 0)); // Orange
                else if (_backendReady) StatusDot.Fill = new SolidColorBrush(Color.FromRgb(52, 199, 89)); // Green
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
                    _indicator?.ShowIndicator(_backendReady ? FloatingIndicator.IndicatorStatus.Ready : FloatingIndicator.IndicatorStatus.Loading);
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
            UpdateStatusLabel.Text = "正在检查更新...";
            UpdateStatusLabel.Visibility = Visibility.Visible;
            
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                string json = await client.GetStringAsync(VersionCheckUrl);
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json);

                if (versionInfo == null || string.IsNullOrEmpty(versionInfo.version))
                {
                    UpdateStatusLabel.Text = "版本信息获取失败";
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
                    UpdateStatusLabel.Text = "✓ 当前已是最新版本";
                    UpdateStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(52, 199, 89)); // Green
                }
            }
            catch (Exception ex)
            {
                App.Log($"手动检查更新失败: {ex.Message}");
                UpdateStatusLabel.Text = "检查更新失败，请稍后重试";
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
            if (_pythonProcess != null && !_pythonProcess.HasExited)
            {
                try { _pythonProcess.Kill(); } catch { }
            }
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
                    System.Windows.MessageBox.Show("无法获取当前程序路径", "更新失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string tempExe = Path.Combine(Path.GetTempPath(), "VoiceSnap_new.exe");
                string oldExe = currentExe + ".old";

                // 显示下载进度
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("正在下载更新...", "Orange");
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
                            UpdateStatus($"正在下载更新 {progress}%...", "Orange");
                        });
                    }
                }

                fileStream.Close();

                // 验证下载的文件
                if (!File.Exists(tempExe) || new FileInfo(tempExe).Length < 1024 * 100) // 至少 100KB
                {
                    System.Windows.MessageBox.Show("下载的文件无效", "更新失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("正在应用更新...", "Orange");
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
                    System.Windows.MessageBox.Show($"更新失败: {ex.Message}", "更新错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatus("✓ 已就绪", "Green");
                });
            }
        }
    }
}
