using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

// 避免与 WinForms 命名空间冲突
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace VoiceSnap
{
    /// <summary>
    /// 浮动录音指示器 - 完美复刻 PyQt5 版的视觉效果
    /// </summary>
    public partial class FloatingIndicator : Window
    {
        // Apple 配色
        private static readonly Color AppleBlue = Color.FromRgb(0, 122, 255);
        private static readonly Color AppleGreen = Color.FromRgb(52, 199, 89);
        private static readonly Color AppleRed = Color.FromRgb(255, 59, 48);
        private static readonly Color AppleOrange = Color.FromRgb(255, 149, 0);

        // 状态
        public enum IndicatorStatus { Loading, Ready, Recording, Processing, Done }
        private IndicatorStatus _status = IndicatorStatus.Loading;
        private string _hotkeyName = "Ctrl";

        // 动画
        private readonly DispatcherTimer _animationTimer;
        private double _animationTime = 0;
        private readonly double[] _barHeights = { 14, 14, 14, 14, 14 };
        private readonly double[] _realVolumes = { 0, 0, 0, 0, 0 };
        private readonly Border[] _bars;
        private DispatcherTimer? _hideTimer;

        public FloatingIndicator()
        {
            InitializeComponent();

            // 获取声纹条引用
            _bars = new[] { Bar1, Bar2, Bar3, Bar4, Bar5 };

            // 定位到屏幕底部中间
            PositionCenterBottom();

            // 初始化动画定时器 (60fps)
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _animationTimer.Tick += AnimationTimer_Tick;
        }

        /// <summary>
        /// 定位到屏幕底部中间
        /// </summary>
        private void PositionCenterBottom()
        {
            var screen = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            Left = (screen - Width) / 2;
            Top = screenHeight - Height - 100;
        }

        /// <summary>
        /// 设置状态
        /// </summary>
        public void SetStatus(IndicatorStatus status)
        {
            _status = status;
            UpdateStatusVisuals();
        }

        public void SetHotkeyName(string name)
        {
            _hotkeyName = name;
            if (_status == IndicatorStatus.Ready)
            {
                UpdateStatusVisuals();
            }
        }

        /// <summary>
        /// 更新状态视觉
        /// </summary>
        private void UpdateStatusVisuals()
        {
            Dispatcher.Invoke(() =>
            {
                Color barColor;
                string statusText = "";

                switch (_status)
                {
                    case IndicatorStatus.Loading:
                        barColor = AppleBlue;
                        statusText = "加载中";
                        break;
                    case IndicatorStatus.Ready:
                        barColor = AppleGreen;
                        statusText = $"按住{_hotkeyName}说话";
                        break;
                    case IndicatorStatus.Recording:
                        barColor = AppleRed;
                        statusText = ""; // 录音时不显示文字
                        break;
                    case IndicatorStatus.Processing:
                        barColor = AppleOrange;
                        statusText = "识别中";
                        break;
                    case IndicatorStatus.Done:
                        barColor = AppleGreen;
                        statusText = "完成";
                        break;
                    default:
                        barColor = AppleBlue;
                        statusText = "";
                        break;
                }

                // 更新声纹条颜色
                var brush = new SolidColorBrush(barColor);
                foreach (var bar in _bars)
                {
                    bar.Background = brush;
                }

                // 更新状态文字
                StatusText.Text = statusText;
            });
        }

        /// <summary>
        /// 更新音量数据
        /// </summary>
        public void UpdateVolume(double volume)
        {
            // 移动历史数据
            for (int i = 0; i < 4; i++)
            {
                _realVolumes[i] = _realVolumes[i + 1];
            }
            _realVolumes[4] = volume;
        }

        /// <summary>
        /// 动画帧更新
        /// </summary>
        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            _animationTime += 0.016; // 16ms per frame

            switch (_status)
            {
                case IndicatorStatus.Loading:
                    // 呼吸脉冲效果
                    for (int i = 0; i < 5; i++)
                    {
                        double pulse = Math.Sin(_animationTime * 2 + i * 0.3) * 0.5 + 0.5;
                        _barHeights[i] = 10 + pulse * 20;
                    }
                    break;

                case IndicatorStatus.Ready:
                    // 静止
                    for (int i = 0; i < 5; i++)
                    {
                        _barHeights[i] = 22;
                    }
                    break;

                case IndicatorStatus.Recording:
                    // 使用真实音量数据，如果没有数据则使用模拟动画
                    bool hasRealData = _realVolumes.Any(v => v > 0.01);
                    
                    for (int i = 0; i < 5; i++)
                    {
                        double target;
                        if (hasRealData)
                        {
                            // 使用真实音量
                            target = 8 + _realVolumes[i] * 32;
                        }
                        else
                        {
                            // 模拟动态声纹效果
                            double wave = Math.Sin(_animationTime * 4 + i * 0.8) * 0.5 + 0.5;
                            double random = Math.Sin(_animationTime * 7 + i * 1.5) * 0.3;
                            target = 10 + (wave + random) * 25;
                        }
                        // 平滑过渡
                        _barHeights[i] = _barHeights[i] * 0.6 + target * 0.4;
                    }
                    break;

                case IndicatorStatus.Processing:
                    // 优雅波浪
                    for (int i = 0; i < 5; i++)
                    {
                        double wave = Math.Sin(_animationTime * 3 + i * 0.6) * 0.5 + 0.5;
                        _barHeights[i] = 12 + wave * 18;
                    }
                    break;

                case IndicatorStatus.Done:
                    // 静止
                    for (int i = 0; i < 5; i++)
                    {
                        _barHeights[i] = 20;
                    }
                    break;
            }

            // 更新 UI
            Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < 5; i++)
                {
                    _bars[i].Height = _barHeights[i];
                }
            });
        }

        /// <summary>
        /// 显示指示器（带淡入+上滑动画）
        /// </summary>
        public void ShowIndicator(IndicatorStatus status = IndicatorStatus.Recording)
        {
            SetStatus(status);

            Dispatcher.Invoke(() =>
            {
                if (_hideTimer != null)
                {
                    _hideTimer.Stop();
                    _hideTimer = null;
                }

                var screenHeight = SystemParameters.PrimaryScreenHeight;
                double targetTop = screenHeight - Height - 100;
                double startTop = targetTop + 15;

                // 如果当前已经显示且没有在动画中，或者动画已经接近完成，则不重复触发
                if (IsVisible && Opacity > 0.99 && Math.Abs(Top - targetTop) < 1) return;

                // 获取当前状态作为动画起点
                double currentOpacity = Opacity;
                double currentTop = Top;

                if (!IsVisible)
                {
                    Opacity = 0;
                    Top = startTop;
                    currentOpacity = 0;
                    currentTop = startTop;
                    Show();
                }

                if (!_animationTimer.IsEnabled)
                    _animationTimer.Start();

                // 停止旧动画并从当前值开始新动画
                var fadeIn = new DoubleAnimation
                {
                    From = currentOpacity,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                var slideUp = new DoubleAnimation
                {
                    From = currentTop,
                    To = targetTop,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                BeginAnimation(OpacityProperty, fadeIn);
                BeginAnimation(TopProperty, slideUp);
            });
        }

        /// <summary>
        /// 隐藏指示器（带淡出+下滑动画）
        /// </summary>
        public void HideIndicator()
        {
            Dispatcher.Invoke(() =>
            {
                if (!IsVisible || Opacity < 0.01) return;

                double currentOpacity = Opacity;
                double currentTop = Top;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                double targetTop = screenHeight - Height - 100 + 15;

                var fadeOut = new DoubleAnimation
                {
                    From = currentOpacity,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (s, e) =>
                {
                    if (Opacity < 0.1)
                    {
                        _animationTimer.Stop();
                        Hide();
                    }
                };

                var slideDown = new DoubleAnimation
                {
                    From = currentTop,
                    To = targetTop,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };

                BeginAnimation(OpacityProperty, fadeOut);
                BeginAnimation(TopProperty, slideDown);
            });
        }

        /// <summary>
        /// 延迟隐藏
        /// </summary>
        public void DelayedHide(int delayMs)
        {
            Dispatcher.Invoke(() =>
            {
                if (_hideTimer != null)
                {
                    _hideTimer.Stop();
                }

                _hideTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(delayMs)
                };
                _hideTimer.Tick += (s, e) =>
                {
                    _hideTimer.Stop();
                    _hideTimer = null;
                    HideIndicator();
                };
                _hideTimer.Start();
            });
        }
    }
}
