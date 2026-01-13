using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre non-modale pour modifier la durée d'un bannissement
    /// </summary>
    public partial class EditBanWindow : Window
    {
        private readonly BannedUserViewModel _ban;
        
        // Événement déclenché quand la modification est confirmée
        public event Action<BannedUserViewModel, string, int?>? OnBanUpdated;
        
        public bool IsConfirmed { get; private set; }
        public string NewBanType { get; private set; } = "Temporary";
        public int? NewDurationMinutes { get; private set; }

        public EditBanWindow(BannedUserViewModel ban)
        {
            InitializeComponent();
            _ban = ban;
            
            LoadBanInfo();
        }

        private void LoadBanInfo()
        {
            // User info
            UserDisplayName.Text = _ban.DisplayName;
            UserUsername.Text = $"@{_ban.Username}";
            
            // Avatar
            try
            {
                if (!string.IsNullOrEmpty(_ban.AvatarUrl))
                {
                    UserAvatar.ImageSource = new BitmapImage(new Uri(_ban.AvatarUrl, UriKind.Absolute));
                }
                else
                {
                    UserAvatar.ImageSource = new BitmapImage(new Uri($"{ApiService.BaseUrl}/avatars/default_avatar.png", UriKind.Absolute));
                }
            }
            catch
            {
                // Ignore avatar loading errors
            }

            // Current ban status
            if (_ban.BanType == "Permanent")
            {
                CurrentTypeBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                CurrentTypeText.Text = "PERMANENT";
                CurrentDurationText.Text = "Sans limite de temps";
                
                PermanentRadio.IsChecked = true;
            }
            else
            {
                CurrentTypeBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                CurrentTypeText.Text = "TEMPORAIRE";
                CurrentDurationText.Text = !string.IsNullOrEmpty(_ban.TimeRemaining) 
                    ? $"Expire dans: {_ban.TimeRemaining}" 
                    : "Expire bientôt";
                
                TemporaryRadio.IsChecked = true;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void DurationType_Changed(object sender, RoutedEventArgs e)
        {
            if (DurationPanel == null || PermanentWarning == null) return;
            
            if (TemporaryRadio.IsChecked == true)
            {
                DurationPanel.Visibility = Visibility.Visible;
                PermanentWarning.Visibility = Visibility.Collapsed;
            }
            else
            {
                DurationPanel.Visibility = Visibility.Collapsed;
                PermanentWarning.Visibility = Visibility.Visible;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            
            if (PermanentRadio.IsChecked == true)
            {
                NewBanType = "Permanent";
                NewDurationMinutes = null;
            }
            else
            {
                NewBanType = "Temporary";
                
                if (DurationComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag != null)
                {
                    NewDurationMinutes = int.Parse(item.Tag.ToString()!);
                }
                else
                {
                    NewDurationMinutes = 60; // Default 1 hour
                }
            }
            
            // Déclencher l'événement pour notifier la fenêtre parente
            OnBanUpdated?.Invoke(_ban, NewBanType, NewDurationMinutes);
            
            Close();
        }
    }
}
