using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.IO;
using System;
using System.Media;
using PaLX.Admin.Services;
using System.Linq;
using System.Threading.Tasks;

namespace PaLX.Admin
{
    public partial class MainView : Window
    {
        public string CurrentUsername { get; private set; }
        public string CurrentRole { get; private set; }

        private SoundPlayer? _onlineSound;
        private SoundPlayer? _offlineSound;
        private SoundPlayer? _messageSound;
        private Dictionary<string, ChatWindow> _openChatWindows = new Dictionary<string, ChatWindow>();

        // Notification Properties
        public static readonly DependencyProperty NotificationCountProperty =
            DependencyProperty.Register("NotificationCount", typeof(int), typeof(MainView), new PropertyMetadata(0));

        public int NotificationCount
        {
            get { return (int)GetValue(NotificationCountProperty); }
            set { SetValue(NotificationCountProperty, value); }
        }

        public static readonly DependencyProperty HasNotificationsProperty =
            DependencyProperty.Register("HasNotifications", typeof(bool), typeof(MainView), new PropertyMetadata(false));

        public bool HasNotifications
        {
            get { return (bool)GetValue(HasNotificationsProperty); }
            set { SetValue(HasNotificationsProperty, value); }
        }

        public MainView(string username, string role)
        {
            InitializeComponent();
            CurrentUsername = username;
            CurrentRole = role;
            
            // Set DataContext for bindings
            NotificationButton.DataContext = this;
            
            // Subscribe to SignalR events
            ApiService.Instance.OnPrivateMessageReceived += OnPrivateMessageReceived;
            ApiService.Instance.OnUserStatusChanged += OnUserStatusChanged;
            
            // Friend Sync
            ApiService.Instance.OnFriendRequestAccepted += OnFriendUpdate;
            ApiService.Instance.OnFriendRemoved += OnFriendUpdate;
            ApiService.Instance.OnFriendRequestReceived += OnFriendRequestReceived;

            // Block Sync
            ApiService.Instance.OnUserBlocked += OnUserBlocked;
            ApiService.Instance.OnUserBlockedBy += OnUserBlocked;
            ApiService.Instance.OnUserUnblocked += OnUserUnblocked;
            ApiService.Instance.OnUserUnblockedBy += OnUserUnblocked;

            // System Events
            ApiService.Instance.OnConnectionClosed += OnConnectionClosed;

            // Load Sounds
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _onlineSound = new SoundPlayer(Path.Combine(baseDir, "Assets", "Sounds", "online.wav"));
                _offlineSound = new SoundPlayer(Path.Combine(baseDir, "Assets", "Sounds", "offline.wav"));
                _messageSound = new SoundPlayer(Path.Combine(baseDir, "Assets", "Sounds", "message.wav"));
                // Preload
                _onlineSound.LoadAsync();
                _offlineSound.LoadAsync();
                _messageSound.LoadAsync();
            }
            catch { /* Ignore sound errors */ }

            LoadStatuses();
            Loaded += async (s, e) => 
            {
                await LoadUserProfileAsync(username);
                await LoadFriendsAsync();
                UpdateFriendRequestsCount();
            };

            this.Closing += MainView_Closing;
        }

        private void OnConnectionClosed()
        {
            Dispatcher.Invoke(() =>
            {
                var maintenance = new MaintenanceWindow();
                maintenance.ShowDialog();
                
                // Force Logout logic
                Logout_Click(null!, null!);
            });
        }

        private void OnFriendUpdate(string username)
        {
            Dispatcher.Invoke(async () => 
            {
                await LoadFriendsAsync();
                UpdateFriendRequestsCount();
            });
        }

        private void OnUserBlocked(string username)
        {
            Dispatcher.Invoke(() =>
            {
                if (FriendsList.ItemsSource is List<FriendItem> items)
                {
                    var friend = items.FirstOrDefault(f => f.Username == username);
                    if (friend != null)
                    {
                        friend.StatusText = "Bloqué";
                        friend.StatusColor = Brushes.Red;
                        FriendsList.Items.Refresh();
                    }
                }
            });
        }

