using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.IO;
using System;
using System.Linq;
using System.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
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
        private SoundPlayer? _friendRequestSound;
        private SoundPlayer? _friendAddedSound;
        private MediaPlayer _startupPlayer = new MediaPlayer();
        private Dictionary<string, ChatWindow> _openChatWindows = new Dictionary<string, ChatWindow>();
        private ObservableCollection<Friend> _friendsCollection = new ObservableCollection<Friend>();
        private ObservableCollection<ConversationItem> _conversationsCollection = new ObservableCollection<ConversationItem>();
        
        // Anti-duplicate toast tracking
        private Dictionary<string, DateTime> _lastStatusToastTime = new Dictionary<string, DateTime>();
        private const int STATUS_TOAST_COOLDOWN_MS = 3000; // 3 seconds cooldown per user

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
            _username = username;
            _role = role;
            
            PlayStartupSound();

            // Set DataContext for bindings
            NotificationButton.DataContext = this;

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

            _ = LoadUserProfile(username);
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
            _refreshTimer.Tick += async (s, e) => 
            {
                await LoadFriends();
                // CheckForIncomingMessages(); // Handled by SignalR
            };
            _refreshTimer.Start();

            // Subscribe to SignalR events
            ApiService.Instance.OnPrivateMessageReceived += OnPrivateMessageReceived;
            ApiService.Instance.OnBuzzReceived += OnBuzzReceived;
            ApiService.Instance.OnUserStatusChanged += OnUserStatusChanged;
            ApiService.Instance.OnImageRequestReceived += OnImageRequestReceived;
            ApiService.Instance.OnVideoRequestReceived += OnVideoRequestReceived;
            ApiService.Instance.OnAudioRequestReceived += OnAudioRequestReceived;
            ApiService.Instance.OnFileRequestReceived += OnFileRequestReceived;

            // Subscribe to Voice Call
            if (ApiService.Instance.VoiceService != null)
            {
                ApiService.Instance.VoiceService.OnIncomingCall += OnIncomingCall;
            }
            
            // Subscribe to Video Call
            if (ApiService.Instance.VideoService != null)
            {
                ApiService.Instance.VideoService.OnIncomingVideoCall += OnIncomingVideoCall;
            }
            
            // Friend Sync
            ApiService.Instance.OnFriendRequestAccepted += OnFriendAdded;
            ApiService.Instance.OnFriendRequestReceived += OnFriendRequestReceived;
            ApiService.Instance.OnFriendRemoved += OnFriendRemovedEvent;

            // Block Sync
            ApiService.Instance.OnUserBlocked += OnUserBlocked;
            ApiService.Instance.OnUserBlockedBy += OnUserBlocked; 
            ApiService.Instance.OnUserUnblocked += OnUserUnblocked;
            ApiService.Instance.OnUserUnblockedBy += OnUserUnblocked;

            // System Events
            ApiService.Instance.OnConnectionClosed += OnConnectionClosed;

            this.Closing += MainView_Closing;
            
            InitializeData();

            // Debug Visibility
            this.Loaded += (s, e) => 
            {
                // MessageBox.Show($"RoomsButton Visibility: {RoomsButton.Visibility}");
                RoomsButton.Visibility = Visibility.Visible;
                RoomsTab.Visibility = Visibility.Visible;
                // MessageBox.Show("DEBUG: RoomsButton forced to Visible");
            };
        }

        private async void InitializeData()
        {
            await LoadFriends();
            CheckUnreadConversations();
        }

        // ═══════════════════════════════════════════════════════════════
        // WINDOW CHROME HANDLERS
        // ═══════════════════════════════════════════════════════════════
        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, e);
            }
            else
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MaximizeIcon.Text = "\uE922"; // Maximize icon
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MaximizeIcon.Text = "\uE923"; // Restore icon
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl == null) return;
            
            var tabIndex = MainTabControl.SelectedIndex;
            
            // Tab 0: Friends, Tab 1: Conversations, Tab 2: Rooms
            FriendsList.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            ConversationsList.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            RoomListControl.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
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
                    if (profile != null)
                    {
                        string fullName = $"{profile.LastName} {profile.FirstName}".Trim();
                        newItem.DisplayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fullName.ToLower());
                    }
                    else
                    {
                        newItem.DisplayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(username.ToLower());
                    }
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

        private void OnIncomingCall(string sender)
        {
            Dispatcher.Invoke(() => 
            {
                var callWindow = new VoiceCallWindow(ApiService.Instance.VoiceService!, sender, true);
                callWindow.Show();
            });
        }

        private void OnIncomingVideoCall(string caller, string callId)
        {
            Dispatcher.Invoke(() =>
            {
                // Get display name from friends list if available
                var friend = _friendsCollection.FirstOrDefault(f => f.Username.Equals(caller, StringComparison.OrdinalIgnoreCase));
                string displayName = friend?.Name ?? caller;
                string? avatarPath = friend?.AvatarPath;
                
                // Show toast notification
                ToastService.Info($"📹 Appel vidéo de {displayName}");
                
                // Open video call window for incoming call
                var videoWindow = new VideoCallWindow(
                    ApiService.Instance.VideoService!,
                    caller,
                    displayName,
                    callId,
                    avatarPath);
                videoWindow.Show();
            });
        }

        private void PlayStartupSound()
        {
            try
            {
                string soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds", "startup.mp3");
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
            Dispatcher.Invoke(() =>
            {
                // Force Logout logic immediately
                Logout_Click(null!, null!);
                new DisconnectionWindow().Show();
            });
        }

        private void OnUserBlocked(string username)
        {
            Dispatcher.Invoke(() =>
            {
                var friend = _friendsCollection.FirstOrDefault(f => f.Username == username);
                if (friend != null)
                {
                    friend.IsBlocked = true;
                    friend.BlockIcon = "\xE785";
                    friend.BlockToolTip = "Débloquer";
                    friend.BlockOverlayVisibility = Visibility.Visible;
                    friend.StatusText = "Bloqué";
                    friend.StatusColor = Brushes.Red;
                    friend.NameFontWeight = FontWeights.Normal;
                }
            });
        }

        private void OnUserUnblocked(string username)
        {
            Dispatcher.Invoke(async () =>
            {
                var friend = _friendsCollection.FirstOrDefault(f => f.Username == username);
                if (friend != null)
                {
                    friend.IsBlocked = false;
                    friend.BlockIcon = "\xE72E";
                    friend.BlockToolTip = "Bloquer";
                    friend.BlockOverlayVisibility = Visibility.Collapsed;
                }
                await LoadFriends();
            });
        }

        private void OnFriendAdded(string username)
        {
            Dispatcher.Invoke(async () => 
            {
                try { _friendAddedSound?.Play(); } catch { }
                await LoadFriends();
                UpdateFriendRequestsCount();
            });
        }

        private void OnFriendRemovedEvent(string username)
        {
            Dispatcher.Invoke(async () => 
            {
                await LoadFriends();
                UpdateFriendRequestsCount();
            });
        }

        // Deprecated generic handler
        private void OnFriendUpdate(string username) 
        {
            Dispatcher.Invoke(async () => 
            {
                await LoadFriends();
                UpdateFriendRequestsCount();
            });
        }

        private void OnFriendRequestReceived(string username)
        {
            Dispatcher.Invoke(async () => 
            {
                try { _friendRequestSound?.Play(); } catch { }
                UpdateFriendRequestsCount();
                await LoadFriends();
            });
        }

        private void OnImageRequestReceived(int id, string sender, string filename, string url)
        {
            // Ignore if sender is empty or if it's ourselves
            if (string.IsNullOrEmpty(sender) || sender == _username)
                return;
                
            Dispatcher.Invoke(async () => 
            {
                bool isWindowOpen = _openChatWindows.ContainsKey(sender);
                await AddOrUpdateConversation(sender, "📷 Image reçue", DateTime.Now, !isWindowOpen);

                if (!isWindowOpen)
                {
                    OpenChatWindow(sender);
                    isWindowOpen = true;
                }

                if (isWindowOpen)
                {
                    var window = _openChatWindows[sender];
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                }
                
                _messageSound?.Play();
            });
        }

        private void OnVideoRequestReceived(int id, string sender, string filename, string url)
        {
            // Ignore if sender is empty or if it's ourselves
            if (string.IsNullOrEmpty(sender) || sender == _username)
                return;
                
            Dispatcher.Invoke(async () => 
            {
                bool isWindowOpen = _openChatWindows.ContainsKey(sender);
                await AddOrUpdateConversation(sender, "🎥 Vidéo reçue", DateTime.Now, !isWindowOpen);

                if (!isWindowOpen)
                {
                    OpenChatWindow(sender);
                    isWindowOpen = true;
                }

                if (isWindowOpen)
                {
                    var window = _openChatWindows[sender];
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                }
                
                _messageSound?.Play();
            });
        }

        private void OnAudioRequestReceived(int id, string sender, string filename, string url)
        {
            // Ignore if sender is empty or if it's ourselves
            if (string.IsNullOrEmpty(sender) || sender == _username)
                return;
                
            Dispatcher.Invoke(async () => 
            {
                bool isWindowOpen = _openChatWindows.ContainsKey(sender);
                await AddOrUpdateConversation(sender, "🎵 Audio reçu", DateTime.Now, !isWindowOpen);

                if (!isWindowOpen)
                {
                    OpenChatWindow(sender);
                    isWindowOpen = true;
                }

                if (isWindowOpen)
                {
                    var window = _openChatWindows[sender];
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                }
                
                _messageSound?.Play();
            });
        }

        private void OnFileRequestReceived(int id, string sender, string filename, string url)
        {
            // Ignore if sender is empty or if it's ourselves
            if (string.IsNullOrEmpty(sender) || sender == _username)
                return;
                
            Dispatcher.Invoke(async () => 
            {
                bool isWindowOpen = _openChatWindows.ContainsKey(sender);
                await AddOrUpdateConversation(sender, "📄 Fichier reçu", DateTime.Now, !isWindowOpen);

                if (!isWindowOpen)
                {
                    OpenChatWindow(sender);
                    isWindowOpen = true;
                }

                if (isWindowOpen)
                {
                    var window = _openChatWindows[sender];
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                }
                
                _messageSound?.Play();
            });
        }

        private void OnPrivateMessageReceived(string sender, string message, int id)
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
                    var chatWindow = new ChatWindow(_username, sender);
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
                    case "Hors ligne": statusValue = 6; break;
                    default: statusValue = 6; break;
                }

                // Get previous status (default to offline if unknown)
                int previousStatus = _previousStatuses.ContainsKey(username) ? _previousStatuses[username] : 6;
                bool statusChanged = previousStatus != statusValue;

                // Get friend info for toast notification
                var friend = _friendsCollection.FirstOrDefault(f => f.Username == username);

                // Check if we should show toast (anti-duplicate cooldown)
                bool canShowToast = true;
                if (_lastStatusToastTime.ContainsKey(username))
                {
                    var timeSinceLastToast = (DateTime.Now - _lastStatusToastTime[username]).TotalMilliseconds;
                    canShowToast = timeSinceLastToast > STATUS_TOAST_COOLDOWN_MS;
                }

                if (statusChanged)
                {
                    // Play appropriate sound and show toast
                    try
                    {
                        if (statusValue == 6 && previousStatus != 6)
                        {
                            // User went OFFLINE (was online/away/etc, now offline)
                            _offlineSound?.Play();
                            
                            // Show offline toast (with cooldown check)
                            if (friend != null && canShowToast)
                            {
                                ToastService.FriendStatus(friend.Name, friend.AvatarPath, false);
                                _lastStatusToastTime[username] = DateTime.Now;
                            }
                        }
                        else if (previousStatus == 6 && statusValue == 0)
                        {
                            // User came ONLINE (was offline, now online)
                            _onlineSound?.Play();
                            // Only blink when coming online, not when going offline
                            _blinkingUntil[username] = DateTime.Now.AddSeconds(3);
                            
                            // Show online toast (with cooldown check)
                            if (friend != null && canShowToast)
                            {
                                ToastService.FriendStatus(friend.Name, friend.AvatarPath, true);
                                _lastStatusToastTime[username] = DateTime.Now;
                            }
                        }
                    }
                    catch { }
                }

                // Update tracking to prevent overwriting by polling
                _previousStatuses[username] = statusValue;
                _lastSignalRUpdate[username] = DateTime.Now;

                // Immediately update UI for this friend without waiting for full refresh
                if (friend != null)
                {
                    // Update status immediately
                    SolidColorBrush statusBrush = Brushes.Gray;
                    string statusText = status;

                    switch (statusValue)
                    {
                        case 0: statusBrush = Brushes.Green; statusText = "En ligne"; break;
                        case 1: statusBrush = Brushes.Red; statusText = "Occupé"; break;
                        case 2: statusBrush = Brushes.Orange; statusText = "Absent"; break;
                        case 3: statusBrush = Brushes.DarkRed; statusText = "En appel"; break;
                        case 4: statusBrush = Brushes.Purple; statusText = "Ne pas déranger"; break;
                        default: statusBrush = Brushes.Gray; statusText = "Hors ligne"; break;
                    }

                    friend.StatusText = statusText;
                    friend.StatusColor = statusBrush;
                    friend.StatusValue = statusValue;
                    friend.NameFontWeight = statusValue != 6 ? FontWeights.Bold : FontWeights.Normal;
                    friend.IsBlinking = _blinkingUntil.ContainsKey(username) && DateTime.Now < _blinkingUntil[username];
                }

                // Also do a background refresh to sync any other changes
                await LoadFriends();
            });
        }

        private void CheckForIncomingMessages()
        {
            // Deprecated by SignalR
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

            var chatWindow = new ChatWindow(_username, partnerUsername);
            chatWindow.Closed += (s, args) => _openChatWindows.Remove(partnerUsername);
            _openChatWindows.Add(partnerUsername, chatWindow);
            chatWindow.Show();
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
            ApiService.Instance.OnConnectionClosed -= OnConnectionClosed;

            try
            {
                await ApiService.Instance.DisconnectAsync();
            }
            catch { }
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

        private async Task LoadFriends()
        {
            try
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
                    
                    int effectiveStatusValue = f.StatusValue;
                    if (_lastSignalRUpdate.ContainsKey(f.Username) && 
                        (DateTime.Now - _lastSignalRUpdate[f.Username]).TotalSeconds < 5 &&
                        _previousStatuses.ContainsKey(f.Username))
                    {
                        effectiveStatusValue = _previousStatuses[f.Username];
                    }

                    SolidColorBrush statusBrush = Brushes.Gray;
                    string statusText = f.Status;

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

                    if (f.IsBlocked)
                    {
                        statusText = "Bloqué";
                        statusBrush = Brushes.Red;
                    }
                    else
                    {
                        switch (effectiveStatusValue)
                        {
                            case 0: statusBrush = Brushes.Green; break;
                            case 1: statusBrush = Brushes.Red; break;
                            case 2: statusBrush = Brushes.Orange; break;
                            case 3: statusBrush = Brushes.DarkRed; break;
                            case 4: statusBrush = Brushes.Purple; break;
                            default: statusBrush = Brushes.Gray; break;
                        }
                    }

                    bool isBlinking = false;
                    int previousStatus = _previousStatuses.ContainsKey(f.Username) ? _previousStatuses[f.Username] : 6;
                    bool statusChanged = previousStatus != effectiveStatusValue;
                    
                    // Only trigger sounds/blinking from polling if not recently updated by SignalR
                    bool recentlyUpdatedBySignalR = _lastSignalRUpdate.ContainsKey(f.Username) && 
                        (DateTime.Now - _lastSignalRUpdate[f.Username]).TotalSeconds < 10;
                    
                    if (statusChanged && !recentlyUpdatedBySignalR)
                    {
                        try
                        {
                            if (effectiveStatusValue == 6)
                            {
                                // User went OFFLINE - play sound, no blinking
                                _offlineSound?.Play();
                            }
                            else if (previousStatus == 6 && effectiveStatusValue == 0)
                            {
                                // User came ONLINE - play sound and blink
                                _onlineSound?.Play();
                                _blinkingUntil[f.Username] = DateTime.Now.AddSeconds(3);
                            }
                        }
                        catch { }
                    }
                    _previousStatuses[f.Username] = effectiveStatusValue;

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

                    // Find existing or create new
                    var existingFriend = _friendsCollection.FirstOrDefault(x => x.Username == f.Username);
                    if (existingFriend != null)
                    {
                        // Update properties
                        existingFriend.Name = f.DisplayName;
                        existingFriend.StatusText = statusText;
                        existingFriend.StatusColor = statusBrush;
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
                        
                        // Force refresh of sorting property if needed (usually handled by PropertyChanged)
                    }
                    else
                    {
                        // Add new
                        _friendsCollection.Add(new Friend
                        {
                            Name = f.DisplayName,
                            StatusText = statusText,
                            StatusColor = statusBrush,
                            StatusValue = effectiveStatusValue,
                            AvatarPath = hasAvatar ? f.AvatarPath : null,
                            Username = f.Username,
                            AvatarVisibility = hasAvatar ? Visibility.Visible : Visibility.Collapsed,
                            PlaceholderVisibility = hasAvatar ? Visibility.Collapsed : Visibility.Visible,
                            NameFontWeight = effectiveStatusValue != 6 ? FontWeights.Bold : FontWeights.Normal,
                            IsBlinking = isBlinking,
                            IsBlocked = f.IsBlocked,
                            BlockIcon = f.IsBlocked ? "\xE785" : "\xE72E",
                            BlockToolTip = f.IsBlocked ? "Débloquer" : "Bloquer",
                            BlockOverlayVisibility = f.IsBlocked ? Visibility.Visible : Visibility.Collapsed
                        });
                    }
                }
                
                // Refresh sorting
                CollectionViewSource.GetDefaultView(_friendsCollection).Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading friends: {ex.Message}");
            }
        }

        private async Task LoadUserProfile(string username)
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
                // Unsubscribe from connection closed event to prevent loop
                ApiService.Instance.OnConnectionClosed -= OnConnectionClosed;

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
            userProfiles.ProfileSaved += async () => await LoadUserProfile(_username);
            userProfiles.Show();
        }

        private SettingsWindow? _settingsWindow;
        
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // Check if window already exists and is visible
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.Activate();
                _settingsWindow.Focus();
                return;
            }
            
            // Create new window
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (s, args) => _settingsWindow = null;
            _settingsWindow.Show();
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
                _addFriendWindow.FriendAdded += async () => await LoadFriends(); // Instant refresh on action
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
                var friend = _friendsCollection.FirstOrDefault(f => f.Username == username);
                bool isBlocked = friend?.IsBlocked ?? false;

                if (isBlocked)
                {
                    // Unblock
                    var result = await ApiService.Instance.UnblockUserAsync(username);
                    if (result.Success)
                    {
                        await LoadFriends();
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
                            await LoadFriends();
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
                    await LoadFriends();
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
                _addFriendWindow.FriendAdded += async () => 
                {
                    await LoadFriends();
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

        private void RoomsButton_Click(object sender, RoutedEventArgs e)
        {
            // Logic to check subscription could go here
            // For now, we allow access
            MainTabControl.SelectedIndex = 2;
        }
    }

    public class StatusItem
    {
        public string Name { get; set; } = "";
        public SolidColorBrush ColorBrush { get; set; } = Brushes.Gray;
    }

    public class Friend : INotifyPropertyChanged
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

        private SolidColorBrush _statusColor = Brushes.Gray;
        public SolidColorBrush StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(); } }

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

        private string? _avatarPath;
        public string? AvatarPath { get => _avatarPath; set { _avatarPath = value; OnPropertyChanged(); } }

        public string Username { get; set; } = "";

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
