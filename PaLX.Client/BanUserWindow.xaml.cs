using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre de confirmation pour bannir un utilisateur
    /// </summary>
    public partial class BanUserWindow : Window
    {
        public bool Confirmed { get; private set; } = false;
        public string? Reason => string.IsNullOrWhiteSpace(ReasonTextBox.Text) ? null : ReasonTextBox.Text.Trim();
        public string BanType { get; private set; } = "Permanent";
        public int? DurationMinutes { get; private set; } = null;

        private readonly string _username;
        private readonly string? _avatarUrl;

        public BanUserWindow(string username, string? avatarUrl = null)
        {
            InitializeComponent();
            _username = username;
            _avatarUrl = avatarUrl;

            // Set user info
            UsernameText.Text = username;

            // Set avatar if available
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                try
                {
                    UserAvatar.ImageSource = new BitmapImage(new Uri(avatarUrl, UriKind.Absolute));
                }
                catch
                {
                    // Keep default avatar
                }
            }

            // Update warning visibility
            UpdateWarningVisibility();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Duration_Click(object sender, RoutedEventArgs e)
        {
            UpdateWarningVisibility();
        }

        private void UpdateWarningVisibility()
        {
            if (PermanentWarning != null)
            {
                PermanentWarning.Visibility = DurationPermanent.IsChecked == true 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        private void BanButton_Click(object sender, RoutedEventArgs e)
        {
            // Determine ban type and duration
            if (Duration1h.IsChecked == true)
            {
                BanType = "Temporary";
                DurationMinutes = 60; // 1 hour
            }
            else if (Duration24h.IsChecked == true)
            {
                BanType = "Temporary";
                DurationMinutes = 1440; // 24 hours
            }
            else if (Duration7d.IsChecked == true)
            {
                BanType = "Temporary";
                DurationMinutes = 10080; // 7 days
            }
            else // Permanent
            {
                BanType = "Permanent";
                DurationMinutes = null;
            }

            Confirmed = true;
            DialogResult = true;
            Close();
        }
    }
}
