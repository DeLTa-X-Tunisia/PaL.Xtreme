using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
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

        private System.Windows.Threading.DispatcherTimer _refreshTimer;

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
            StatusCombo.SelectionChanged += StatusCombo_SelectionChanged;

            // Setup Timer
            _refreshTimer = new System.Windows.Threading.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2); // Faster refresh for better UX
            _refreshTimer.Tick += (s, e) => LoadFriends();
            _refreshTimer.Start();

            this.Closing += MainView_Closing;
        }

        private void MainView_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _dbService.EndSession(_username);
        }

        private void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusCombo.SelectedItem is StatusItem selectedStatus)
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
                _dbService.UpdateStatus(_username, statusValue);
            }
        }

        private void LoadFriends()
        {
            var friends = _dbService.GetFriends(_username);
            
            // Sort: Online (0-5) first, Offline (6) last
            var sortedFriends = friends.OrderBy(f => f.StatusValue == 6).ThenBy(f => f.DisplayName);

            var friendViewModels = sortedFriends.Select(f => {
                bool hasAvatar = !string.IsNullOrEmpty(f.AvatarPath) && File.Exists(f.AvatarPath);
                
                SolidColorBrush statusBrush = Brushes.Gray;
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

                return new Friend
                {
                    Name = f.DisplayName,
                    StatusText = f.Status,
                    StatusColor = statusBrush,
                    AvatarPath = hasAvatar ? f.AvatarPath : null,
                    Username = f.Username,
                    AvatarVisibility = hasAvatar ? Visibility.Visible : Visibility.Collapsed,
                    PlaceholderVisibility = hasAvatar ? Visibility.Collapsed : Visibility.Visible,
                    NameFontWeight = f.StatusValue != 6 ? FontWeights.Bold : FontWeights.Normal
                };
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
            _dbService.EndSession(_username);
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
                _addFriendWindow.FriendAdded += () => LoadFriends(); // Instant refresh on action
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
                    if (_dbService.BlockUser(_username, username, 0, null, "Bloqué par l'utilisateur"))
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
                    _dbService.RemoveFriend(_username, username);
                    LoadFriends();
                }
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
        public string Username { get; set; } = "";
        public Visibility AvatarVisibility { get; set; } = Visibility.Collapsed;
        public Visibility PlaceholderVisibility { get; set; } = Visibility.Visible;
        public FontWeight NameFontWeight { get; set; } = FontWeights.Normal;
    }
}
