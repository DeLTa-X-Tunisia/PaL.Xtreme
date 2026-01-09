using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Data;
using System.Globalization;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class AddFriendWindow : Window
    {
        private string _currentUsername;

        public event Action? FriendAdded;

        public AddFriendWindow(string username)
        {
            InitializeComponent();
            _currentUsername = username;
            
            // Load all users initially
            PerformSearch("");
            LoadRequests();

            // Subscribe to SignalR events
            ApiService.Instance.OnFriendRequestReceived += OnFriendUpdate;
            ApiService.Instance.OnFriendRequestAccepted += OnFriendUpdate;
            ApiService.Instance.OnFriendRemoved += OnFriendUpdate;
        }

        // Window Chrome Handlers
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return;
            this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public void SelectReceivedRequestsTab()
        {
            if (MainTabControl != null && MainTabControl.Items.Count > 1)
            {
                MainTabControl.SelectedIndex = 1;
            }
        }

        private string BuildAvatarUrl(string? avatarPath)
        {
            var defaultAvatar = System.IO.Path.GetFullPath("Assets/default_avatar.png");
            if (string.IsNullOrEmpty(avatarPath))
                return defaultAvatar;
            
            if (avatarPath.StartsWith("http://") || avatarPath.StartsWith("https://"))
                return avatarPath;
            
            if ((avatarPath.Contains(":\\") || avatarPath.StartsWith("/") || avatarPath.StartsWith("\\")) && File.Exists(avatarPath))
                return avatarPath;
            
            return $"{ApiService.BaseUrl}/{avatarPath.TrimStart('/', '\\')}";
        }

        private void OnFriendUpdate(string username)
        {
            Dispatcher.Invoke(() =>
            {
                LoadRequests();
                PerformSearch(SearchBox.Text.Trim());
                FriendAdded?.Invoke(); // Notify parent window if needed
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            ApiService.Instance.OnFriendRequestReceived -= OnFriendUpdate;
            ApiService.Instance.OnFriendRequestAccepted -= OnFriendUpdate;
            ApiService.Instance.OnFriendRemoved -= OnFriendUpdate;
        }

        private async void LoadRequests()
        {
            var requests = await ApiService.Instance.GetPendingRequestsAsync();
            // Fix Avatar Paths for UI - support server URLs
            foreach (var r in requests)
            {
                r.AvatarPath = BuildAvatarUrl(r.AvatarPath);
            }
            RequestsList.ItemsSource = requests;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PerformSearch(SearchBox.Text.Trim());
        }

        private async void PerformSearch(string query)
        {
            var results = await ApiService.Instance.SearchUsersAsync(query);
            
            // Fix Avatar Paths for UI - support server URLs
            foreach (var r in results)
            {
                r.AvatarPath = BuildAvatarUrl(r.AvatarPath);
            }
            
            UsersList.ItemsSource = results;
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string targetUsername)
            {
                await ApiService.Instance.SendFriendRequestAsync(targetUsername);
                
                // Refresh list to show pending status
                PerformSearch(SearchBox.Text.Trim());
            }
        }

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string requester)
            {
                await ApiService.Instance.RespondToFriendRequestAsync(requester, 1); // 1 = Accept
                LoadRequests();
                PerformSearch(SearchBox.Text.Trim());
                FriendAdded?.Invoke();
            }
        }

        private async void AcceptAdd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string requester)
            {
                await ApiService.Instance.RespondToFriendRequestAsync(requester, 2); // 2 = Accept & Add
                LoadRequests();
                PerformSearch(SearchBox.Text.Trim());
                FriendAdded?.Invoke();
            }
        }

        private async void Decline_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string requester)
            {
                await ApiService.Instance.RespondToFriendRequestAsync(requester, 3); // 3 = Decline
                LoadRequests();
            }
        }

        private void Block_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FriendDto user)
            {
                var blockWindow = new BlockUserWindow(_currentUsername, user.Username, user.DisplayName);
                if (blockWindow.ShowDialog() == true)
                {
                    LoadRequests();
                    PerformSearch(SearchBox.Text.Trim());
                }
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
    }

    public class StatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status && parameter is string targetStatusStr && int.TryParse(targetStatusStr, out int targetStatus))
            {
                return status == targetStatus ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}