using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

// 避免与 WinForms 命名空间冲突
using Color = System.Windows.Media.Color;
using Clipboard = System.Windows.Clipboard;
using Application = System.Windows.Application;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using ColorConverter = System.Windows.Media.ColorConverter;
using RadioButton = System.Windows.Controls.RadioButton;

namespace VoiceSnap
{
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
        
        // 音频录制器
        private readonly AudioRecorder _audioRecorder;

        // 状态
        private bool _hotkeyActive = false;
        private bool _isRecording = false;
        private System.Windows.Threading.DispatcherTimer? _ctrlStateTimer;
        
        // 自定义快捷键
        private int _currentHotkeyVK = 0x11; // 默认 VK_CONTROL
        private bool _isRecordingHotkey = false;

        public MainWindow()
        {
            try
            {
                App.Log("MainWindow 启动中...");
                InitializeComponent();

                _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                _indicator = new FloatingIndicator();
                
                _audioRecorder = new AudioRecorder();
                _audioRecorder.VolumeUpdated += volume =>
                {
                    Dispatcher.BeginInvoke(() => _indicator?.UpdateVolume(volume));
                };

                LoadIcon();

                // 启动时显示加载指示器
                _indicator.ShowIndicator(FloatingIndicator.IndicatorStatus.Loading);

                // 启动永久状态轮询
                StartPermanentCtrlTimer();

                // 启动后端
                _ = StartPythonBackend();

                DeviceLabel.Text = "输入设备: " + _audioRecorder.GetDeviceName();
                string initialKey = GetKeyName(_currentHotkeyVK);
                TrayIcon.ToolTipText = $"VoiceSnap 语闪 - 长按 {initialKey} 说话";
                _indicator.SetHotkeyName(initialKey);
                
                // 检查开机启动状态
                CheckStartupStatus();

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
                byte[] audioData = _audioRecorder.StopRecording();
                if (audioData == null || audioData.Length < 100)
                {
                    Dispatcher.Invoke(() => {
                        _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                        _indicator?.DelayedHide(1000);
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

        private async Task StartPythonBackend()
        {
            UpdateStatus("正在启动后端...", "Orange");
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
                _indicator?.SetStatus(FloatingIndicator.IndicatorStatus.Ready);
                _indicator?.DelayedHide(2000);
            });
        }

        private void LoadIcon()
        {
            try
            {
                // 使用 Pack URI 从嵌入资源加载图标
                var iconUri = new Uri("pack://application:,,,/Assets/icon.ico", UriKind.RelativeOrAbsolute);
                
                // 设置窗口图标
                Icon = new BitmapImage(iconUri);
                
                // 设置托盘图标 (从资源流读取)
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
                // 获取 Win32 虚拟键码
                int vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key);
                
                // 处理特殊键 (WPF 对某些键有特殊处理，如 System 键)
                if (e.Key == System.Windows.Input.Key.System)
                {
                    vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.SystemKey);
                }

                if (vk > 0)
                {
                    _currentHotkeyVK = vk;
                    _isRecordingHotkey = false;
                    
                    string keyName = GetKeyName(vk);
                    // 更新 UI
                    HotkeyLabel.Text = keyName;
                    HotkeyHint.Text = "快捷键已更新。";
                    HotkeyHint.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
                    
                    // 同步更新托盘提示和指示器
                    TrayIcon.ToolTipText = $"VoiceSnap 语闪 - 长按 {keyName} 说话";
                    _indicator.SetHotkeyName(keyName);
                    
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
                case 0x11: return "Control";
                case 0xA2: return "LControl";
                case 0xA3: return "RControl";
                case 0x12: return "Alt";
                case 0xA4: return "LAlt";
                case 0xA5: return "RAlt";
                case 0x10: return "Shift";
                case 0xA0: return "LShift";
                case 0xA1: return "RShift";
                case 0x14: return "Caps Lock";
                case 0x20: return "Space";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x5B: return "LWin";
                case 0x5C: return "RWin";
                case 0x1B: return "Escape";
                default:
                    // 尝试获取字符
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
            // 在侧边栏模式下，我们不再显示详细的录音状态文字，
            // 而是通过 StatusDot 的颜色变化来微弱提示
            Dispatcher.Invoke(() =>
            {
                if (text.Contains("录音")) StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 59, 48)); // Red
                else if (text.Contains("识别")) StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 149, 0)); // Orange
                else if (_backendReady) StatusDot.Fill = new SolidColorBrush(Color.FromRgb(52, 199, 89)); // Green
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            this.ShowInTaskbar = false;
            this.Visibility = Visibility.Hidden;
            Hide();
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
            // 逻辑已在 StopRecording 中通过 IsChecked 直接判断
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
                    FileName = "https://github.com/vorojar/VoiceSnap",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void CleanupAndExit()
        {
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
    }
}
