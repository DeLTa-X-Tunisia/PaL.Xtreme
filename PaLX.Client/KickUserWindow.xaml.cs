using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre de confirmation pour kicker un utilisateur
    /// </summary>
    public partial class KickUserWindow : Window
    {
        public bool Confirmed { get; private set; } = false;
        public string? Reason => string.IsNullOrWhiteSpace(ReasonTextBox.Text) ? null : ReasonTextBox.Text.Trim();

        private readonly string _username;
        private readonly string? _avatarUrl;

        public KickUserWindow(string username, string? avatarUrl = null)
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
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
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

        private void KickButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DialogResult = true;
            Close();
        }
    }
}
