using System.Windows;
using System.Windows.Controls;
using System.IO;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class BlockedUsersWindow : Window
    {
        private string _currentUsername;

        public BlockedUsersWindow(string username)
        {
            InitializeComponent();
            _currentUsername = username;
            LoadBlockedUsers();
        }

        private async void LoadBlockedUsers()
        {
            var users = await ApiService.Instance.GetBlockedUsersAsync();
            var defaultAvatar = System.IO.Path.GetFullPath("Assets/default_avatar.png");
            foreach (var u in users)
            {
                if (string.IsNullOrEmpty(u.AvatarPath) || !File.Exists(u.AvatarPath))
                {
                    u.AvatarPath = defaultAvatar;
                }
            }
            BlockedList.ItemsSource = users;
        }

        private async void Unblock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string targetUsername)
            {
                if (MessageBox.Show($"Voulez-vous vraiment débloquer {targetUsername} ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await ApiService.Instance.UnblockUserAsync(targetUsername);
                    LoadBlockedUsers();
                }
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                BlockUserWindow? blockWindow = null;

                if (btn.Tag is string targetUsername)
                {
                    // Fallback if Tag is string (should not happen with new binding but safe to keep)
                    blockWindow = new BlockUserWindow(_currentUsername, targetUsername);
                }
                else if (btn.Tag is BlockedUserDto info)
                {
                    // Edit mode
                    blockWindow = new BlockUserWindow(_currentUsername, info);
                }

                if (blockWindow != null)
                {
                    blockWindow.UserBlocked += LoadBlockedUsers;
                    blockWindow.ShowDialog();
                }
            }
        }
    }
}