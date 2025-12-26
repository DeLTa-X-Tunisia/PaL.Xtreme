using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.IO;
using System;

namespace PaLX.Admin
{
    public partial class MainView : Window
    {
        public string CurrentUsername { get; private set; }
        public string CurrentRole { get; private set; }
        private DatabaseService _dbService;

        private System.Windows.Threading.DispatcherTimer _refreshTimer;

        public MainView(string username, string role)
        {
            InitializeComponent();
            CurrentUsername = username;
            CurrentRole = role;
            _dbService = new DatabaseService();

            LoadStatuses();
            LoadUserProfile(username);
            LoadFriends();

            // Setup Timer
            _refreshTimer = new System.Windows.Threading.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2); // Faster refresh
            _refreshTimer.Tick += (s, e) => LoadFriends();
            _refreshTimer.Start();

            this.Closing += MainView_Closing;
        }

        private void MainView_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _dbService.EndSession(CurrentUsername);
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
            StatusCombo.SelectionChanged += StatusCombo_SelectionChanged;
        }

        private void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusCombo.SelectedItem is UserStatus selectedStatus)
            {
                int statusValue = 0;
                switch (selectedStatus.Name)
                {
                    case "En ligne": statusValue = 0; break;
                    case "Absent": statusValue = 1; break;
                    case "Occupé": statusValue = 2; break;
                    case "En appel": statusValue = 3; break;
                    case "Ne pas déranger": statusValue = 2; break;
                    case "Hors ligne": statusValue = 6; break;
                    default: statusValue = 0; break;
                }
                _dbService.UpdateStatus(CurrentUsername, statusValue);
            }
        }

        private Dictionary<string, int> _previousStatuses = new Dictionary<string, int>();
        private Dictionary<string, DateTime> _blinkingUntil = new Dictionary<string, DateTime>();

        private void LoadFriends()
        {
            var friends = _dbService.GetFriends(CurrentUsername);
            
            // Sort: Online (0-5) first, Offline (6) last
            var sortedFriends = friends.OrderBy(f => f.StatusValue == 6).ThenBy(f => f.DisplayName);

            var friendItems = new List<FriendItem>();

            foreach (var f in sortedFriends)
            {
                bool hasAvatar = !string.IsNullOrEmpty(f.AvatarPath) && File.Exists(f.AvatarPath);
                
                Brush statusBrush = Brushes.Gray;
                switch (f.StatusValue)
                {
                    case 0: statusBrush = Brushes.Green; break;
                    case 1: statusBrush = Brushes.Orange; break;
                    case 2: statusBrush = Brushes.Red; break;
                    case 3: statusBrush = Brushes.DarkRed; break;
                    case 4: statusBrush = Brushes.YellowGreen; break;
                    case 5: statusBrush = Brushes.DarkBlue; break;
                    default: statusBrush = Brushes.Gray; break;
                }

                // Check for status change
                bool isBlinking = false;
                if (_previousStatuses.ContainsKey(f.Username))
                {
                    if (_previousStatuses[f.Username] != f.StatusValue)
                    {
                        // Status changed, set blinking expiration to Now + 5 seconds
                        _blinkingUntil[f.Username] = DateTime.Now.AddSeconds(5);
                    }
                }
                _previousStatuses[f.Username] = f.StatusValue;

                // Check if currently blinking
                if (_blinkingUntil.ContainsKey(f.Username))
                {
                    if (DateTime.Now < _blinkingUntil[f.Username])
                    {
                        isBlinking = true;
                    }
                    else
                    {
                        _blinkingUntil.Remove(f.Username);
                    }
                }

                friendItems.Add(new FriendItem
                {
                    Name = f.DisplayName,
                    StatusText = f.Status,
                    StatusColor = statusBrush,
                    Username = f.Username,
                    AvatarPath = hasAvatar ? f.AvatarPath : null,
                    AvatarVisibility = hasAvatar ? Visibility.Visible : Visibility.Collapsed,
                    PlaceholderVisibility = hasAvatar ? Visibility.Collapsed : Visibility.Visible,
                    NameFontWeight = f.StatusValue != 6 ? FontWeights.Bold : FontWeights.Normal,
                    IsBlinking = isBlinking
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
            addFriendWindow.FriendAdded += () => LoadFriends(); // Instant refresh
            addFriendWindow.ShowDialog();
            LoadFriends(); // Refresh list after closing (redundant but safe)
        }

        private void BlockedUsers_Click(object sender, RoutedEventArgs e)
        {
            var blockedWindow = new BlockedUsersWindow(CurrentUsername);
            blockedWindow.ShowDialog();
            LoadFriends(); // Refresh list in case unblocked users are now friends (unlikely but good practice)
        }

        private void ViewProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string username)
            {
                var profileWindow = new PublicProfileWindow(username);
                profileWindow.ShowDialog();
            }
        }

        private void BlockUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string username)
            {
                if (MessageBox.Show($"Voulez-vous vraiment bloquer {username} ?", "Bloquer", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    if (_dbService.BlockUser(CurrentUsername, username, 0, null, "Bloqué par l'admin"))
                    {
                        LoadFriends();
                    }
                }
            }
        }

        private void RemoveFriend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string username)
            {
                if (MessageBox.Show($"Voulez-vous vraiment retirer {username} de vos amis ?", "Retirer", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _dbService.RemoveFriend(CurrentUsername, username);
                    LoadFriends();
                }
            }
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
        public string Username { get; set; } = "";
        public string? AvatarPath { get; set; }
        public Visibility AvatarVisibility { get; set; } = Visibility.Collapsed;
        public Visibility PlaceholderVisibility { get; set; } = Visibility.Visible;
        public FontWeight NameFontWeight { get; set; } = FontWeights.Normal;
        public bool IsBlinking { get; set; } = false;
    }
}
