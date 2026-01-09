using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre de modération du salon - Version simplifiée avec deux listes
    /// Gère les permissions selon le rôle de l'utilisateur
    /// </summary>
    public partial class RoomModerationWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly int _roomId;
        private readonly string _roomName;
        
        // Informations de rôle pour filtrer les permissions
        private readonly bool _isOwner;
        private readonly string? _currentUserRole; // SuperAdmin, Admin, Moderator ou null
        
        // Deux collections séparées pour une gestion claire
        public ObservableCollection<FriendItem> AvailableFriends { get; } = new();
        public ObservableCollection<AdminItem> RoomAdmins { get; } = new();

        public RoomModerationWindow(int roomId, string roomName, bool isOwner = true, string? userRole = null)
        {
            InitializeComponent();
            
            Console.WriteLine($"[RoomModeration] *** WINDOW CREATED with roomId={roomId}, roomName={roomName}, isOwner={isOwner}, userRole={userRole} ***");
            
            _apiService = ApiService.Instance;
            _roomId = roomId;
            _roomName = roomName;
            _isOwner = isOwner;
            _currentUserRole = userRole;
            
            TitleText.Text = $"Modération - {roomName}";
            
            AvailableFriendsList.ItemsSource = AvailableFriends;
            AdminsList.ItemsSource = RoomAdmins;
            
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadData();
        }

        /// <summary>
        /// Charge les amis et les administrateurs du salon
        /// Filtre selon le rôle de l'utilisateur
        /// </summary>
        private async System.Threading.Tasks.Task LoadData()
        {
            try
            {
                Console.WriteLine($"[RoomModeration] ========== LOADING DATA FOR ROOM {_roomId} ==========");
                Console.WriteLine($"[RoomModeration] Current user: IsOwner={_isOwner}, Role={_currentUserRole}");
                
                // 1. Récupérer tous les amis
                var friends = await _apiService.GetFriendsAsync();
                Console.WriteLine($"[RoomModeration] Friends count: {friends?.Count ?? 0}");
                
                // 2. Récupérer les rôles actuels du salon
                var roles = await _apiService.GetRoomRolesAsync(_roomId);
                Console.WriteLine($"[RoomModeration] Roles from API: {roles?.Count ?? 0}");
                
                if (roles != null)
                {
                    foreach (var r in roles)
                    {
                        Console.WriteLine($"[RoomModeration]   -> UserId={r.UserId}, Username={r.Username}, Role={r.Role}");
                    }
                }
                
                // 3. Séparer en deux listes avec filtrage selon le rôle
                AvailableFriends.Clear();
                RoomAdmins.Clear();
                
                // Déterminer quels boutons de rôle sont visibles selon le rôle actuel
                bool canAssignSuperAdmin = _isOwner; // Seul Owner peut assigner SuperAdmin
                bool canAssignAdmin = _isOwner || _currentUserRole == "SuperAdmin"; // Owner et SuperAdmin
                bool canAssignModerator = _isOwner || _currentUserRole == "SuperAdmin" || _currentUserRole == "Admin"; // Owner, SuperAdmin, Admin
                
                Console.WriteLine($"[RoomModeration] Permissions: CanAssignSuperAdmin={canAssignSuperAdmin}, CanAssignAdmin={canAssignAdmin}, CanAssignModerator={canAssignModerator}");
                
                foreach (var friend in friends)
                {
                    var roleInfo = roles?.FirstOrDefault(r => r.UserId == friend.Id);
                    string avatarUrl = BuildAvatarUrl(friend.AvatarPath);
                    
                    Console.WriteLine($"[RoomModeration] Friend {friend.Username} (ID={friend.Id}) - RoleInfo: {(roleInfo != null ? roleInfo.Role : "null")}");
                    
                    if (roleInfo != null && !string.IsNullOrEmpty(roleInfo.Role))
                    {
                        // Ami avec un rôle → Vérifier si l'utilisateur peut le voir
                        if (CanSeeRole(roleInfo.Role))
                        {
                            // Utiliser l'avatar du roleInfo si disponible, sinon celui de l'ami
                            string adminAvatarUrl = !string.IsNullOrEmpty(roleInfo.AvatarUrl) 
                                ? BuildAvatarUrl(roleInfo.AvatarUrl) 
                                : avatarUrl;
                            
                            RoomAdmins.Add(new AdminItem
                            {
                                UserId = friend.Id,
                                Username = friend.Username,
                                DisplayName = roleInfo.DisplayName ?? friend.DisplayName ?? friend.Username,
                                AvatarUrl = adminAvatarUrl,
                                Role = roleInfo.Role,
                                CanRemove = CanRemoveRole(roleInfo.Role) // Peut-on retirer ce rôle ?
                            });
                            Console.WriteLine($"[RoomModeration]   -> Added to ADMINS list (CanRemove={CanRemoveRole(roleInfo.Role)})");
                        }
                        else
                        {
                            Console.WriteLine($"[RoomModeration]   -> HIDDEN (role {roleInfo.Role} not visible for current user)");
                        }
                    }
                    else
                    {
                        // Ami sans rôle → Liste disponible (avec boutons filtrés)
                        AvailableFriends.Add(new FriendItem
                        {
                            UserId = friend.Id,
                            Username = friend.Username,
                            DisplayName = friend.DisplayName ?? friend.Username,
                            AvatarUrl = avatarUrl,
                            CanAssignSuperAdmin = canAssignSuperAdmin,
                            CanAssignAdmin = canAssignAdmin,
                            CanAssignModerator = canAssignModerator
                        });
                        Console.WriteLine($"[RoomModeration]   -> Added to AVAILABLE list");
                    }
                }
                
                // Ajouter les administrateurs qui ne sont PAS des amis
                if (roles != null)
                {
                    var friendUserIds = friends.Select(f => f.Id).ToHashSet();
                    foreach (var roleInfo in roles.Where(r => !friendUserIds.Contains(r.UserId) && !string.IsNullOrEmpty(r.Role)))
                    {
                        if (CanSeeRole(roleInfo.Role))
                        {
                            string adminAvatarUrl = BuildAvatarUrl(roleInfo.AvatarUrl);
                            RoomAdmins.Add(new AdminItem
                            {
                                UserId = roleInfo.UserId,
                                Username = roleInfo.Username,
                                DisplayName = roleInfo.DisplayName ?? roleInfo.Username,
                                AvatarUrl = adminAvatarUrl,
                                Role = roleInfo.Role,
                                CanRemove = CanRemoveRole(roleInfo.Role)
                            });
                            Console.WriteLine($"[RoomModeration] Non-friend admin {roleInfo.Username} added to ADMINS list");
                        }
                    }
                }
                
                Console.WriteLine($"[RoomModeration] FINAL: Available={AvailableFriends.Count}, Admins={RoomAdmins.Count}");
                
                UpdateUI();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomModeration] Erreur chargement: {ex.Message}");
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Détermine si l'utilisateur actuel peut voir un rôle donné
        /// Owner voit tout, SuperAdmin voit Admin+Moderator, Admin voit Moderator
        /// </summary>
        private bool CanSeeRole(string role)
        {
            if (_isOwner) return true; // Owner voit tout
            
            return _currentUserRole switch
            {
                "SuperAdmin" => role == "Admin" || role == "Moderator", // Voit Admin et Moderator
                "Admin" => role == "Moderator", // Voit seulement Moderator
                _ => false
            };
        }
        
        /// <summary>
        /// Détermine si l'utilisateur actuel peut retirer un rôle donné
        /// </summary>
        private bool CanRemoveRole(string role)
        {
            if (_isOwner) return true; // Owner peut tout retirer
            
            return _currentUserRole switch
            {
                "SuperAdmin" => role == "Admin" || role == "Moderator", // Peut retirer Admin et Moderator
                "Admin" => role == "Moderator", // Peut retirer seulement Moderator
                _ => false
            };
        }

        /// <summary>
        /// Met à jour les compteurs et états vides
        /// </summary>
        private void UpdateUI()
        {
            AvailableCountText.Text = $"({AvailableFriends.Count})";
            AdminsCountText.Text = $"({RoomAdmins.Count})";
            
            NoAvailableFriendsText.Visibility = AvailableFriends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NoAdminsPanel.Visibility = RoomAdmins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Construit l'URL de l'avatar
        /// </summary>
        private string BuildAvatarUrl(string? avatarPath)
        {
            var defaultAvatar = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_avatar.png");
            
            if (string.IsNullOrEmpty(avatarPath))
                return defaultAvatar;
            
            // Si c'est déjà une URL complète
            if (avatarPath.StartsWith("http://") || avatarPath.StartsWith("https://"))
                return avatarPath;
            
            // Si c'est un chemin local absolu (Windows: C:\... ou chemin réseau \\...)
            if (avatarPath.Contains(":\\") || avatarPath.StartsWith("\\\\"))
                return System.IO.File.Exists(avatarPath) ? avatarPath : defaultAvatar;
            
            // Sinon c'est un chemin relatif du serveur (comme /uploads/... ou uploads/...)
            return $"{ApiService.BaseUrl}/{avatarPath.TrimStart('/', '\\')}";
        }

        // ═══════════════════════════════════════════════════════════════
        // ATTRIBUTION DE RÔLES
        // ═══════════════════════════════════════════════════════════════

        private void AssignSuperAdmin_Click(object sender, RoutedEventArgs e) => AssignRole(sender, "SuperAdmin");
        private void AssignAdmin_Click(object sender, RoutedEventArgs e) => AssignRole(sender, "Admin");
        private void AssignModerator_Click(object sender, RoutedEventArgs e) => AssignRole(sender, "Moderator");

        private async void AssignRole(object sender, string role)
        {
            try
            {
                if (sender is not System.Windows.Controls.Button btn || btn.Tag is not FriendItem friend)
                    return;

                Console.WriteLine($"[RoomModeration] Assigning {role} to {friend.Username}");
                
                var result = await _apiService.AssignRoleAsync(_roomId, friend.UserId, role);
                
                if (result.Success)
                {
                    // Déplacer de AvailableFriends vers RoomAdmins
                    AvailableFriends.Remove(friend);
                    RoomAdmins.Add(new AdminItem
                    {
                        UserId = friend.UserId,
                        Username = friend.Username,
                        DisplayName = friend.DisplayName,
                        AvatarUrl = friend.AvatarUrl,
                        Role = role,
                        CanRemove = CanRemoveRole(role) // Appliquer les permissions
                    });
                    
                    UpdateUI();
                    
                    string roleName = GetRoleDisplayName(role);
                    ToastService.Success($"{friend.DisplayName} est maintenant {roleName}", "Rôle attribué");
                }
                else
                {
                    ToastService.Warning(result.Message ?? "Erreur lors de l'attribution");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomModeration] Error: {ex.Message}");
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // RETRAIT DE RÔLES
        // ═══════════════════════════════════════════════════════════════

        private async void RemoveRole_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not System.Windows.Controls.Button btn || btn.Tag is not AdminItem admin)
                    return;

                Console.WriteLine($"[RoomModeration] Removing role from {admin.Username}");
                
                var result = await _apiService.RemoveRoomRoleAsync(_roomId, admin.UserId);
                
                if (result.Success)
                {
                    // Déplacer de RoomAdmins vers AvailableFriends avec les permissions appropriées
                    RoomAdmins.Remove(admin);
                    
                    // Déterminer les permissions d'attribution
                    bool canAssignSuperAdmin = _isOwner;
                    bool canAssignAdmin = _isOwner || _currentUserRole == "SuperAdmin";
                    bool canAssignModerator = _isOwner || _currentUserRole == "SuperAdmin" || _currentUserRole == "Admin";
                    
                    AvailableFriends.Add(new FriendItem
                    {
                        UserId = admin.UserId,
                        Username = admin.Username,
                        DisplayName = admin.DisplayName,
                        AvatarUrl = admin.AvatarUrl,
                        CanAssignSuperAdmin = canAssignSuperAdmin,
                        CanAssignAdmin = canAssignAdmin,
                        CanAssignModerator = canAssignModerator
                    });
                    
                    UpdateUI();
                    
                    ToastService.Success($"Le rôle de {admin.DisplayName} a été retiré", "Rôle retiré");
                }
                else
                {
                    ToastService.Warning(result.Message ?? "Erreur lors du retrait");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomModeration] Error: {ex.Message}");
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static string GetRoleDisplayName(string role) => role switch
        {
            "SuperAdmin" => "SuperAdmin 👑",
            "Admin" => "Admin ⭐",
            "Moderator" => "Modérateur 🔧",
            _ => role
        };

        // ═══════════════════════════════════════════════════════════════
        // WINDOW CHROME
        // ═══════════════════════════════════════════════════════════════

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return;
            DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MODÈLES SIMPLES (pas de PropertyChanged complexe)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ami disponible (sans rôle) avec permissions d'attribution
    /// </summary>
    public class FriendItem
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
        
        // Permissions d'attribution selon le rôle de l'utilisateur courant
        public bool CanAssignSuperAdmin { get; set; } = false;
        public bool CanAssignAdmin { get; set; } = false;
        public bool CanAssignModerator { get; set; } = false;
        
        // Pour le binding XAML (Visibility)
        public System.Windows.Visibility SuperAdminButtonVisibility => 
            CanAssignSuperAdmin ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public System.Windows.Visibility AdminButtonVisibility => 
            CanAssignAdmin ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public System.Windows.Visibility ModeratorButtonVisibility => 
            CanAssignModerator ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    /// <summary>
    /// Administrateur du salon (avec rôle) avec permission de retrait
    /// </summary>
    public class AdminItem
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
        public string Role { get; set; } = "";
        
        // Permission de retrait selon le rôle de l'utilisateur courant
        public bool CanRemove { get; set; } = true;
        
        // Pour le binding XAML
        public System.Windows.Visibility RemoveButtonVisibility => 
            CanRemove ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        
        public string RoleDisplayName => Role switch
        {
            "SuperAdmin" => "SuperAdmin 👑",
            "Admin" => "Admin ⭐",
            "Moderator" => "Modérateur 🔧",
            _ => Role
        };
    }
}
