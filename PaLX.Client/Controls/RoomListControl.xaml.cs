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
            
            // S'abonner aux changements de visibilité des salons (temps réel)
            _apiService.OnRoomVisibilityChanged += OnRoomVisibilityChanged;
            
            Console.WriteLine($"[RoomListControl] *** INITIALIZED - Subscribed to role and visibility events ***");
        }

        /// <summary>
        /// Appelé quand la visibilité d'un salon change (temps réel via SignalR)
        /// </summary>
        private void OnRoomVisibilityChanged(int roomId, bool isActive, bool isSystemHidden)
        {
            Dispatcher.Invoke(async () =>
            {
                try
                {
                    Console.WriteLine($"[RoomListControl] OnRoomVisibilityChanged received for room {roomId}, isActive={isActive}, isSystemHidden={isSystemHidden}");
                    
                    // Rafraîchir la liste des salons pour mettre à jour l'affichage
                    await RefreshRooms();
                    
                    Console.WriteLine($"[RoomListControl] Room list refreshed after visibility change");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RoomListControl] Error handling visibility change: {ex.Message}");
                }
            });
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
                        IsSystemHidden = r.IsSystemHidden,
                        SubscriptionLevel = r.SubscriptionLevel,
                        CreatedAt = r.CreatedAt,
                        UserRole = r.UserRole
                    });
                }
            }
            catch
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
            string? password = null;
            bool isInvisible = false;
            
            // Si l'utilisateur est admin système, afficher le modal de choix de mode
            if (ApiService.Instance.IsSystemAdmin)
            {
                var modeWindow = new JoinRoomModeWindow(room.Name);
                var result = modeWindow.ShowDialog();
                
                if (result != true || modeWindow.IsInvisibleMode == null)
                {
                    return; // L'utilisateur a annulé
                }
                
                isInvisible = modeWindow.IsInvisibleMode.Value;
            }
            
            if (room.IsPrivate)
            {
                // Prompt for password (simple input dialog or custom window)
                // For now, let's assume we have a simple input dialog or we can implement one.
                // password = Prompt.Show("Mot de passe requis", "Entrez le mot de passe du salon");
                // If cancel, return.
            }

            try
            {
                var success = await _apiService.JoinRoomAsync(room.Id, password, isInvisible);
                if (success)
                {
                    var roomWin = new RoomWindow(room, isInvisible);
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
            // Le Tag peut être un RoomViewModel (depuis le binding) ou un int
            int? roomId = null;
            string? roomName = null;
            
            if (sender is Button btn)
            {
                if (btn.Tag is RoomViewModel room)
                {
                    roomId = room.Id;
                    roomName = room.Name;
                }
                else if (btn.Tag is int id)
                {
                    roomId = id;
                }
            }
            
            if (roomId.HasValue)
            {
                var message = string.IsNullOrEmpty(roomName) 
                    ? "Voulez-vous vraiment supprimer ce salon ?" 
                    : $"Voulez-vous vraiment supprimer le salon '{roomName}' ?";
                    
                if (MessageBox.Show(message, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _apiService.DeleteRoomAsync(roomId.Value);
                        ToastService.Success("Salon supprimé avec succès !");
                        await RefreshRooms();
                    }
                    catch (Exception ex)
                    {
                        ToastService.Error($"Erreur lors de la suppression: {ex.Message}");
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

        /// <summary>
        /// Bouton rouge pour cacher/afficher un salon en mode admin système.
        /// Quand activé, même le RoomOwner ne voit plus son salon.
        /// </summary>
        private async void SystemHideRoom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RoomViewModel room)
            {
                try
                {
                    var confirmMessage = room.IsSystemHidden 
                        ? $"Voulez-vous réafficher le salon '{room.Name}' au propriétaire ?"
                        : $"Voulez-vous cacher le salon '{room.Name}' même à son propriétaire ?\n\nLe RoomOwner ne pourra plus voir ni gérer son propre salon.";
                    
                    var result = MessageBox.Show(confirmMessage, "Action Admin Système", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes) return;

                    var newState = await _apiService.ToggleSystemHiddenAsync(room.Id);
                    var message = newState 
                        ? $"Le salon '{room.Name}' est maintenant caché (même au propriétaire)." 
                        : $"Le salon '{room.Name}' est de nouveau visible pour tous.";
                    ToastService.Success(message, "Action Admin");
                    await RefreshRooms();
                }
                catch (Exception ex)
                {
                    ToastService.Error($"Erreur: {ex.Message}");
                }
            }
        }
    }

    public class RoomViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public int MaxUsers { get; set; }
        public bool IsPrivate { get; set; }
        public bool Is18Plus { get; set; }
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Quand TRUE, le salon est caché même au RoomOwner.
        /// Seuls les admins système peuvent le voir.
        /// </summary>
        public bool IsSystemHidden { get; set; }
        
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
        
        /// <summary>
        /// Icône pour le bouton système (rouge = caché par admin)
        /// </summary>
        public string SystemHiddenIcon => IsSystemHidden ? "🚫" : "⛔";
        public string SystemHiddenTooltip => IsSystemHidden ? "Afficher le salon (Admin)" : "Cacher le salon (Admin)";
        
        /// <summary>
        /// Opacité du salon: réduite si caché normalement OU caché par admin
        /// </summary>
        public double Opacity => (IsActive && !IsSystemHidden) ? 1.0 : 0.5;

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
        public string Name { get; set; } = string.Empty;
    }
}