        private void OnUserUnblocked(string username)
        {
             Dispatcher.Invoke(async () => await LoadFriendsAsync());
        }

        private void OnPrivateMessageReceived(string sender, string message)
        {
            Dispatcher.Invoke(() => 
            {
                if (!_openChatWindows.ContainsKey(sender))
                {
                    OpenChatWindow(sender);
                    _messageSound?.Play();
                }
                else
                {
                    var window = _openChatWindows[sender];
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                }
            });
        }

        private void OnUserStatusChanged(string username, string status)
        {
            Dispatcher.Invoke(() =>
            {
                if (FriendsList.ItemsSource is List<FriendItem> items)
                {
                    var friend = items.FirstOrDefault(f => f.Username == username);
                    if (friend != null)
                    {
                        // Update Status
                        friend.StatusText = status;
                        friend.StatusColor = GetStatusColor(status);
                        
                        int statusValue = 6;
                        if (status == "En ligne") statusValue = 0;
                        else if (status == "Occupé") statusValue = 1;
                        else if (status == "Absent") statusValue = 2;
                        else if (status == "En appel") statusValue = 3;
                        else if (status == "Ne pas déranger") statusValue = 4;
                        
                        friend.NameFontWeight = statusValue != 6 ? FontWeights.Bold : FontWeights.Normal;

                        // Play Sound
                        try
                        {
                            if (statusValue != 6 && _previousStatuses.ContainsKey(username) && _previousStatuses[username] == 6) 
                                _onlineSound?.Play();
                            else if (statusValue == 6) 
                                _offlineSound?.Play();
                        }
                        catch { }
                        
                        if (_previousStatuses.ContainsKey(username)) _previousStatuses[username] = statusValue;
                        else _previousStatuses.Add(username, statusValue);

                        // Re-sort and Refresh
                        var sortedFriends = items.OrderBy(f => f.StatusText == "Hors ligne").ThenBy(f => f.Name).ToList();
                        FriendsList.ItemsSource = null;
                        FriendsList.ItemsSource = sortedFriends;
                    }
                }
            });
        }

        private void OpenChatWindow(string partnerUsername)
        {
            if (_openChatWindows.ContainsKey(partnerUsername))
            {
                var window = _openChatWindows[partnerUsername];
                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                window.Activate();
                return;
            }

            ApiService.Instance.OnConnectionClosed -= OnConnectionClosed;
            var chatWindow = new ChatWindow(CurrentUsername, partnerUsername);
            chatWindow.Closed += (s, args) => _openChatWindows.Remove(partnerUsername);
            _openChatWindows.Add(partnerUsername, chatWindow);
            chatWindow.Show();
        }

        private void OnFriendRequestReceived(string username)
        {
            Dispatcher.Invoke(() => UpdateFriendRequestsCount());
        }

        private async void UpdateFriendRequestsCount()
        {
            try
            {
                var requests = await ApiService.Instance.GetPendingRequestsAsync();
                NotificationCount = requests.Count;
                HasNotifications = NotificationCount > 0;
            }
            catch { }
        }

