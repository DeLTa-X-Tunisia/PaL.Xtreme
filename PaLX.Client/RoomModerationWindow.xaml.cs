using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class RoomModerationWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly int _roomId;
        private readonly string _roomName;
        private ObservableCollection<FriendRoleViewModel> _friends;
        private List<FriendRoleViewModel> _allFriends;

        public RoomModerationWindow(int roomId, string roomName)
        {
            InitializeComponent();
            _apiService = ApiService.Instance;
            _roomId = roomId;
            _roomName = roomName;
            _friends = new ObservableCollection<FriendRoleViewModel>();
            _allFriends = new List<FriendRoleViewModel>();
            
            RoomNameLabel.Text = roomName;
            FriendsList.ItemsSource = _friends;
            
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Charger la liste des amis avec leurs rôles actuels dans ce salon
                await LoadFriendsWithRoles();
            }
            catch (Exception ex)
            {
                ToastService.Error($"Erreur chargement: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadFriendsWithRoles()
        {
            try
            {
                // Récupérer les amis
                var friends = await _apiService.GetFriendsAsync();
                
                // Récupérer les rôles actuels du salon (seulement si roomId valide)
                List<RoomRoleDto>? roomRoles = null;
                if (_roomId > 0)
                {
                    try
                    {
                        roomRoles = await _apiService.GetRoomRolesAsync(_roomId);
                    }
                    catch
                    {
                        // Ignorer les erreurs de récupération de rôles
                        roomRoles = null;
                    }
                }
                
                _allFriends.Clear();
                _friends.Clear();

                // Avec le nouveau système simplifié, pas de demandes en attente
                // Les rôles sont attribués directement

                foreach (var friend in friends)
                {
                    var roleInfo = roomRoles?.FirstOrDefault(r => r.UserId == friend.Id);
                    
                    // Construire le chemin de l'avatar
                    string avatarPath;
                    if (!string.IsNullOrEmpty(friend.AvatarPath))
                    {
                        // Vérifier si c'est un chemin local absolu ou une URL
                        if (friend.AvatarPath.Contains(":\\") || friend.AvatarPath.StartsWith("/") || friend.AvatarPath.StartsWith("\\"))
                        {
                            // Chemin local absolu - utiliser directement s'il existe
                            avatarPath = System.IO.File.Exists(friend.AvatarPath) 
                                ? friend.AvatarPath 
                                : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_avatar.png");
                        }
                        else
                        {
                            // Chemin relatif - construire l'URL complète
                            var cleanPath = friend.AvatarPath.TrimStart('/', '\\');
                            avatarPath = $"{ApiService.BaseUrl}/{cleanPath}";
                        }
                    }
                    else
                    {
                        // Utiliser l'avatar par défaut local
                        avatarPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_avatar.png");
                    }
                    
                    var viewModel = new FriendRoleViewModel
                    {
                        UserId = friend.Id,
                        Username = friend.Username,
                        DisplayName = friend.DisplayName ?? friend.Username,
                        AvatarUrl = avatarPath,
                        CurrentRole = roleInfo?.Role,
                        PendingRole = null,
                        PendingRequestId = null,
                        IsSelected = false
                    };
                    viewModel.PropertyChanged += Friend_PropertyChanged;
                    _allFriends.Add(viewModel);
                    _friends.Add(viewModel);
                }

                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }

        private void Friend_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FriendRoleViewModel.IsSelected))
            {
                UpdateSelectionCount();
            }
        }

        private void UpdateSelectionCount()
        {
            int count = _friends.Count(f => f.IsSelected);
            SelectionCount.Text = $"{count} ami(s) sélectionné(s)";
            BulkAssignButton.IsEnabled = count > 0;
            BulkAssignButton.Opacity = count > 0 ? 1.0 : 0.6;
        }

        private void UpdateEmptyState()
        {
            EmptyState.Visibility = _friends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string search = SearchBox.Text.Trim().ToLower();
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(search) ? Visibility.Visible : Visibility.Collapsed;

            _friends.Clear();
            var filtered = string.IsNullOrEmpty(search)
                ? _allFriends
                : _allFriends.Where(f => 
                    f.DisplayName.ToLower().Contains(search) || 
                    f.Username.ToLower().Contains(search));

            foreach (var friend in filtered)
            {
                _friends.Add(friend);
            }

            UpdateEmptyState();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = _friends.All(f => f.IsSelected);
            foreach (var friend in _friends)
            {
                friend.IsSelected = !allSelected;
            }
        }

        private async void AssignSuperAdmin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is FriendRoleViewModel friend)
                {
                    await AssignRole(friend, "SuperAdmin");
                }
                else
                {
                    Console.WriteLine($"[RoomModeration] AssignSuperAdmin_Click: Tag is not FriendRoleViewModel, type={((sender as Button)?.Tag?.GetType().Name ?? "null")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomModeration] AssignSuperAdmin_Click ERROR: {ex.Message}");
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }

        private async void AssignAdmin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is FriendRoleViewModel friend)
                {
                    await AssignRole(friend, "Admin");
                }
                else
                {
                    Console.WriteLine($"[RoomModeration] AssignAdmin_Click: Tag is not FriendRoleViewModel");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomModeration] AssignAdmin_Click ERROR: {ex.Message}");
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }

        private async void AssignModerator_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is FriendRoleViewModel friend)
                {
                    await AssignRole(friend, "Moderator");
                }
                else
                {
                    Console.WriteLine($"[RoomModeration] AssignModerator_Click: Tag is not FriendRoleViewModel");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomModeration] AssignModerator_Click ERROR: {ex.Message}");
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }

        private async void RemoveRole_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is FriendRoleViewModel friend)
                {
                    await RemoveRole(friend);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomModeration] RemoveRole_Click ERROR: {ex.Message}");
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task AssignRole(FriendRoleViewModel friend, string role)
        {
            // Vérifier que le salon existe
            if (_roomId <= 0)
            {
                ToastService.Warning("Vous devez d'abord créer le salon avant d'attribuer des rôles.", "Salon requis");
                return;
            }

            // Vérifier la hiérarchie : si un rôle supérieur est déjà attribué, ne pas permettre un rôle inférieur
            if (!string.IsNullOrEmpty(friend.CurrentRole))
            {
                int currentLevel = GetRoleLevel(friend.CurrentRole);
                int newLevel = GetRoleLevel(role);
                if (newLevel <= currentLevel)
                {
                    ToastService.Warning($"{friend.DisplayName} a déjà un rôle égal ou supérieur.", "Rôle existant");
                    return;
                }
            }
            
            try
            {
                // Attribution directe du rôle (nouvelle API simplifiée)
                var result = await _apiService.AssignRoleAsync(_roomId, friend.UserId, role);
                
                if (result.Success)
                {
                    string roleName = role switch
                    {
                        "SuperAdmin" => "SuperAdmin 👑",
                        "Admin" => "Admin ⭐",
                        "Moderator" => "Modérateur 🔧",
                        _ => role
                    };
                    ToastService.Success($"{friend.DisplayName} est maintenant {roleName}", "Rôle attribué");
                    // Mettre à jour le rôle dans l'UI
                    friend.CurrentRole = role;
                }
                else
                {
                    // Parser le message d'erreur JSON si présent
                    string errorMsg = result.Message ?? "Erreur lors de l'attribution";
                    if (errorMsg.Contains("Room not found"))
                        errorMsg = "Le salon n'existe pas encore. Créez-le d'abord.";
                    else if (errorMsg.Contains("not a friend"))
                        errorMsg = "Cet utilisateur n'est pas dans votre liste d'amis.";
                    else if (errorMsg.Contains("Not owner"))
                        errorMsg = "Seul le propriétaire peut attribuer des rôles.";
                    ToastService.Warning(errorMsg, "Attribution impossible");
                }
            }
            catch (Exception ex)
            {
                ToastService.Error($"Erreur de connexion: {ex.Message}", "Erreur");
            }
        }
        private async System.Threading.Tasks.Task RemoveRole(FriendRoleViewModel friend)
        {
            try
            {
                var result = await _apiService.RemoveRoomRoleAsync(_roomId, friend.UserId);
                
                if (result.Success)
                {
                    friend.CurrentRole = null;
                    ToastService.Success($"Le rôle de {friend.DisplayName} a été retiré avec succès", "Rôle retiré");
                }
                else
                {
                    ToastService.Warning(result.Message ?? "Erreur lors du retrait");
                }
            }
            catch (Exception ex)
            {
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }

        private void CancelRequest_Click(object sender, RoutedEventArgs e)
        {
            // Avec le nouveau système simplifié, il n'y a plus de demandes en attente à annuler
            // Les rôles sont attribués directement
            ToastService.Info("Les rôles sont maintenant attribués directement.", "Information");
        }

        private async void BulkAssign_Click(object sender, RoutedEventArgs e)
        {
            var selected = _friends.Where(f => f.IsSelected).ToList();
            if (selected.Count == 0) return;

            // Afficher un menu contextuel pour choisir le rôle
            var contextMenu = new ContextMenu();
            
            var superAdminItem = new MenuItem { Header = "👑 SuperAdmin", Tag = "SuperAdmin" };
            superAdminItem.Click += async (s, args) => await BulkAssignRole(selected, "SuperAdmin");
            
            var adminItem = new MenuItem { Header = "⭐ Admin", Tag = "Admin" };
            adminItem.Click += async (s, args) => await BulkAssignRole(selected, "Admin");
            
            var modItem = new MenuItem { Header = "🔧 Modérateur", Tag = "Moderator" };
            modItem.Click += async (s, args) => await BulkAssignRole(selected, "Moderator");
            
            contextMenu.Items.Add(superAdminItem);
            contextMenu.Items.Add(adminItem);
            contextMenu.Items.Add(modItem);
            
            contextMenu.IsOpen = true;
        }

        private async System.Threading.Tasks.Task BulkAssignRole(List<FriendRoleViewModel> friends, string role)
        {
            int successCount = 0;
            foreach (var friend in friends)
            {
                try
                {
                    var result = await _apiService.AssignRoleAsync(_roomId, friend.UserId, role);
                    if (result.Success)
                    {
                        friend.CurrentRole = role;
                        friend.IsSelected = false;
                        successCount++;
                    }
                }
                catch { }
            }
            
            string roleName = role switch
            {
                "SuperAdmin" => "SuperAdmin 👑",
                "Admin" => "Admin ⭐",
                "Moderator" => "Modérateur 🔧",
                _ => role
            };
            ToastService.Success($"{successCount} rôle(s) {roleName} attribué(s) avec succès !", "Rôles attribués");
            UpdateSelectionCount();
        }

        /// <summary>
        /// Retourne le niveau de hiérarchie du rôle (plus haut = plus de pouvoir)
        /// SuperAdmin > Admin > Moderator
        /// </summary>
        private static int GetRoleLevel(string? role) => role switch
        {
            "SuperAdmin" => 3,
            "Admin" => 2,
            "Moderator" => 1,
            _ => 0
        };
    }

    /// <summary>
    /// ViewModel pour un ami avec son rôle dans le salon
    /// </summary>
    public class FriendRoleViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string? _currentRole;
        private string? _pendingRole;
        private int? _pendingRequestId;

        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string AvatarUrl { get; set; } = "";

        public int? PendingRequestId
        {
            get => _pendingRequestId;
            set { _pendingRequestId = value; OnPropertyChanged(nameof(PendingRequestId)); OnPropertyChanged(nameof(CancelButtonVisibility)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string? CurrentRole
        {
            get => _currentRole;
            set
            {
                _currentRole = value;
                OnPropertyChanged(nameof(CurrentRole));
                OnPropertyChanged(nameof(RoleDisplayName));
                OnPropertyChanged(nameof(RoleBadgeVisibility));
                OnPropertyChanged(nameof(RemoveButtonVisibility));
                OnPropertyChanged(nameof(SuperAdminOpacity));
                OnPropertyChanged(nameof(AdminOpacity));
                OnPropertyChanged(nameof(ModeratorOpacity));
            }
        }

        public string? PendingRole
        {
            get => _pendingRole;
            set 
            { 
                _pendingRole = value; 
                OnPropertyChanged(nameof(PendingRole));
                OnPropertyChanged(nameof(PendingRoleDisplayName));
                OnPropertyChanged(nameof(PendingBadgeVisibility));
                OnPropertyChanged(nameof(SuperAdminButtonState));
                OnPropertyChanged(nameof(AdminButtonState));
                OnPropertyChanged(nameof(ModeratorButtonState));
                OnPropertyChanged(nameof(SuperAdminOpacity));
                OnPropertyChanged(nameof(AdminOpacity));
                OnPropertyChanged(nameof(ModeratorOpacity));
            }
        }

        public string RoleDisplayName => CurrentRole switch
        {
            "SuperAdmin" => "SuperAdmin",
            "Admin" => "Admin",
            "Moderator" => "Modérateur",
            _ => ""
        };

        public string PendingRoleDisplayName => PendingRole switch
        {
            "SuperAdmin" => "⏳ SuperAdmin",
            "Admin" => "⏳ Admin",
            "Moderator" => "⏳ Modérateur",
            _ => ""
        };

        public Visibility RoleBadgeVisibility => 
            !string.IsNullOrEmpty(CurrentRole) ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PendingBadgeVisibility => 
            !string.IsNullOrEmpty(PendingRole) && string.IsNullOrEmpty(CurrentRole) ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RemoveButtonVisibility => 
            !string.IsNullOrEmpty(CurrentRole) ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CancelButtonVisibility => 
            PendingRequestId.HasValue && !string.IsNullOrEmpty(PendingRole) ? Visibility.Visible : Visibility.Collapsed;

        // État des boutons : "normal", "pending", "active", "disabled"
        public string SuperAdminButtonState => GetButtonState("SuperAdmin");
        public string AdminButtonState => GetButtonState("Admin");
        public string ModeratorButtonState => GetButtonState("Moderator");

        private string GetButtonState(string role)
        {
            // Si ce rôle est déjà attribué
            if (CurrentRole == role) return "active";
            // Si une demande est en attente pour ce rôle
            if (PendingRole == role) return "pending";
            // Si l'utilisateur a un rôle supérieur, désactiver les rôles inférieurs
            int currentLevel = GetRoleLevelStatic(CurrentRole);
            int roleLevel = GetRoleLevelStatic(role);
            if (currentLevel >= roleLevel && currentLevel > 0) return "disabled";
            // Si une demande en attente existe, désactiver tous les autres boutons
            if (!string.IsNullOrEmpty(PendingRole) && PendingRole != role) return "disabled";
            return "normal";
        }

        private static int GetRoleLevelStatic(string? role) => role switch
        {
            "SuperAdmin" => 3,
            "Admin" => 2,
            "Moderator" => 1,
            _ => 0
        };

        public double SuperAdminOpacity => SuperAdminButtonState == "disabled" ? 0.3 : 1.0;
        public double AdminOpacity => AdminButtonState == "disabled" ? 0.3 : 1.0;
        public double ModeratorOpacity => ModeratorButtonState == "disabled" ? 0.3 : 1.0;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
