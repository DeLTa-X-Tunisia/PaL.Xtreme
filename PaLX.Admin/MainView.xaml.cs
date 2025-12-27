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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;

namespace PaLX.Admin
{
    public partial class MainView : Window
    {
        public string CurrentUsername { get; private set; }
        public string CurrentRole { get; private set; }

        private System.Windows.Threading.DispatcherTimer _refreshTimer;
        private SoundPlayer? _onlineSound;
        private SoundPlayer? _offlineSound;
        private SoundPlayer? _messageSound;
        private SoundPlayer? _friendRequestSound;
        private SoundPlayer? _friendAddedSound;
        private MediaPlayer _startupPlayer = new MediaPlayer();
        private Dictionary<string, ChatWindow> _openChatWindows = new Dictionary<string, ChatWindow>();
        private ObservableCollection<FriendItem> _friendsCollection = new ObservableCollection<FriendItem>();
        private ObservableCollection<ConversationItem> _conversationsCollection = new ObservableCollection<ConversationItem>();

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

        // Total Unread Messages Property
        public static readonly DependencyProperty TotalUnreadCountProperty =
            DependencyProperty.Register("TotalUnreadCount", typeof(int), typeof(MainView), new PropertyMetadata(0));

        public int TotalUnreadCount
        {
            get { return (int)GetValue(TotalUnreadCountProperty); }
            set 
            { 
                SetValue(TotalUnreadCountProperty, value); 
                SetValue(TotalUnreadVisibilityProperty, value > 0 ? Visibility.Visible : Visibility.Collapsed);
            }
        }

        public static readonly DependencyProperty TotalUnreadVisibilityProperty =
            DependencyProperty.Register("TotalUnreadVisibility", typeof(Visibility), typeof(MainView), new PropertyMetadata(Visibility.Collapsed));

        public Visibility TotalUnreadVisibility
        {
            get { return (Visibility)GetValue(TotalUnreadVisibilityProperty); }
            set { SetValue(TotalUnreadVisibilityProperty, value); }
        }

        public MainView(string username, string role)
        {
            InitializeComponent();
            CurrentUsername = username;
            CurrentRole = role;
            
            PlayStartupSound();

            // Set DataContext for bindings
            NotificationButton.DataContext = this;
            
            // Subscribe to SignalR events
            ApiService.Instance.OnPrivateMessageReceived += OnPrivateMessageReceived;
            ApiService.Instance.OnBuzzReceived += OnBuzzReceived;
            ApiService.Instance.OnUserStatusChanged += OnUserStatusChanged;
            ApiService.Instance.OnImageRequestReceived += OnImageRequestReceived;
            
            // Friend Sync
            ApiService.Instance.OnFriendRequestAccepted += OnFriendAdded;
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
                _friendRequestSound = new SoundPlayer(Path.Combine(baseDir, "Assets", "Sounds", "friend_request.wav"));
                _friendAddedSound = new SoundPlayer(Path.Combine(baseDir, "Assets", "Sounds", "friend_added.wav"));
                // Preload
                _onlineSound.LoadAsync();
                _offlineSound.LoadAsync();
                _messageSound.LoadAsync();
                _friendRequestSound.LoadAsync();
                _friendAddedSound.LoadAsync();
            }
            catch { /* Ignore sound errors */ }

            FriendsList.ItemsSource = _friendsCollection;
            ConversationsList.ItemsSource = _conversationsCollection;
            
