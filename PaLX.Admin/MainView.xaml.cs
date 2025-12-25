using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PaLX.Admin
{
    public partial class MainView : Window
    {
        public string CurrentUsername { get; private set; }
        public string CurrentRole { get; private set; }

        public MainView(string username, string role)
        {
            InitializeComponent();
            CurrentUsername = username;
            CurrentRole = role;
            UsernameText.Text = username;

            LoadDummyData();
        }

        private void LoadDummyData()
        {
            // Statuses
            var statuses = new List<UserStatus>
            {
                new UserStatus { Name = "En ligne", ColorBrush = Brushes.Green },
                new UserStatus { Name = "Occupé", ColorBrush = Brushes.Red },
                new UserStatus { Name = "Absent", ColorBrush = Brushes.Orange },
                new UserStatus { Name = "En appel", ColorBrush = Brushes.DarkRed },
                new UserStatus { Name = "Ne pas déranger", ColorBrush = Brushes.Purple },
                new UserStatus { Name = "Hors ligne", ColorBrush = Brushes.Gray }
            };
            StatusCombo.ItemsSource = statuses;
            StatusCombo.SelectedIndex = 0;

            // Friends
            var friends = new List<FriendItem>
            {
                new FriendItem { Name = "AdminUser", StatusText = "En ligne", StatusColor = Brushes.Green },
                new FriendItem { Name = "Moderator1", StatusText = "Occupé", StatusColor = Brushes.Red },
                new FriendItem { Name = "Support", StatusText = "Absent", StatusColor = Brushes.Orange },
                new FriendItem { Name = "NewUser123", StatusText = "Hors ligne", StatusColor = Brushes.Gray }
            };
            FriendsList.ItemsSource = friends;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new MainWindow();
            loginWindow.Show();
            this.Close();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsButton.ContextMenu != null)
            {
                SettingsButton.ContextMenu.PlacementTarget = SettingsButton;
                SettingsButton.ContextMenu.IsOpen = true;
            }
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Mon Profil - Fonctionnalité à venir", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Paramètres - Fonctionnalité à venir", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public class UserStatus
    {
        public required string Name { get; set; }
        public required Brush ColorBrush { get; set; }
    }

    public class FriendItem
    {
        public required string Name { get; set; }
        public required string StatusText { get; set; }
        public required Brush StatusColor { get; set; }
    }
}