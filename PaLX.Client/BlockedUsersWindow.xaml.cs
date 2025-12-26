using System.Windows;
using System.Windows.Controls;
using System.IO;

namespace PaLX.Client
{
    public partial class BlockedUsersWindow : Window
    {
        private string _currentUsername;
        private DatabaseService _dbService;

        public BlockedUsersWindow(string username)
        {
            InitializeComponent();
            _currentUsername = username;
            _dbService = new DatabaseService();
            LoadBlockedUsers();
        }

        private void LoadBlockedUsers()
        {
            var users = _dbService.GetBlockedUsers(_currentUsername);
            foreach (var u in users)
            {
                if (string.IsNullOrEmpty(u.AvatarPath) || !File.Exists(u.AvatarPath))
                {
                    u.AvatarPath = null;
                }
            }
            BlockedList.ItemsSource = users;
        }

        private void Unblock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string targetUsername)
            {
                if (MessageBox.Show($"Voulez-vous vraiment débloquer {targetUsername} ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _dbService.UnblockUser(_currentUsername, targetUsername);
                    LoadBlockedUsers();
                }
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string targetUsername)
            {
                var blockWindow = new BlockUserWindow(_currentUsername, targetUsername);
                blockWindow.UserBlocked += LoadBlockedUsers;
                blockWindow.ShowDialog();
            }
        }
    }
}