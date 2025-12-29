using System.Windows;
using System.Linq;
using System.Collections.Generic;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class InviteFriendWindow : Window
    {
        public string? SelectedFriend { get; private set; }
        private List<string> _excludedUsernames;

        public InviteFriendWindow(List<string> excludedUsernames)
        {
            InitializeComponent();
            _excludedUsernames = excludedUsernames ?? new List<string>();
            LoadFriends();
        }

        private async void LoadFriends()
        {
            var friends = await ApiService.Instance.GetFriendsAsync();
            
            // Filter:
            // 1. Not in excluded list (participants)
            // 2. Not Blocked
            // 3. Status is Online (0) or Away (2)
            // Note: 0=Online, 1=Busy, 2=Away, 3=InCall, 4=DND
            
            var availableFriends = friends.Where(f => 
                !_excludedUsernames.Contains(f.Username) && 
                !f.IsBlocked &&
                (f.StatusValue == 0 || f.StatusValue == 2)
            ).ToList();

            if (availableFriends.Any())
            {
                FriendsList.ItemsSource = availableFriends;
                FriendsList.Visibility = Visibility.Visible;
                NoFriendsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                FriendsList.Visibility = Visibility.Collapsed;
                NoFriendsText.Visibility = Visibility.Visible;
            }
        }

        private void Invite_Click(object sender, RoutedEventArgs e)
        {
            if (FriendsList.SelectedItem is FriendDto friend)
            {
                SelectedFriend = friend.Username;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Veuillez sélectionner un ami.");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}