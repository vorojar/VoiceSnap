// 避免与 WinForms 命名空间冲突
using System;
using System.Windows.Threading;
using Application = System.Windows.Application;
using StartupEventArgs = System.Windows.StartupEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace VoiceSnap
{
    public partial class App : Application
    {
        // 是否启用文件日志 (发布版本设为 false)
        private static readonly bool EnableFileLog = false;
        
        protected override void OnStartup(StartupEventArgs e)
        {
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
            
            base.OnStartup(e);
        }
        
        public static void Log(string message)
        {
            // 发布版本禁用日志，提高性能
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
#endif
        }
        
        public static void LogError(string context, Exception? ex)
        {
            // 错误日志始终输出到 Debug
            System.Diagnostics.Debug.WriteLine($"[ERROR] {context}: {ex?.Message}");
        }
    }
}