            // Setup Sorting
            var view = (ListCollectionView)CollectionViewSource.GetDefaultView(_friendsCollection);
            view.IsLiveSorting = true;
            view.LiveSortingProperties.Add("StatusSortOrder");
            view.LiveSortingProperties.Add("Name");
            view.SortDescriptions.Add(new SortDescription("StatusSortOrder", ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

            // Setup Timer
            _refreshTimer = new System.Windows.Threading.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += async (s, e) => 
            {
                await LoadFriendsAsync();
            };
            _refreshTimer.Start();

            LoadStatuses();
            Loaded += async (s, e) => 
            {
                await LoadUserProfileAsync(username);
                await LoadFriendsAsync();
                UpdateFriendRequestsCount();
                CheckUnreadConversations();
            };

            this.Closing += MainView_Closing;
        }

        private async void CheckUnreadConversations()
        {
            try
            {
                var senders = await ApiService.Instance.GetUnreadConversationsAsync();
                foreach (var sender in senders)
                {
                    await AddOrUpdateConversation(sender, "Nouveau message", DateTime.Now, true);
                }
            }
            catch { }
        }

        private async Task AddOrUpdateConversation(string username, string lastMessage, DateTime time, bool isUnread)
        {
            var existing = _conversationsCollection.FirstOrDefault(c => c.Username == username);
            if (existing != null)
            {
                existing.LastMessage = lastMessage;
                existing.LastMessageTime = time;
                if (isUnread) existing.UnreadCount++;

                // Refresh friend status if currently unknown
                if (!existing.IsFriend)
                {
                    var friend = _friendsCollection.FirstOrDefault(f => f.Username == username);
                    if (friend != null)
                    {
                        existing.DisplayName = friend.Name;
                        existing.AvatarPath = friend.AvatarPath;
                        existing.IsFriend = true;
                    }
                }

                _conversationsCollection.Move(_conversationsCollection.IndexOf(existing), 0);
            }
            else
            {
                var friend = _friendsCollection.FirstOrDefault(f => f.Username == username);
                var newItem = new ConversationItem
                {
                    Username = username,
                    LastMessage = lastMessage,
                    LastMessageTime = time,
                    UnreadCount = isUnread ? 1 : 0
                };

                if (friend != null)
                {
                    newItem.DisplayName = friend.Name;
                    newItem.AvatarPath = friend.AvatarPath;
                    newItem.IsFriend = true;
                }
                else
                {
                    var profile = await ApiService.Instance.GetUserProfileAsync(username);
                    newItem.DisplayName = profile != null ? $"{profile.LastName} {profile.FirstName}" : username;
                    newItem.AvatarPath = profile?.AvatarPath;
                    newItem.IsFriend = false;
                }
                
                _conversationsCollection.Insert(0, newItem);
            }
            
            UpdateTotalUnreadCount();
        }

        private void UpdateTotalUnreadCount()
        {
            TotalUnreadCount = _conversationsCollection.Sum(c => c.UnreadCount);
        }

        private void ConversationsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ConversationsList.SelectedItem is ConversationItem item)
            {
                OpenChatWindow(item.Username);
                // Remove from list when opened (as it is read)
                _conversationsCollection.Remove(item);
                UpdateTotalUnreadCount();
            }
        }

        private void PlayStartupSound()
        {
            try
            {
                string soundPath = @"C:\Users\azizi\OneDrive\Desktop\PaL.Xtreme\start_sound\admin_start.mp3";
                if (File.Exists(soundPath))
                {
                    _startupPlayer.Open(new Uri(soundPath));
                    _startupPlayer.Play();
                }
            }
            catch { /* Ignore startup sound errors */ }
        }

        private void OnConnectionClosed()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var maintenance = new MaintenanceWindow();
                maintenance.ShowDialog();
                
