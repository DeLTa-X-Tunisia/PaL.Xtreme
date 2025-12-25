using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System;

namespace PaLX.Client
{
    public partial class MainView : Window
    {
        private string _username;
        private string _role;

        public MainView(string username, string role)
        {
            InitializeComponent();
            _username = username;
            _role = role;
            LoadUserProfile(username);
            
            // Initialize Statuses
            var statuses = new List<StatusItem>
            {
                new StatusItem { Name = "En ligne", ColorBrush = Brushes.Green },
                new StatusItem { Name = "Occupé", ColorBrush = Brushes.Red },
                new StatusItem { Name = "Absent", ColorBrush = Brushes.Orange },
                new StatusItem { Name = "En appel", ColorBrush = Brushes.DarkRed },
                new StatusItem { Name = "Ne pas déranger", ColorBrush = Brushes.Purple },
                new StatusItem { Name = "Hors ligne", ColorBrush = Brushes.Gray }
            };
            StatusCombo.ItemsSource = statuses;
            StatusCombo.SelectedIndex = 0;

            // Initialize Dummy Friends
            var friends = new List<Friend>
            {
                new Friend { Name = "Alice", StatusText = "En ligne", StatusColor = Brushes.Green },
                new Friend { Name = "Bob", StatusText = "Occupé", StatusColor = Brushes.Red },
                new Friend { Name = "Charlie", StatusText = "Absent", StatusColor = Brushes.Orange },
                new Friend { Name = "David", StatusText = "Hors ligne", StatusColor = Brushes.Gray },
                new Friend { Name = "Eve", StatusText = "En ligne", StatusColor = Brushes.Green }
            };
            FriendsList.ItemsSource = friends;
        }

        private void LoadUserProfile(string username)
        {
            var dbService = new DatabaseService();
            var profile = dbService.GetUserProfile(username);

            if (profile != null)
            {
                // Display Name: LastName + FirstName
                UsernameText.Text = $"{profile.LastName} {profile.FirstName}";

                // Load Avatar
                if (!string.IsNullOrEmpty(profile.AvatarPath) && File.Exists(profile.AvatarPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(profile.AvatarPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    UserAvatar.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                    AvatarPlaceholder.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                UsernameText.Text = username;
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
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
            var userProfiles = new UserProfiles(_username, _role, true);
            userProfiles.ProfileSaved += () => LoadUserProfile(_username);
            userProfiles.Show();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Paramètres - Fonctionnalité à venir", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public class StatusItem
    {
        public string Name { get; set; } = "";
        public SolidColorBrush ColorBrush { get; set; } = Brushes.Gray;
    }

    public class Friend
    {
        public string Name { get; set; } = "";
        public string StatusText { get; set; } = "";
        public SolidColorBrush StatusColor { get; set; } = Brushes.Gray;
    }
}
