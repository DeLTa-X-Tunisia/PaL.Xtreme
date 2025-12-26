using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Data;
using System.Globalization;

namespace PaLX.Client
{
    public partial class AddFriendWindow : Window
    {
        private string _currentUsername;
        private DatabaseService _dbService;

        public AddFriendWindow(string username)
        {
            InitializeComponent();
            _currentUsername = username;
            _dbService = new DatabaseService();
            
            // Load all users initially
            PerformSearch("");
            LoadRequests();
        }

        private void LoadRequests()
        {
            var requests = _dbService.GetPendingRequests(_currentUsername);
            foreach (var r in requests)
            {
                if (string.IsNullOrEmpty(r.AvatarPath) || !File.Exists(r.AvatarPath))
                {
                    r.AvatarPath = null;
                }
            }
            RequestsList.ItemsSource = requests;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PerformSearch(SearchBox.Text.Trim());
        }

        private void PerformSearch(string query)
        {
            var results = _dbService.SearchUsers(query, _currentUsername);
            
            // Fix Avatar Paths for UI
            foreach (var r in results)
            {
                if (string.IsNullOrEmpty(r.AvatarPath) || !File.Exists(r.AvatarPath))
                {
                    r.AvatarPath = null; 
                }
            }
            
            UsersList.ItemsSource = results;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string targetUsername)
            {
                _dbService.SendFriendRequest(_currentUsername, targetUsername);
                
                // Refresh list to show pending status
                PerformSearch(SearchBox.Text.Trim());
            }
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string requester)
            {
                _dbService.RespondToFriendRequest(_currentUsername, requester, 1); // 1 = Accept
                LoadRequests();
                PerformSearch(SearchBox.Text.Trim());
            }
        }

        private void AcceptAdd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string requester)
            {
                _dbService.RespondToFriendRequest(_currentUsername, requester, 2); // 2 = Accept & Add
                LoadRequests();
                PerformSearch(SearchBox.Text.Trim());
            }
        }

        private void Decline_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string requester)
            {
                _dbService.RespondToFriendRequest(_currentUsername, requester, 3); // 3 = Decline
                LoadRequests();
            }
        }

        private void Block_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string requester)
            {
                var blockWindow = new BlockUserWindow(_currentUsername, requester);
                if (blockWindow.ShowDialog() == true)
                {
                    LoadRequests();
                    PerformSearch(SearchBox.Text.Trim());
                }
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