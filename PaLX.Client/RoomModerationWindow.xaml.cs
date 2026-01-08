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
    /// </summary>
    public partial class RoomModerationWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly int _roomId;
        private readonly string _roomName;
        
        // Deux collections séparées pour une gestion claire
        public ObservableCollection<FriendItem> AvailableFriends { get; } = new();
        public ObservableCollection<AdminItem> RoomAdmins { get; } = new();

        public RoomModerationWindow(int roomId, string roomName)
        {
            InitializeComponent();
            
            Console.WriteLine($"[RoomModeration] *** WINDOW CREATED with roomId={roomId}, roomName={roomName} ***");
            
            _apiService = ApiService.Instance;
            _roomId = roomId;
            _roomName = roomName;
            
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
        /// </summary>
        private async System.Threading.Tasks.Task LoadData()
        {
            try
            {
                Console.WriteLine($"[RoomModeration] ========== LOADING DATA FOR ROOM {_roomId} ==========");
                
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
                
                // 3. Séparer en deux listes
                AvailableFriends.Clear();
                RoomAdmins.Clear();
                
                foreach (var friend in friends)
                {
                    var roleInfo = roles?.FirstOrDefault(r => r.UserId == friend.Id);
                    string avatarUrl = BuildAvatarUrl(friend.AvatarPath);
                    
                    Console.WriteLine($"[RoomModeration] Friend {friend.Username} (ID={friend.Id}) - RoleInfo: {(roleInfo != null ? roleInfo.Role : "null")}");
                    
                    if (roleInfo != null && !string.IsNullOrEmpty(roleInfo.Role))
                    {
                        // Ami avec un rôle → Liste des admins
                        RoomAdmins.Add(new AdminItem
                        {
                            UserId = friend.Id,
                            Username = friend.Username,
                            DisplayName = friend.DisplayName ?? friend.Username,
                            AvatarUrl = avatarUrl,
                            Role = roleInfo.Role
                        });
                        Console.WriteLine($"[RoomModeration]   -> Added to ADMINS list");
                    }
                    else
                    {
                        // Ami sans rôle → Liste disponible
                        AvailableFriends.Add(new FriendItem
                        {
                            UserId = friend.Id,
                            Username = friend.Username,
                            DisplayName = friend.DisplayName ?? friend.Username,
                            AvatarUrl = avatarUrl
                        });
                        Console.WriteLine($"[RoomModeration]   -> Added to AVAILABLE list");
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
            if (string.IsNullOrEmpty(avatarPath))
                return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_avatar.png");
            
            if (avatarPath.Contains(":\\") || avatarPath.StartsWith("/") || avatarPath.StartsWith("\\"))
                return System.IO.File.Exists(avatarPath) ? avatarPath 
                    : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_avatar.png");
            
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
                        Role = role
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
                    // Déplacer de RoomAdmins vers AvailableFriends
                    RoomAdmins.Remove(admin);
                    AvailableFriends.Add(new FriendItem
                    {
                        UserId = admin.UserId,
                        Username = admin.Username,
                        DisplayName = admin.DisplayName,
                        AvatarUrl = admin.AvatarUrl
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
    /// Ami disponible (sans rôle)
    /// </summary>
    public class FriendItem
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
    }

    /// <summary>
    /// Administrateur du salon (avec rôle)
    /// </summary>
    public class AdminItem
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
        public string Role { get; set; } = "";
        
        public string RoleDisplayName => Role switch
        {
            "SuperAdmin" => "SuperAdmin 👑",
            "Admin" => "Admin ⭐",
            "Moderator" => "Modérateur 🔧",
            _ => Role
        };
    }
}
