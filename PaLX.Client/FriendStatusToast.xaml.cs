using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;

namespace PaLX.Client
{
    public partial class FriendStatusToast : Window
    {
        private readonly DispatcherTimer _autoCloseTimer;
        private bool _isClosing;
        private static int _activeCount = 0;
        private static readonly object _countLock = new();

        public FriendStatusToast(string friendName, string? avatarPath, bool isOnline)
        {
            InitializeComponent();
            _isClosing = false;

            // Configure friend info
            FriendNameText.Text = friendName;
            
            // Configure avatar
            SetAvatar(avatarPath);

            // Configure status appearance
            ConfigureStatus(isOnline);

            // Setup auto-close timer (3.5 seconds for status toasts - shorter than regular toasts)
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(3500)
            };
            _autoCloseTimer.Tick += (s, e) => CloseWithAnimation();

            // Position and show
            Loaded += FriendStatusToast_Loaded;

            // Pause on hover
            MouseEnter += (s, e) => _autoCloseTimer.Stop();
            MouseLeave += (s, e) => { if (!_isClosing) _autoCloseTimer.Start(); };
        }

        private void SetAvatar(string? avatarPath)
        {
            if (!string.IsNullOrEmpty(avatarPath) && File.Exists(avatarPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(avatarPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    
                    AvatarBrush.ImageSource = bitmap;
                    AvatarPlaceholder.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    ShowPlaceholder();
                }
            }
            else
            {
                ShowPlaceholder();
            }
        }

        private void ShowPlaceholder()
        {
            AvatarBrush.ImageSource = null;
            AvatarPlaceholder.Visibility = Visibility.Visible;
        }

        private void ConfigureStatus(bool isOnline)
        {
            if (isOnline)
            {
                // Online - Green theme
                StatusDot.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                StatusText.Text = "est en ligne";
                ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }
            else
            {
                // Offline - Gray/subtle theme
                StatusDot.Background = new SolidColorBrush(Color.FromRgb(128, 128, 128));
                StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128));
                StatusText.Text = "est hors ligne";
                ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            }
        }

        private void FriendStatusToast_Loaded(object sender, RoutedEventArgs e)
        {
            int currentIndex;
            lock (_countLock)
            {
                currentIndex = _activeCount;
                _activeCount++;
            }

            // Position in bottom-right corner
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - ActualHeight - 20 - (currentIndex * (ActualHeight + 10));

            // Play slide-in animation
            Opacity = 0;
            var slideIn = (Storyboard)FindResource("SlideIn");
            slideIn.Begin(this);

            // Start timer
            _autoCloseTimer.Start();
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

            var slideOut = (Storyboard)FindResource("SlideOut");
            slideOut.Completed += (s, e) =>
            {
                lock (_countLock)
                {
                    _activeCount = Math.Max(0, _activeCount - 1);
                }
                Close();
            };
            slideOut.Begin(this);
        }
    }
}
