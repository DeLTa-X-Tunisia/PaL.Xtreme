using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class InviteToRoomWindow : Window
    {
        private readonly int _roomId;
        private readonly string _roomName;
        private readonly string _roomCategory;
        private readonly int _participantCount;
        private readonly ApiService _apiService;
        private readonly HashSet<string> _usernamesInRoom;
        
        public ObservableCollection<InvitableFriendViewModel> Friends { get; set; } = new ObservableCollection<InvitableFriendViewModel>();
        public List<string> InvitedUsernames { get; private set; } = new List<string>();
        
        public InviteToRoomWindow(int roomId, string roomName, string roomCategory, int participantCount, IEnumerable<string> usernamesInRoom)
        {
            InitializeComponent();
            _roomId = roomId;
            _roomName = roomName;
            _roomCategory = roomCategory;
            _participantCount = participantCount;
            _apiService = ApiService.Instance;
            _usernamesInRoom = new HashSet<string>(usernamesInRoom, StringComparer.OrdinalIgnoreCase);
            
            // Setup UI
            RoomNameText.Text = $"Dans le salon « {roomName} »";
            RoomTitleText.Text = roomName;
            RoomCategoryText.Text = roomCategory;
            RoomParticipantsText.Text = $"• {participantCount} participant{(participantCount > 1 ? "s" : "")}";
            
            FriendsListBox.ItemsSource = Friends;
            
            Loaded += InviteToRoomWindow_Loaded;
        }
        
        private async void InviteToRoomWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var friends = await _apiService.GetFriendsAsync();
                
                // Filter: not blocked, Online (0) or Away (2)
                var availableFriends = friends
                    .Where(f => !f.IsBlocked && (f.StatusValue == 0 || f.StatusValue == 2))
                    .OrderByDescending(f => f.StatusValue == 0) // Online first
                    .ThenBy(f => f.DisplayName)
                    .ToList();
                
                Friends.Clear();
                var baseUrl = _apiService.GetBaseUrl().TrimEnd('/');
                
                foreach (var friend in availableFriends)
                {
                    // Build absolute avatar URL
                    string? avatarUrl = null;
                    if (!string.IsNullOrEmpty(friend.AvatarPath))
                    {
                        avatarUrl = friend.AvatarPath.StartsWith("http") 
                            ? friend.AvatarPath 
                            : (friend.AvatarPath.StartsWith("/") 
                                ? $"{baseUrl}{friend.AvatarPath}" 
                                : $"{baseUrl}/{friend.AvatarPath}");
                    }
                    
                    // Check if friend is already in the room
                    bool isInRoom = _usernamesInRoom.Contains(friend.Username);
                    
                    Friends.Add(new InvitableFriendViewModel
                    {
                        Username = friend.Username,
                        DisplayName = string.IsNullOrEmpty(friend.DisplayName) ? friend.Username : friend.DisplayName,
                        AvatarPath = avatarUrl,
                        StatusValue = friend.StatusValue,
                        StatusText = isInRoom ? "✅ Déjà dans le salon" : (friend.StatusValue == 0 ? "En ligne" : "Absent"),
                        StatusColor = new SolidColorBrush(isInRoom ? 
                            Color.FromRgb(100, 100, 100) :   // Gray for in room
                            (friend.StatusValue == 0 ? 
                                Color.FromRgb(102, 187, 106) :  // Green for Online
                                Color.FromRgb(255, 193, 7))),   // Amber for Away
                        IsSelected = false,
                        IsInRoom = isInRoom
                    });
                }
                
                // Show empty state if no friends available
                if (Friends.Count == 0)
                {
                    EmptyState.Visibility = Visibility.Visible;
                    FriendsListBox.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptyState.Visibility = Visibility.Collapsed;
                    FriendsListBox.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[InviteToRoomWindow] Error loading friends: {ex.Message}");
                EmptyState.Visibility = Visibility.Visible;
                FriendsListBox.Visibility = Visibility.Collapsed;
            }
        }
        
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void FriendsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedCount();
        }
        
        private void UpdateSelectedCount()
        {
            var selectedCount = Friends.Count(f => f.IsSelected);
            var invitableCount = Friends.Count(f => f.CanBeInvited);
            SelectedCountText.Text = $"{selectedCount} sélectionné{(selectedCount > 1 ? "s" : "")}";
            SendButton.IsEnabled = selectedCount > 0;
            
            // Update select all checkbox state (only count invitable friends)
            if (selectedCount == 0)
            {
                SelectAllCheckBox.IsChecked = false;
            }
            else if (invitableCount > 0 && selectedCount == invitableCount)
            {
                SelectAllCheckBox.IsChecked = true;
            }
        }
        
        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var friend in Friends.Where(f => f.CanBeInvited))
            {
                friend.IsSelected = true;
            }
            UpdateSelectedCount();
            FriendsListBox.Items.Refresh();
        }
        
        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var friend in Friends.Where(f => f.CanBeInvited))
            {
                friend.IsSelected = false;
            }
            UpdateSelectedCount();
            FriendsListBox.Items.Refresh();
        }
        
        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            var selectedFriends = Friends.Where(f => f.IsSelected).ToList();
            if (selectedFriends.Count == 0)
            {
                return;
            }
            
            InvitedUsernames = selectedFriends.Select(f => f.Username).ToList();
            
            try
            {
                // Send invitations via SignalR
                foreach (var username in InvitedUsernames)
                {
                    Console.WriteLine($"[InviteToRoomWindow] Sending invitation to {username} for room {_roomId} ({_roomName})");
                    await _apiService.SendRoomInvitationAsync(username, _roomId, _roomName, _roomCategory);
                    Console.WriteLine($"[InviteToRoomWindow] Invitation sent to {username}");
                }
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[InviteToRoomWindow] Error sending invitations: {ex.Message}");
                MessageBox.Show("Erreur lors de l'envoi des invitations.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    public class InvitableFriendViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isInRoom;
        
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? AvatarPath { get; set; }
        public int StatusValue { get; set; }
        public string StatusText { get; set; } = "";
        public SolidColorBrush StatusColor { get; set; } = new SolidColorBrush(Colors.Gray);
        
        /// <summary>
        /// True if the friend is already in the room (cannot be invited)
        /// </summary>
        public bool IsInRoom
        {
            get => _isInRoom;
            set
            {
                if (_isInRoom != value)
                {
                    _isInRoom = value;
                    OnPropertyChanged(nameof(IsInRoom));
                    OnPropertyChanged(nameof(CanBeInvited));
                }
            }
        }
        
        /// <summary>
        /// True if the friend can be invited (not already in room)
        /// </summary>
        public bool CanBeInvited => !IsInRoom;
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                // Can only select if not in room
                if (!IsInRoom && _isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
