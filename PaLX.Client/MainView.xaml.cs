using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.IO;
using System;
using System.Linq;
using System.Media;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class MainView : Window
    {
        private string _username;
        private string _role;
        private AddFriendWindow? _addFriendWindow;

        private System.Windows.Threading.DispatcherTimer _refreshTimer;
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
            _username = username;
            _role = role;
            
            // Set DataContext for bindings
            NotificationButton.DataContext = this;

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

            LoadUserProfile(username);
            LoadFriends();
            UpdateFriendRequestsCount();
            
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
            _refreshTimer.Interval = TimeSpan.FromSeconds(5); // Slower refresh as we use SignalR for messages
            _refreshTimer.Tick += (s, e) => 
            {
                LoadFriends();
                // CheckForIncomingMessages(); // Handled by SignalR
            };
            _refreshTimer.Start();

            // Subscribe to SignalR events
            ApiService.Instance.OnPrivateMessageReceived += OnPrivateMessageReceived;
            ApiService.Instance.OnUserStatusChanged += OnUserStatusChanged;
            
            // Friend Sync
            ApiService.Instance.OnFriendRequestAccepted += OnFriendUpdate;
            ApiService.Instance.OnFriendRequestReceived += OnFriendRequestReceived;
            ApiService.Instance.OnFriendRemoved += OnFriendUpdate;

            // Block Sync
            ApiService.Instance.OnUserBlocked += OnUserBlocked;
            ApiService.Instance.OnUserBlockedBy += OnUserBlocked; 
            ApiService.Instance.OnUserUnblocked += OnUserUnblocked;
            ApiService.Instance.OnUserUnblockedBy += OnUserUnblocked;

            // System Events
            ApiService.Instance.OnConnectionClosed += OnConnectionClosed;

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

        private void OnUserBlocked(string username)
        {
            Dispatcher.Invoke(() =>
            {
                if (FriendsList.ItemsSource is List<Friend> items)
                {
                    var friend = items.FirstOrDefault(f => f.Username == username);
                    if (friend != null)
                    {
                        friend.StatusText = "Bloqué";
                        friend.StatusColor = Brushes.Red;
                        friend.NameFontWeight = FontWeights.Normal;
                        FriendsList.Items.Refresh();
                    }
                }
            });
        }

        private void OnUserUnblocked(string username)
        {
             Dispatcher.Invoke(() => LoadFriends());
        }

        private void OnFriendUpdate(string username)
        {
            Dispatcher.Invoke(() => 
            {
                LoadFriends();
                UpdateFriendRequestsCount();
            });
        }

        private void OnFriendRequestReceived(string username)
        {
            Dispatcher.Invoke(() => 
            {
                UpdateFriendRequestsCount();
                LoadFriends();
            });
        }

        private void OnPrivateMessageReceived(string sender, string message)
        {
            Dispatcher.Invoke(() => 
            {
                if (!_openChatWindows.ContainsKey(sender))
                {
                    // Show alert or open window?
                    // For now, just play sound if not open
                    _messageSound?.Play();
                    
                    // Optionally open chat window automatically
                    OpenChatWindow(sender);
                }
            });
        }

        private void OnUserStatusChanged(string username, string status)
        {
            Dispatcher.Invoke(() =>
            {
                // Determine status value
                int statusValue = 6;
                switch (status)
                {
                    case "En ligne": statusValue = 0; break;
                    case "Occupé": statusValue = 1; break;
                    case "Absent": statusValue = 2; break;
                    case "En appel": statusValue = 3; break;
                    case "Ne pas déranger": statusValue = 4; break;
                    default: statusValue = 6; break;
                }

                // Update tracking to prevent overwriting by polling
                _previousStatuses[username] = statusValue;
                _lastSignalRUpdate[username] = DateTime.Now;
                _blinkingUntil[username] = DateTime.Now.AddSeconds(5);

                if (FriendsList.ItemsSource is List<Friend> items)
                {
                    var friend = items.FirstOrDefault(f => f.Username == username);
                    if (friend != null)
                    {
                        // Update Status
                        friend.StatusText = status;
                        
                        SolidColorBrush statusBrush = Brushes.Gray;
                        switch (statusValue)
                        {
                            case 0: statusBrush = Brushes.Green; break;
                            case 1: statusBrush = Brushes.Red; break;
                            case 2: statusBrush = Brushes.Orange; break;
                            case 3: statusBrush = Brushes.DarkRed; break;
                            case 4: statusBrush = Brushes.Purple; break;
                            default: statusBrush = Brushes.Gray; break;
                        }
                        friend.StatusColor = statusBrush;
                        friend.NameFontWeight = statusValue != 6 ? FontWeights.Bold : FontWeights.Normal;
                        friend.IsBlinking = true;

                        // Play Sound
                        try
                        {
                            if (statusValue == 0) _onlineSound?.Play();
                            else if (statusValue == 6) _offlineSound?.Play();
                        }
                        catch { }

                        // Re-sort and Refresh
                        var sortedFriends = items.OrderBy(f => f.StatusColor == Brushes.Gray).ThenBy(f => f.Name).ToList();
                        FriendsList.ItemsSource = null;
                        FriendsList.ItemsSource = sortedFriends;
                    }
                    else
                    {
                        LoadFriends();
                    }
                }
            });
        }

        private void CheckForIncomingMessages()
        {
            // Deprecated by SignalR
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

            var chatWindow = new ChatWindow(_username, partnerUsername);
            chatWindow.Closed += (s, args) => _openChatWindows.Remove(partnerUsername);
            _openChatWindows.Add(partnerUsername, chatWindow);
            chatWindow.Show();
        }

        private async void MainView_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _refreshTimer.Stop();
            ApiService.Instance.OnPrivateMessageReceived -= OnPrivateMessageReceived;
            ApiService.Instance.OnUserStatusChanged -= OnUserStatusChanged;
            ApiService.Instance.OnFriendRequestAccepted -= OnFriendUpdate;
            ApiService.Instance.OnFriendRemoved -= OnFriendUpdate;
            ApiService.Instance.OnConnectionClosed -= OnConnectionClosed;

            await ApiService.Instance.DisconnectAsync();
        }

        private async void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusCombo.SelectedItem is StatusItem selectedStatus)
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
        private Dictionary<string, DateTime> _lastSignalRUpdate = new Dictionary<string, DateTime>();

        private async void LoadFriends()
        {
            try
            {
                var friends = await ApiService.Instance.GetFriendsAsync();
                
                // Sort: Online (0-5) first, Offline (6) last
                var sortedFriends = friends.OrderBy(f => f.StatusValue == 6).ThenBy(f => f.DisplayName);

                var friendViewModels = sortedFriends.Select(f => {
                    bool hasAvatar = !string.IsNullOrEmpty(f.AvatarPath) && File.Exists(f.AvatarPath);
                    
                    // Check if we have a recent SignalR update (within last 5 seconds)
                    // If so, use the cached status from _previousStatuses instead of API data
                    // This prevents stale API data from overwriting fresh SignalR events
                    int effectiveStatusValue = f.StatusValue;
                    if (_lastSignalRUpdate.ContainsKey(f.Username) && 
                        (DateTime.Now - _lastSignalRUpdate[f.Username]).TotalSeconds < 5 &&
                        _previousStatuses.ContainsKey(f.Username))
                    {
                        effectiveStatusValue = _previousStatuses[f.Username];
                    }

                    SolidColorBrush statusBrush = Brushes.Gray;
                    string statusText = f.Status;

                    // Re-map status text if we are using effective value
                    if (effectiveStatusValue != f.StatusValue)
                    {
                        switch (effectiveStatusValue)
                        {
                            case 0: statusText = "En ligne"; break;
                            case 1: statusText = "Occupé"; break;
                            case 2: statusText = "Absent"; break;
                            case 3: statusText = "En appel"; break;
                            case 4: statusText = "Ne pas déranger"; break;
                            default: statusText = "Hors ligne"; break;
                        }
                    }

                    // Override if Blocked
                    if (f.IsBlocked)
                    {
                        statusText = "Bloqué";
                        statusBrush = Brushes.Red;
                    }
                    else
                    {
                        switch (effectiveStatusValue)
                        {
                            case 0: statusBrush = Brushes.Green; break; // En ligne
                            case 1: statusBrush = Brushes.Red; break;   // Occupé
                            case 2: statusBrush = Brushes.Orange; break; // Absent
                            case 3: statusBrush = Brushes.DarkRed; break; // En appel
                            case 4: statusBrush = Brushes.Purple; break; // Ne pas déranger
                            default: statusBrush = Brushes.Gray; break; // Hors ligne
                        }
                    }

                    // Check for status change
                    bool isBlinking = false;
                    if (_previousStatuses.ContainsKey(f.Username))
                    {
                        if (_previousStatuses[f.Username] != effectiveStatusValue)
                        {
                            // Status changed, set blinking expiration to Now + 5 seconds
                            _blinkingUntil[f.Username] = DateTime.Now.AddSeconds(5);

                            // Play Sound Effects
                            try
                            {
                                if (effectiveStatusValue == 0) // Came Online
                                {
                                    _onlineSound?.Play();
                                }
                                else if (effectiveStatusValue == 6) // Went Offline
                                {
                                    _offlineSound?.Play();
                                }
                            }
                            catch { /* Ignore playback errors */ }
                        }
                    }
                    _previousStatuses[f.Username] = effectiveStatusValue;

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

                    return new Friend
                    {
                        Name = f.DisplayName,
                        StatusText = statusText,
                        StatusColor = statusBrush,
                        AvatarPath = hasAvatar ? f.AvatarPath : null,
                        Username = f.Username,
                        AvatarVisibility = hasAvatar ? Visibility.Visible : Visibility.Collapsed,
                        PlaceholderVisibility = hasAvatar ? Visibility.Collapsed : Visibility.Visible,
                        NameFontWeight = effectiveStatusValue != 6 ? FontWeights.Bold : FontWeights.Normal,
                        IsBlinking = isBlinking,
                        IsBlocked = f.IsBlocked,
                        BlockIcon = f.IsBlocked ? "\xE785" : "\xE72E", // Unlock vs Lock
                        BlockToolTip = f.IsBlocked ? "Débloquer" : "Bloquer",
                        BlockOverlayVisibility = f.IsBlocked ? Visibility.Visible : Visibility.Collapsed
                    };
                }).ToList();

                FriendsList.ItemsSource = friendViewModels;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading friends: {ex.Message}");
            }
        }

        private async void LoadUserProfile(string username)
        {
            try
            {
                var profile = await ApiService.Instance.GetUserProfileAsync(username);

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading profile: {ex.Message}");
                UsernameText.Text = username;
            }
        }

        private async void Logout_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                // Only attempt network calls if this is a manual logout (sender is Button)
                if (sender is Button) 
                {
                    await ApiService.Instance.UpdateStatusAsync(6); // Offline
                    await ApiService.Instance.DisconnectAsync();
                }
                else 
                {
                    // Forced logout - just ensure local cleanup
                    // We can still call DisconnectAsync as it handles its own state check
                    _ = ApiService.Instance.DisconnectAsync(); 
                }
            }
            catch { }

            // Close all other windows
            var windows = new List<Window>(Application.Current.Windows.Cast<Window>());
            foreach (var window in windows)
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
                var friends = FriendsList.ItemsSource as List<Friend>;
                var friend = friends?.FirstOrDefault(f => f.Username == username);
                bool isBlocked = friend?.IsBlocked ?? false;

                if (isBlocked)
                {
                    // Unblock
                    var result = await ApiService.Instance.UnblockUserAsync(username);
                    if (result.Success)
                    {
                        LoadFriends();
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
                        var result = await ApiService.Instance.BlockUserAsync(username);
                        if (result.Success)
                        {
                            LoadFriends();
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
                    LoadFriends();
                }
            }
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
            if (_addFriendWindow == null || !_addFriendWindow.IsLoaded)
            {
                _addFriendWindow = new AddFriendWindow(_username);
                _addFriendWindow.Closed += (s, args) => _addFriendWindow = null;
                _addFriendWindow.FriendAdded += () => 
                {
                    LoadFriends();
                    UpdateFriendRequestsCount();
                };
                _addFriendWindow.SelectReceivedRequestsTab();
                _addFriendWindow.Show();
            }
            else
            {
                if (_addFriendWindow.WindowState == WindowState.Minimized)
                {
                    _addFriendWindow.WindowState = WindowState.Normal;
                }
                _addFriendWindow.SelectReceivedRequestsTab();
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
        public string Username { get; set; } = "";
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