        private void NotificationButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if already open
            var existingWindow = Application.Current.Windows.OfType<AddFriendWindow>().FirstOrDefault();
            if (existingWindow != null)
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                    existingWindow.WindowState = WindowState.Normal;
                existingWindow.SelectReceivedRequestsTab();
                existingWindow.Activate();
                return;
            }

            var addFriendWindow = new AddFriendWindow(CurrentUsername);
            addFriendWindow.FriendAdded += async () => 
            {
                await LoadFriendsAsync();
                UpdateFriendRequestsCount();
            };
            addFriendWindow.SelectReceivedRequestsTab();
            addFriendWindow.Show();
        }

        private async void MainView_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            ApiService.Instance.OnPrivateMessageReceived -= OnPrivateMessageReceived;
            ApiService.Instance.OnUserStatusChanged -= OnUserStatusChanged;
            ApiService.Instance.OnFriendRequestAccepted -= OnFriendUpdate;
            ApiService.Instance.OnFriendRemoved -= OnFriendUpdate;
            ApiService.Instance.OnFriendRequestReceived -= OnFriendRequestReceived;

            await ApiService.Instance.DisconnectAsync();
        }

        private async Task LoadUserProfileAsync(string username)
        {
            var profile = await ApiService.Instance.GetUserProfileAsync(username);

            if (profile != null)
            {
                // Display Name: LastName + FirstName
                UsernameText.Text = $"{profile.LastName} {profile.FirstName}";

                // Load Avatar
                if (!string.IsNullOrEmpty(profile.AvatarPath) && File.Exists(profile.AvatarPath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(profile.AvatarPath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        UserAvatar.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                        AvatarPlaceholder.Visibility = Visibility.Collapsed;
                    }
                    catch { /* Ignore image load errors */ }
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

        private async void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusCombo.SelectedItem is UserStatus selectedStatus)
            {
                int statusValue = 0;
                switch (selectedStatus.Name)
                {
                    case "En ligne": statusValue = 0; break;
                    case "Occupé": statusValue = 1; break;
                    case "Absent": statusValue = 2; break;
                    case "En appel": statusValue = 3; break;
                    case "Ne pas déranger": statusValue = 4; break;
                    case "Hors ligne": statusValue = 6; break;
                    default: statusValue = 0; break;
                }
                await ApiService.Instance.UpdateStatusAsync(statusValue);
            }
        }

        private Dictionary<string, int> _previousStatuses = new Dictionary<string, int>();
        private Dictionary<string, DateTime> _blinkingUntil = new Dictionary<string, DateTime>();

        private async Task LoadFriendsAsync()
        {
            var friends = await ApiService.Instance.GetFriendsAsync();
            
            // Sort: Online (not "Hors ligne") first, Offline last
            var sortedFriends = friends.OrderBy(f => f.Status == "Hors ligne").ThenBy(f => f.DisplayName);

            var friendItems = new List<FriendItem>();

            foreach (var f in sortedFriends)
            {
                bool hasAvatar = !string.IsNullOrEmpty(f.AvatarPath) && File.Exists(f.AvatarPath);
                
                Brush statusBrush = GetStatusColor(f.Status);
                int statusValue = f.Status == "Hors ligne" ? 6 : 0; // Simplified for blinking logic
                if (f.Status == "Occupé") statusValue = 1;
                else if (f.Status == "Absent") statusValue = 2;
                else if (f.Status == "En appel") statusValue = 3;
                else if (f.Status == "Ne pas déranger") statusValue = 4;

                // Check for status change
                bool isBlinking = false;
                if (_previousStatuses.ContainsKey(f.Username))
                {
                    if (_previousStatuses[f.Username] != statusValue)
                    {
                        // Status changed, set blinking expiration to Now + 5 seconds
                        _blinkingUntil[f.Username] = DateTime.Now.AddSeconds(5);

                        // Play Sound Effects
                        try
                        {
                            if (statusValue != 6 && _previousStatuses[f.Username] == 6) // Came Online
                            {
                                _onlineSound?.Play();
                            }
                            else if (statusValue == 6) // Went Offline
                            {
                                _offlineSound?.Play();
                            }
                        }
                        catch { /* Ignore playback errors */ }
                    }
                }
                _previousStatuses[f.Username] = statusValue;

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

                // Override if Blocked
                string displayStatus = f.Status;
                Brush displayColor = statusBrush;

                if (f.IsBlocked)
                {
                    displayStatus = "Bloqué";
                    displayColor = Brushes.Red;
                }

                friendItems.Add(new FriendItem
                {
                    Name = f.DisplayName,
                    StatusText = displayStatus,
                    StatusColor = displayColor,
                    Username = f.Username,
                    AvatarPath = hasAvatar ? f.AvatarPath : null,
                    AvatarVisibility = hasAvatar ? Visibility.Visible : Visibility.Collapsed,
                    PlaceholderVisibility = hasAvatar ? Visibility.Collapsed : Visibility.Visible,
                    NameFontWeight = statusValue != 6 ? FontWeights.Bold : FontWeights.Normal,
                    IsBlinking = isBlinking,
                    IsBlocked = f.IsBlocked,
                    BlockIcon = f.IsBlocked ? "\xE785" : "\xE72E", // Unlock vs Lock
                    BlockToolTip = f.IsBlocked ? "Débloquer" : "Bloquer",
                    BlockOverlayVisibility = f.IsBlocked ? Visibility.Visible : Visibility.Collapsed
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

        private async void Logout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Only attempt network calls if this is a manual logout (sender is Button)
                if (sender is Button)
                {
                    await ApiService.Instance.DisconnectAsync();
                }
                else
                {
                    // Forced logout - fire and forget disconnect
                    _ = ApiService.Instance.DisconnectAsync();
                }
            }
            catch { }
            
            // Close all other windows (Chat, Profile, etc.)
            var windows = new List<Window>(Application.Current.Windows.Cast<Window>());
            foreach (var window in windows)
            {
                if (window != this)
                {
                    window.Close();
                }
            }

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
            userProfiles.ProfileSaved += async () => await LoadUserProfileAsync(CurrentUsername);
            userProfiles.Show();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Paramètres - Fonctionnalité à venir", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

private void AddFriend_Click(object sender, RoutedEventArgs e)
        {
            // Check if already open
            var existingWindow = Application.Current.Windows.OfType<AddFriendWindow>().FirstOrDefault();
            if (existingWindow != null)
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                    existingWindow.WindowState = WindowState.Normal;
                existingWindow.Activate();
                return;
            }

            var addFriendWindow = new AddFriendWindow(CurrentUsername);
            addFriendWindow.FriendAdded += async () => 
            {
                await LoadFriendsAsync();
                UpdateFriendRequestsCount();
            };
            addFriendWindow.Show();
        }

        private async void BlockedUsers_Click(object sender, RoutedEventArgs e)
        {
            var blockedWindow = new BlockedUsersWindow(CurrentUsername);
            blockedWindow.Show();
            // Note: Since it's non-modal, we can't await its closing to refresh friends.
            // But BlockedUsersWindow could trigger an event if needed.
        }

        private void OpenChat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string username)
                {
                    OpenChatWindow(username);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible d'ouvrir le chat : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private async void BlockUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string username)
            {
                // Check current status
                var friends = FriendsList.ItemsSource as List<FriendItem>;
                var friend = friends?.FirstOrDefault(f => f.Username == username);
                bool isBlocked = friend?.IsBlocked ?? false;

                if (isBlocked)
                {
                    // Unblock
                    var result = await ApiService.Instance.UnblockUserAsync(username);
                    if (result.Success)
                    {
                        await LoadFriendsAsync();
                    }
                    else
                    {
                        new CustomAlertWindow(result.Message).ShowDialog();
                    }
                }
                else
                {
                    // Block
                    var confirm = new CustomConfirmWindow($"Voulez-vous vraiment bloquer {username} ?", "Bloquer");
                    if (confirm.ShowDialog() == true)
                    {
                        var result = await ApiService.Instance.BlockUserAsync(username, 0, null, "Bloqué par l'admin");
                        if (result.Success)
                        {
                            await LoadFriendsAsync();
                        }
                        else
                        {
                            new CustomAlertWindow(result.Message).ShowDialog();
                        }
                    }
                }
            }
        }

        private async void RemoveFriend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string username)
            {
                var confirm = new CustomConfirmWindow($"Voulez-vous vraiment retirer {username} de vos amis ?", "Retirer");
                if (confirm.ShowDialog() == true)
                {
                    await ApiService.Instance.RemoveFriendAsync(username);
                    await LoadFriendsAsync();
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

        // New properties for Blocking
        public bool IsBlocked { get; set; } = false;
        public string BlockIcon { get; set; } = "\xE72B"; // Default Block Icon
        public string BlockToolTip { get; set; } = "Bloquer";
        public Visibility BlockOverlayVisibility { get; set; } = Visibility.Collapsed;
    }
}
