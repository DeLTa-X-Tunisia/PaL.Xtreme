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
        
        // Garde une référence aux fenêtres d'édition ouvertes
        private Dictionary<int, CreateRoomWindow> _openEditWindows = new Dictionary<int, CreateRoomWindow>();

        public RoomListControl()
        {
            InitializeComponent();
            _apiService = ApiService.Instance; // Should be injected or singleton
            RoomsList.ItemsSource = Rooms;
            CategoryFilter.ItemsSource = Categories;
            LoadData();
            
            // S'abonner aux événements de rôle
            _apiService.OnRoleRemoved += OnRoleRemoved;
            _apiService.OnRoleAssigned += OnRoleAssigned;
            
            Console.WriteLine($"[RoomListControl] *** INITIALIZED - Subscribed to OnRoleRemoved and OnRoleAssigned events ***");
        }

        private void OnRoleAssigned(int roomId, string roomName, string role)
        {
            Dispatcher.Invoke(async () =>
            {
                try
                {
                    Console.WriteLine($"[RoomListControl] OnRoleAssigned received for room {roomId} ({roomName}) with role {role}");
                    
                    // Rafraîchir la liste des salons pour mettre à jour CanEdit
                    await RefreshRooms();
                    
                    // Afficher une notification toast
                    string roleName = role switch
                    {
                        "SuperAdmin" => "SuperAdmin 👑",
                        "Admin" => "Admin ⭐",
                        "Moderator" => "Modérateur 🔧",
                        _ => role
                    };
                    ToastService.Success($"Vous êtes maintenant {roleName} du salon '{roomName}'", "Rôle attribué");
                    
                    Console.WriteLine($"[RoomListControl] Room list refreshed after role assignment");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RoomListControl] Error handling role assignment: {ex.Message}");
                }
            });
        }

        private void OnRoleRemoved(int roomId, string roomName)
        {
            Dispatcher.Invoke(async () =>
            {
                try
                {
                    Console.WriteLine($"[RoomListControl] OnRoleRemoved received for room {roomId} ({roomName})");
                    
                    // Fermer la fenêtre d'édition si elle est ouverte pour ce salon
                    if (_openEditWindows.TryGetValue(roomId, out var editWindow))
                    {
                        try
                        {
                            editWindow.Close();
                            _openEditWindows.Remove(roomId);
                            Console.WriteLine($"[RoomListControl] Closed edit window for room {roomId}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RoomListControl] Error closing edit window: {ex.Message}");
                        }
                    }
                    
                    // Rafraîchir la liste des salons pour mettre à jour CanEdit
                    await RefreshRooms();
                    
                    // Afficher une notification toast
                    ToastService.Info($"Votre rôle dans le salon '{roomName}' a été retiré.", "Rôle retiré");
                    
                    Console.WriteLine($"[RoomListControl] Room list refreshed after role removal");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RoomListControl] Error handling role removal: {ex.Message}");
                }
            });
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
                        CategoryId = r.CategoryId,
                        CategoryName = r.CategoryName,
                        OwnerId = r.OwnerId,
                        OwnerName = r.OwnerName,
                        UserCount = r.UserCount,
                        MaxUsers = r.MaxUsers,
                        IsPrivate = r.IsPrivate,
                        Is18Plus = r.Is18Plus,
                        IsActive = r.IsActive,
                        SubscriptionLevel = r.SubscriptionLevel,
                        CreatedAt = r.CreatedAt,
                        UserRole = r.UserRole
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
            try
            {
                // Ouvrir la fenêtre de création de salon (non-modale)
                var createWin = new CreateRoomWindow();
                createWin.Closed += (s, args) => LoadData(); // Refresh when closed
                createWin.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur CreateRoomWindow: {ex.Message}\n\nStack: {ex.StackTrace}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    var roomWin = new RoomWindow(room);
                    roomWin.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible de rejoindre: {ex.Message}");
            }
        }

        private async void DeleteRoom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int roomId)
            {
                if (MessageBox.Show("Voulez-vous vraiment supprimer ce salon ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _apiService.DeleteRoomAsync(roomId);
                        await RefreshRooms();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur: {ex.Message}");
                    }
                }
            }
        }

        private void EditRoom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RoomViewModel room)
            {
                // Fermer l'ancienne fenêtre si elle existe
                if (_openEditWindows.TryGetValue(room.Id, out var existingWindow))
                {
                    try
                    {
                        existingWindow.Close();
                    }
                    catch { }
                    _openEditWindows.Remove(room.Id);
                }
                
                var editWin = new CreateRoomWindow(room);
                editWin.Closed += (s, args) => 
                {
                    _openEditWindows.Remove(room.Id);
                    LoadData(); // Refresh when closed
                };
                _openEditWindows[room.Id] = editWin;
                editWin.Show();
            }
        }

        private async void HideRoom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RoomViewModel room)
            {
                try
                {
                    var newState = await _apiService.ToggleRoomVisibilityAsync(room.Id);
                    MessageBox.Show(newState ? "Salon visible." : "Salon caché.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshRooms();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur: {ex.Message}");
                }
            }
        }
    }

    public class RoomViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int OwnerId { get; set; }
        public string OwnerName { get; set; }
        public int UserCount { get; set; }
        public int MaxUsers { get; set; }
        public bool IsPrivate { get; set; }
        public bool Is18Plus { get; set; }
        public bool IsActive { get; set; }
        public int SubscriptionLevel { get; set; }
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Rôle de l'utilisateur connecté dans ce salon (SuperAdmin, Admin, Moderator ou null)
        /// </summary>
        public string? UserRole { get; set; }

        public bool IsVIP => SubscriptionLevel >= 2; // Example
        public bool IsOwner => OwnerId == ApiService.Instance.CurrentUserId;
        
        /// <summary>
        /// L'utilisateur est un admin système (ServerMaster à ServerModerator)
        /// Ces rôles ont un accès total à tous les salons.
        /// </summary>
        public bool IsSystemAdmin => ApiService.Instance.IsSystemAdmin;
        
        /// <summary>
        /// L'utilisateur a un accès de type Owner (propriétaire OU admin système)
        /// Permet de modifier, supprimer, cacher/afficher le salon
        /// </summary>
        public bool HasOwnerAccess => IsOwner || IsSystemAdmin;
        
        /// <summary>
        /// L'utilisateur peut modifier le salon s'il est Owner, Admin système, OU Admin/Moderator du salon
        /// </summary>
        public bool CanEdit => HasOwnerAccess || !string.IsNullOrEmpty(UserRole);
        
        public string VisibilityIcon => IsActive ? "👁️" : "🙈";
        public string VisibilityTooltip => IsActive ? "Cacher le salon" : "Afficher le salon";
        public double Opacity => IsActive ? 1.0 : 0.5;

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
