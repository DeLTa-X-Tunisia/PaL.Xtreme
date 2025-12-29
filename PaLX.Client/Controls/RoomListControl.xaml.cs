using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PaLX.Client.Services;

namespace PaLX.Client.Controls
{
    public partial class RoomListControl : UserControl
    {
        private readonly ApiService _apiService;
        public ObservableCollection<RoomViewModel> Rooms { get; set; } = new ObservableCollection<RoomViewModel>();
        public ObservableCollection<CategoryViewModel> Categories { get; set; } = new ObservableCollection<CategoryViewModel>();

        public RoomListControl()
        {
            InitializeComponent();
            _apiService = ApiService.Instance; // Should be injected or singleton
            RoomsList.ItemsSource = Rooms;
            CategoryFilter.ItemsSource = Categories;
            LoadData();
        }

        private async void LoadData()
        {
            try
            {
                // Load Categories
                var categories = await _apiService.GetRoomCategoriesAsync();
                Categories.Clear();
                Categories.Add(new CategoryViewModel { Id = 0, Name = "Toutes" });
                foreach (var cat in categories)
                {
                    Categories.Add(new CategoryViewModel { Id = cat.Id, Name = cat.Name });
                }
                CategoryFilter.SelectedIndex = 0;

                // Load Rooms
                await RefreshRooms();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur de chargement: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task RefreshRooms(int? categoryId = null)
        {
            try
            {
                var rooms = await _apiService.GetRoomsAsync(categoryId == 0 ? null : categoryId);
                Rooms.Clear();
                foreach (var r in rooms)
                {
                    Rooms.Add(new RoomViewModel
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Description = r.Description,
                        CategoryName = r.CategoryName,
                        UserCount = r.UserCount,
                        MaxUsers = r.MaxUsers,
                        IsPrivate = r.IsPrivate,
                        Is18Plus = r.Is18Plus,
                        SubscriptionLevel = r.SubscriptionLevel
                    });
                }
            }
            catch (Exception ex)
            {
                // Handle error
            }
        }

        private async void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryFilter.SelectedItem is CategoryViewModel cat)
            {
                await RefreshRooms(cat.Id);
            }
        }

        private void CreateRoom_Click(object sender, RoutedEventArgs e)
        {
            var createWin = new CreateRoomWindow();
            if (createWin.ShowDialog() == true)
            {
                LoadData(); // Refresh list
            }
        }

        private void RoomsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (RoomsList.SelectedItem is RoomViewModel room)
            {
                // Join Room Logic
                JoinRoom(room);
            }
        }

        private async void JoinRoom(RoomViewModel room)
        {
            string password = null;
            if (room.IsPrivate)
            {
                // Prompt for password (simple input dialog or custom window)
                // For now, let's assume we have a simple input dialog or we can implement one.
                // password = Prompt.Show("Mot de passe requis", "Entrez le mot de passe du salon");
                // If cancel, return.
            }

            try
            {
                var success = await _apiService.JoinRoomAsync(room.Id, password);
                if (success)
                {
                    var roomWin = new RoomWindow(room.Id, room.Name);
                    roomWin.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible de rejoindre: {ex.Message}");
            }
        }
    }

    public class RoomViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string CategoryName { get; set; }
        public int UserCount { get; set; }
        public int MaxUsers { get; set; }
        public bool IsPrivate { get; set; }
        public bool Is18Plus { get; set; }
        public int SubscriptionLevel { get; set; }

        public bool IsVIP => SubscriptionLevel >= 2; // Example

        public string LevelInitial
        {
            get
            {
                return SubscriptionLevel switch
                {
                    0 => "B", // Basic
                    1 => "D", // Deluxe
                    2 => "V", // VIP
                    3 => "R", // Royal
                    4 => "L", // Legend
                    _ => "?"
                };
            }
        }

        public Brush LevelColor
        {
            get
            {
                return SubscriptionLevel switch
                {
                    0 => Brushes.Gray,
                    1 => Brushes.CornflowerBlue,
                    2 => Brushes.Gold,
                    3 => Brushes.Purple,
                    4 => Brushes.Black,
                    _ => Brushes.Gray
                };
            }
        }
    }

    public class CategoryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
