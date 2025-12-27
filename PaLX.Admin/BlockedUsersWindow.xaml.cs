using System.Windows;
using System.Windows.Controls;
using System.IO;
using PaLX.Admin.Services;
using System.Threading.Tasks;

namespace PaLX.Admin
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
            foreach (var u in users)
            {
                if (string.IsNullOrEmpty(u.AvatarPath) || !File.Exists(u.AvatarPath))
                {
                    u.AvatarPath = null;
                }
            }
            BlockedList.ItemsSource = users;
        }

        private async void Unblock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string targetUsername)
            {
                var confirm = new CustomConfirmWindow($"Voulez-vous vraiment débloquer {targetUsername} ?", "Déblocage");
                if (confirm.ShowDialog() == true)
                {
                    var result = await ApiService.Instance.UnblockUserAsync(targetUsername);
                    if (result.Success)
                    {
                        LoadBlockedUsers();
                    }
                    else
                    {
                        new CustomAlertWindow(result.Message).ShowDialog();
                    }
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
                    // Fallback if Tag is string
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