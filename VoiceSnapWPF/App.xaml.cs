// 避免与 WinForms 命名空间冲突
using System;
using System.Windows.Threading;
using System.Windows;
using Application = System.Windows.Application;
using StartupEventArgs = System.Windows.StartupEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace VoiceSnap
{
    public partial class App : Application
    {
        // 是否启用文件日志 (发布版本设为 false)
        private static readonly bool EnableFileLog = true;
        private static System.Threading.Mutex? _mutex;
        
        protected override void OnStartup(StartupEventArgs e)
        {
            // 防止多开
            _mutex = new System.Threading.Mutex(true, "VoiceSnap_SingleInstance_Mutex", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("VoiceSnap 语闪 已经在运行中。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
                return;
            }

            // 设置全局异常处理
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"程序发生错误:\n{ex?.Message}", "VoiceSnap 错误");
            };
            
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"程序发生错误:\n{args.Exception.Message}", "VoiceSnap 错误");
                args.Handled = true;
            };
            
            // 手动创建主窗口但不显示，这样只会显示指示器和托盘图标
            var mainWindow = new MainWindow();
            
            base.OnStartup(e);
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
        
        public static void Log(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (EnableFileLog)
            {
                try
                {
                    string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
                    System.IO.File.AppendAllText(logPath, logEntry + Environment.NewLine);
                }
                catch { }
            }
#if DEBUG
            System.Diagnostics.Debug.WriteLine(logEntry);
#endif
        }
        
        public static void LogError(string context, Exception? ex)
        {
            string logEntry = $"[ERROR] [{DateTime.Now:HH:mm:ss}] {context}: {ex?.Message}";
            if (EnableFileLog)
            {
                try
                {
                    string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
                    System.IO.File.AppendAllText(logPath, logEntry + Environment.NewLine + ex?.StackTrace + Environment.NewLine);
                }
                catch { }
            }
            System.Diagnostics.Debug.WriteLine(logEntry);
        }
    }
}
