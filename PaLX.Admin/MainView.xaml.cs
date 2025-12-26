using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System;

namespace PaLX.Admin
{
    public partial class MainView : Window
    {
        public string CurrentUsername { get; private set; }
        public string CurrentRole { get; private set; }
        private DatabaseService _dbService;

        public MainView(string username, string role)
        {
            InitializeComponent();
            CurrentUsername = username;
            CurrentRole = role;
            _dbService = new DatabaseService();

            LoadStatuses();
            LoadUserProfile(username);
            LoadFriends();
        }

        private void LoadUserProfile(string username)
        {
            var profile = _dbService.GetUserProfile(username);

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

        private void LoadStatuses()
        {
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
        }

        private void LoadFriends()
        {
            var friends = _dbService.GetFriends(CurrentUsername);
            var friendItems = new List<FriendItem>();

            foreach (var f in friends)
            {
                friendItems.Add(new FriendItem
                {
                    Name = f.DisplayName,
                    StatusText = f.Status,
                    StatusColor = GetStatusColor(f.Status)
                });
            }
            FriendsList.ItemsSource = friendItems;
        }

        private Brush GetStatusColor(string status)
        {
            return status switch
            {
                "En ligne" => Brushes.Green,
                "Occupé" => Brushes.Red,
                "Absent" => Brushes.Orange,
                "Hors ligne" => Brushes.Gray,
                _ => Brushes.Gray
            };
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
            var userProfiles = new UserProfiles(CurrentUsername, CurrentRole, true);
            userProfiles.ProfileSaved += () => LoadUserProfile(CurrentUsername);
            userProfiles.Show();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Paramètres - Fonctionnalité à venir", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddFriend_Click(object sender, RoutedEventArgs e)
        {
            var addFriendWindow = new AddFriendWindow(CurrentUsername);
            addFriendWindow.ShowDialog();
            LoadFriends(); // Refresh list after closing
        }

        private void BlockedUsers_Click(object sender, RoutedEventArgs e)
        {
            var blockedWindow = new BlockedUsersWindow(CurrentUsername);
            blockedWindow.ShowDialog();
            LoadFriends(); // Refresh list in case unblocked users are now friends (unlikely but good practice)
        }
    }

    public class UserStatus
    {
        public string Name { get; set; } = "";
        public Brush ColorBrush { get; set; } = Brushes.Gray;
    }

    public class FriendItem
    {
        public string Name { get; set; } = "";
        public string StatusText { get; set; } = "";
        public Brush StatusColor { get; set; } = Brushes.Gray;
    }
}
