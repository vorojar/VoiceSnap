using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace VoiceSnap
{
    public partial class UpdateDialog : Window
    {
        public bool UserWantsUpdate { get; private set; } = false;
        public string DownloadUrl { get; set; } = "";

        private string _version = "";

        public string Version
        {
            get => _version;
            set 
            { 
                _version = value;
                VersionText.Text = $"v{value}"; 
            }
        }

        public string ReleaseNotes
        {
            set
            {
                // 将换行符替换为项目符号格式
                string notes = value.Trim();
                if (!notes.StartsWith("•") && !notes.StartsWith("-"))
                {
                    // 如果不是列表格式，按换行分割并添加项目符号
                    var lines = notes.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    notes = string.Join("\n", Array.ConvertAll(lines, line => 
                        line.TrimStart().StartsWith("•") || line.TrimStart().StartsWith("-") 
                            ? line.Trim() 
                            : "• " + line.Trim()));
                }
                ReleaseNotesText.Text = notes;
            }
        }


        public UpdateDialog()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            UserWantsUpdate = false;
            this.Close();
        }

        private void LaterButton_Click(object sender, RoutedEventArgs e)
        {
            UserWantsUpdate = false;
            this.Close();
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(DownloadUrl))
            {
                UserWantsUpdate = true;
                this.Close();
                return;
            }

            // 切换到进度界面
            ButtonPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            CloseButton.IsEnabled = false;

            try
            {
                await PerformUpdateAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ButtonPanel.Visibility = Visibility.Visible;
                ProgressPanel.Visibility = Visibility.Collapsed;
                CloseButton.IsEnabled = true;
            }
        }

        private async Task PerformUpdateAsync()
        {
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(currentExe))
            {
                throw new Exception("无法获取当前程序路径");
            }

            string tempExe = Path.Combine(Path.GetTempPath(), "VoiceSnap_new.exe");
            string oldExe = currentExe + ".old";

            // 下载新版本
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
            using var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
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
                    int percent = (int)(totalRead * 100 / totalBytes);
                    Dispatcher.Invoke(() =>
                    {
                        ProgressPercent.Text = $"{percent}%";
                        double containerWidth = ProgressBarContainer.ActualWidth;
                        if (containerWidth > 0)
                        {
                            ProgressFill.Width = containerWidth * percent / 100;
                        }
                    });
                }
            }

            fileStream.Close();

            // 验证下载的文件
            if (!File.Exists(tempExe) || new FileInfo(tempExe).Length < 1024 * 100)
            {
                throw new Exception("下载的文件无效");
            }

            Dispatcher.Invoke(() =>
            {
                ProgressText.Text = "正在应用更新...";
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
                System.Windows.Application.Current.Shutdown();
            });
        }

        // 允许拖动窗口
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }
}