                // Force Logout logic
                Logout_Click(null!, null!);
            });
        }

        private void OnFriendAdded(string username)
        {
            Dispatcher.Invoke(async () => 
            {
                try { _friendAddedSound?.Play(); } catch { }
                await LoadFriendsAsync();
                UpdateFriendRequestsCount();
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
                var friend = _friendsCollection.FirstOrDefault(f => f.Username == username);
                if (friend != null)
                {
                    friend.StatusText = "Bloqué";
                    friend.StatusColor = Brushes.Red;
                    friend.NameFontWeight = FontWeights.Normal;
                    // No need to refresh, INotifyPropertyChanged handles it
                }
            });
        }

        private void OnUserUnblocked(string username)
        {
             Dispatcher.Invoke(async () => await LoadFriendsAsync());
        }

        private void OnImageRequestReceived(int id, string sender, string filename, string url)
        {
            Dispatcher.Invoke(async () => 
            {
                bool isWindowOpen = _openChatWindows.ContainsKey(sender);
                await AddOrUpdateConversation(sender, "📷 Image reçue", DateTime.Now, !isWindowOpen);

                if (isWindowOpen)
                {
                    var window = _openChatWindows[sender];
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                }
                else
                {
                    _messageSound?.Play();
                }
            });
        }

        private void OnPrivateMessageReceived(string sender, string message)
        {
            Dispatcher.Invoke(async () => 
            {
                // 1. Check if this is an Offline catch-up message
                if (message == "Nouveau message (Offline)")
                {
                    // Treat as unread notification, do not pop up
                    await AddOrUpdateConversation(sender, message, DateTime.Now, true);
                    _messageSound?.Play();
                    return;
                }

                // 2. Real-time message
                bool isWindowOpen = _openChatWindows.ContainsKey(sender);
                
                if (!isWindowOpen)
                {
                    // Auto-open for real-time messages
                    OpenChatWindow(sender);
                    isWindowOpen = true;
                }

                // If window is open, we don't need to add it to the "Discussions" list 
                // because OpenChatWindow removes it from there.
                
                if (isWindowOpen)
                {
                    var window = _openChatWindows[sender];
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                    
                    _messageSound?.Play();
                }
            });
        }

        private void OnBuzzReceived(string sender)
        {
            Dispatcher.Invoke(() => 
            {
                if (_openChatWindows.ContainsKey(sender))
                {
                    var window = _openChatWindows[sender];
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                }
                else
                {
                    // Open new window and trigger buzz
                    var chatWindow = new ChatWindow(CurrentUsername, sender);
                    chatWindow.Closed += (s, args) => _openChatWindows.Remove(sender);
                    _openChatWindows.Add(sender, chatWindow);
                    chatWindow.Show();
                    
                    // Manually trigger buzz since it missed the event
                    chatWindow.TriggerBuzz();
                }
            });
        }

        private void OnUserStatusChanged(string username, string status)
        {
            Dispatcher.Invoke(async () =>
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

                // Play sound if status changed or new
                if (!_previousStatuses.ContainsKey(username) || _previousStatuses[username] != statusValue)
                {
                    try
                    {
                        if (statusValue == 6) _offlineSound?.Play();
                        else _onlineSound?.Play();
                    }
                    catch { }
                    _blinkingUntil[username] = DateTime.Now.AddSeconds(5);
                }

                // Update tracking to prevent overwriting by polling
                _previousStatuses[username] = statusValue;
                _lastSignalRUpdate[username] = DateTime.Now;
                
                // Trigger refresh to update UI via ObservableCollection logic
                await LoadFriendsAsync();
            });
        }

        private void OpenChatWindow(string partnerUsername)
        {
            var item = _conversationsCollection.FirstOrDefault(c => c.Username == partnerUsername);
            if (item != null) 
            {
                _conversationsCollection.Remove(item);
                UpdateTotalUnreadCount();
            }

            if (_openChatWindows.ContainsKey(partnerUsername))
            {
                var window = _openChatWindows[partnerUsername];
                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                window.Activate();
                return;
            }

            var chatWindow = new ChatWindow(CurrentUsername, partnerUsername);
            chatWindow.Closed += (s, args) => _openChatWindows.Remove(partnerUsername);
            _openChatWindows.Add(partnerUsername, chatWindow);
            chatWindow.Show();
        }

        private void OnFriendRequestReceived(string username)
        {
            Dispatcher.Invoke(() => 
            {
                UpdateFriendRequestsCount();
                try { _friendRequestSound?.Play(); } catch { }
            });
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
            _refreshTimer.Stop();
            ApiService.Instance.OnPrivateMessageReceived -= OnPrivateMessageReceived;
            ApiService.Instance.OnBuzzReceived -= OnBuzzReceived;
            ApiService.Instance.OnUserStatusChanged -= OnUserStatusChanged;
            ApiService.Instance.OnImageRequestReceived -= OnImageRequestReceived;
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
        private Dictionary<string, DateTime> _lastSignalRUpdate = new Dictionary<string, DateTime>();

        private async Task LoadFriendsAsync()
        {
            var friends = await ApiService.Instance.GetFriendsAsync();
            
            // Sync with ObservableCollection
            var currentFriends = _friendsCollection.ToList();
            var newFriendUsernames = friends.Select(f => f.Username).ToHashSet();

            // Remove deleted friends
            foreach (var existing in currentFriends)
            {
                if (!newFriendUsernames.Contains(existing.Username))
                {
                    _friendsCollection.Remove(existing);
                }
            }

            foreach (var f in friends)
            {
                bool hasAvatar = !string.IsNullOrEmpty(f.AvatarPath) && File.Exists(f.AvatarPath);
                
                int effectiveStatusValue = f.Status == "Hors ligne" ? 6 : 0;
                if (f.Status == "Occupé") effectiveStatusValue = 1;
                else if (f.Status == "Absent") effectiveStatusValue = 2;
                else if (f.Status == "En appel") effectiveStatusValue = 3;
                else if (f.Status == "Ne pas déranger") effectiveStatusValue = 4;

                if (_lastSignalRUpdate.ContainsKey(f.Username) && 
                    (DateTime.Now - _lastSignalRUpdate[f.Username]).TotalSeconds < 5 &&
                    _previousStatuses.ContainsKey(f.Username))
                {
                    effectiveStatusValue = _previousStatuses[f.Username];
                }

                string statusText = f.Status;
                if (effectiveStatusValue != (f.Status == "Hors ligne" ? 6 : (f.Status == "Occupé" ? 1 : (f.Status == "Absent" ? 2 : (f.Status == "En appel" ? 3 : (f.Status == "Ne pas déranger" ? 4 : 0))))))
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

                Brush statusBrush = Brushes.Gray;
                switch (effectiveStatusValue)
                {
                    case 0: statusBrush = Brushes.Green; break;
                    case 1: statusBrush = Brushes.Red; break;
                    case 2: statusBrush = Brushes.Orange; break;
                    case 3: statusBrush = Brushes.DarkRed; break;
                    case 4: statusBrush = Brushes.Purple; break;
                    default: statusBrush = Brushes.Gray; break;
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
                            if (effectiveStatusValue != 6 && _previousStatuses[f.Username] == 6) // Came Online
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

                // Override if Blocked
                string displayStatus = statusText;
                Brush displayColor = statusBrush;

                if (f.IsBlocked)
                {
                    displayStatus = "Bloqué";
                    displayColor = Brushes.Red;
                }

                // Find existing or create new
                var existingFriend = _friendsCollection.FirstOrDefault(x => x.Username == f.Username);
                if (existingFriend != null)
                {
                    // Update properties
                    existingFriend.Name = f.DisplayName;
                    existingFriend.StatusText = displayStatus;
                    existingFriend.StatusColor = displayColor;
                    existingFriend.StatusValue = effectiveStatusValue;
                    existingFriend.AvatarPath = hasAvatar ? f.AvatarPath : null;
                    existingFriend.AvatarVisibility = hasAvatar ? Visibility.Visible : Visibility.Collapsed;
                    existingFriend.PlaceholderVisibility = hasAvatar ? Visibility.Collapsed : Visibility.Visible;
                    existingFriend.NameFontWeight = effectiveStatusValue != 6 ? FontWeights.Bold : FontWeights.Normal;
                    existingFriend.IsBlinking = isBlinking;
                    existingFriend.IsBlocked = f.IsBlocked;
                    existingFriend.BlockIcon = f.IsBlocked ? "\xE785" : "\xE72E";
                    existingFriend.BlockToolTip = f.IsBlocked ? "Débloquer" : "Bloquer";
                    existingFriend.BlockOverlayVisibility = f.IsBlocked ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    _friendsCollection.Add(new FriendItem
                    {
                        Name = f.DisplayName,
                        StatusText = displayStatus,
                        StatusColor = displayColor,
                        StatusValue = effectiveStatusValue,
                        Username = f.Username,
                        AvatarPath = hasAvatar ? f.AvatarPath : null,
                        AvatarVisibility = hasAvatar ? Visibility.Visible : Visibility.Collapsed,
                        PlaceholderVisibility = hasAvatar ? Visibility.Collapsed : Visibility.Visible,
                        NameFontWeight = effectiveStatusValue != 6 ? FontWeights.Bold : FontWeights.Normal,
                        IsBlinking = isBlinking,
                        IsBlocked = f.IsBlocked,
                        BlockIcon = f.IsBlocked ? "\xE785" : "\xE72E", // Unlock vs Lock
                        BlockToolTip = f.IsBlocked ? "Débloquer" : "Bloquer",
                        BlockOverlayVisibility = f.IsBlocked ? Visibility.Visible : Visibility.Collapsed
                    });
                }
            }
            
            // Sort logic if needed, but ObservableCollection doesn't support Sort() directly.
            // We can rely on CollectionViewSource if we set it up, or just re-order the collection if strictly needed.
            // For now, we'll assume the order from API is roughly correct or acceptable.
            // If strict sorting is needed, we should use CollectionViewSource in the constructor.
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
                // Unsubscribe from connection closed event to prevent loop
                ApiService.Instance.OnConnectionClosed -= OnConnectionClosed;

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

    public class FriendItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private string _name = "";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _statusText = "";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

        private Brush _statusColor = Brushes.Gray;
        public Brush StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(); } }

        private int _statusValue = 6;
        public int StatusValue 
        { 
            get => _statusValue; 
            set 
            { 
                _statusValue = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(StatusSortOrder)); 
            } 
        }

        public string Username { get; set; } = "";

        private string? _avatarPath;
        public string? AvatarPath { get => _avatarPath; set { _avatarPath = value; OnPropertyChanged(); } }

        private Visibility _avatarVisibility = Visibility.Collapsed;
        public Visibility AvatarVisibility { get => _avatarVisibility; set { _avatarVisibility = value; OnPropertyChanged(); } }

        private Visibility _placeholderVisibility = Visibility.Visible;
        public Visibility PlaceholderVisibility { get => _placeholderVisibility; set { _placeholderVisibility = value; OnPropertyChanged(); } }

        private FontWeight _nameFontWeight = FontWeights.Normal;
        public FontWeight NameFontWeight { get => _nameFontWeight; set { _nameFontWeight = value; OnPropertyChanged(); } }

        private bool _isBlinking = false;
        public bool IsBlinking { get => _isBlinking; set { _isBlinking = value; OnPropertyChanged(); } }

        // New properties for Blocking
        private bool _isBlocked = false;
        public bool IsBlocked 
        { 
            get => _isBlocked; 
            set 
            { 
                _isBlocked = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(StatusSortOrder)); 
            } 
        }

        private string _blockIcon = "\xE72B";
        public string BlockIcon { get => _blockIcon; set { _blockIcon = value; OnPropertyChanged(); } }

        private string _blockToolTip = "Bloquer";
        public string BlockToolTip { get => _blockToolTip; set { _blockToolTip = value; OnPropertyChanged(); } }

        private Visibility _blockOverlayVisibility = Visibility.Collapsed;
        public Visibility BlockOverlayVisibility { get => _blockOverlayVisibility; set { _blockOverlayVisibility = value; OnPropertyChanged(); } }

        // Sorting Helper
        public int StatusSortOrder
        {
            get
            {
                if (IsBlocked) return 2; // Blocked at the very bottom
                if (StatusValue == 6) return 1; // Offline
                return 0; // All other statuses (Online, Busy, etc.) are considered "Online" for sorting
            }
        }
    }

    public class ConversationItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private string _displayName = "";
        public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); } }

        private string _lastMessage = "";
        public string LastMessage { get => _lastMessage; set { _lastMessage = value; OnPropertyChanged(); } }

        private DateTime _lastMessageTime;
        public DateTime LastMessageTime { get => _lastMessageTime; set { _lastMessageTime = value; OnPropertyChanged(); } }

        public string TimeDisplay => LastMessageTime.ToString("HH:mm");

        private int _unreadCount = 0;
        public int UnreadCount 
        { 
            get => _unreadCount; 
            set 
            { 
                _unreadCount = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(HasUnread));
                OnPropertyChanged(nameof(UnreadVisibility));
            } 
        }

        public bool HasUnread => UnreadCount > 0;
        public Visibility UnreadVisibility => UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        private string? _avatarPath;
        public string? AvatarPath { get => _avatarPath; set { _avatarPath = value; OnPropertyChanged(); } }

        public string Username { get; set; } = "";

        private bool _isFriend = true;
        public bool IsFriend { get => _isFriend; set { _isFriend = value; OnPropertyChanged(); } }

        public Visibility UnknownSenderVisibility => !IsFriend ? Visibility.Visible : Visibility.Collapsed;
    }
}
