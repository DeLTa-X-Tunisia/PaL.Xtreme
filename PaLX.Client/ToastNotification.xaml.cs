using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PaLX.Client
{
    public enum ToastType
    {
        Success,
        Error,
        Warning,
        Info
    }

    public partial class ToastNotification : Window
    {
        private readonly DispatcherTimer _autoCloseTimer;
        private readonly DispatcherTimer _progressTimer;
        private readonly int _displayDurationMs;
        private int _elapsedMs;
        private bool _isClosing;

        public ToastNotification(string title, string message, ToastType type, int durationMs = 4000)
        {
            InitializeComponent();

            _displayDurationMs = durationMs;
            _elapsedMs = 0;
            _isClosing = false;

            // Set content
            TitleText.Text = title;
            MessageText.Text = message;

            // Configure appearance based on type
            ConfigureAppearance(type);

            // Setup auto-close timer
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };
            _autoCloseTimer.Tick += (s, e) => CloseWithAnimation();

            // Setup progress bar timer
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _progressTimer.Tick += ProgressTimer_Tick;

            // Position window
            Loaded += ToastNotification_Loaded;

            // Mouse events for pause on hover
            MouseEnter += (s, e) => PauseTimers();
            MouseLeave += (s, e) => ResumeTimers();
        }

        private void ConfigureAppearance(ToastType type)
        {
            string icon;
            Color color;
            Color borderColor;

            switch (type)
            {
                case ToastType.Success:
                    icon = "✓";
                    color = Color.FromRgb(76, 175, 80);   // Green
                    borderColor = Color.FromRgb(56, 142, 60);
                    break;
                case ToastType.Error:
                    icon = "✕";
                    color = Color.FromRgb(244, 67, 54);   // Red
                    borderColor = Color.FromRgb(211, 47, 47);
                    break;
                case ToastType.Warning:
                    icon = "⚠";
                    color = Color.FromRgb(255, 152, 0);   // Orange
                    borderColor = Color.FromRgb(245, 124, 0);
                    break;
                case ToastType.Info:
                default:
                    icon = "ℹ";
                    color = Color.FromRgb(33, 150, 243);  // Blue
                    borderColor = Color.FromRgb(25, 118, 210);
                    break;
            }

            IconText.Text = icon;
            IconBorder.Background = new SolidColorBrush(color);
            ProgressBar.Background = new SolidColorBrush(color);
            ToastBorder.BorderBrush = new SolidColorBrush(borderColor);
        }

        private void ToastNotification_Loaded(object sender, RoutedEventArgs e)
        {
            // Position in bottom-right corner of the primary screen
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - ActualHeight - 20 - (ToastService.ActiveToastCount * (ActualHeight + 10));

            // Set initial progress bar width
            ProgressBar.Width = ToastBorder.ActualWidth + 32;

            // Play slide-in animation
            Opacity = 0;
            var slideIn = (Storyboard)FindResource("SlideIn");
            slideIn.Begin(this);

            // Start timers
            _autoCloseTimer.Start();
            _progressTimer.Start();
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            _elapsedMs += 50;
            double progress = 1.0 - ((double)_elapsedMs / _displayDurationMs);
            // S'assurer que la largeur ne devient jamais négative
            if (progress < 0) progress = 0;
            ProgressBar.Width = Math.Max(0, (ToastBorder.ActualWidth + 32) * progress);
        }

        private void PauseTimers()
        {
            _autoCloseTimer.Stop();
            _progressTimer.Stop();
        }

        private void ResumeTimers()
        {
            if (!_isClosing)
            {
                _autoCloseTimer.Start();
                _progressTimer.Start();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWithAnimation();
        }

        private void CloseWithAnimation()
        {
            if (_isClosing) return;
            _isClosing = true;

            _autoCloseTimer.Stop();
            _progressTimer.Stop();

            var slideOut = (Storyboard)FindResource("SlideOut");
            slideOut.Completed += (s, e) =>
            {
                ToastService.RemoveToast(this);
                Close();
            };
            slideOut.Begin(this);
        }

        public void UpdatePosition(int index)
        {
            var workArea = SystemParameters.WorkArea;
            var targetTop = workArea.Bottom - ActualHeight - 20 - (index * (ActualHeight + 10));

            // Animate to new position
            var animation = new DoubleAnimation
            {
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(TopProperty, animation);
        }
    }
}
