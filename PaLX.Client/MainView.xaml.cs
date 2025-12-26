using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System;
using System.Linq;

namespace PaLX.Client
{
    public partial class MainView : Window
    {
        private string _username;
        private string _role;
        private DatabaseService _dbService;
        private AddFriendWindow? _addFriendWindow;

        public MainView(string username, string role)
        {
            InitializeComponent();
            _username = username;
            _role = role;
            _dbService = new DatabaseService();

            LoadUserProfile(username);
            LoadFriends();
            
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
        }

        private void LoadFriends()
        {
            var friends = _dbService.GetFriends(_username);
            var friendViewModels = friends.Select(f => new Friend
            {
                Name = f.DisplayName,
                StatusText = "Hors ligne", // Default for now, real status needs real-time logic
                StatusColor = Brushes.Gray,
                AvatarPath = (!string.IsNullOrEmpty(f.AvatarPath) && File.Exists(f.AvatarPath)) ? f.AvatarPath : null
            }).ToList();

            FriendsList.ItemsSource = friendViewModels;
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

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // Close all other windows
            foreach (Window window in Application.Current.Windows)
            {
                if (window != this)
                {
                    window.Close();
                }
            }

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

        private void BlockedUsers_Click(object sender, RoutedEventArgs e)
        {
            var blockedWindow = new BlockedUsersWindow(_username);
            blockedWindow.Show();
        }

        private void AddFriend_Click(object sender, RoutedEventArgs e)
        {
            if (_addFriendWindow == null || !_addFriendWindow.IsLoaded)
            {
                _addFriendWindow = new AddFriendWindow(_username);
                _addFriendWindow.Closed += (s, args) => _addFriendWindow = null;
                _addFriendWindow.Show();
            }
            else
            {
                if (_addFriendWindow.WindowState == WindowState.Minimized)
                {
                    _addFriendWindow.WindowState = WindowState.Normal;
                }
                _addFriendWindow.Activate();
            }
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
        public string? AvatarPath { get; set; }
    }
}
