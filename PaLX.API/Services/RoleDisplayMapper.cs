namespace PaLX.API.Services
{
    /// <summary>
    /// Classe utilitaire pour mapper les noms techniques de rôles vers des DisplayNames lisibles.
    /// Inclut les rôles de salon (RoomRoles) et les rôles système (Roles).
    /// </summary>
    public static class RoleDisplayMapper
    {
        // ═══════════════════════════════════════════════════════════════════════
        // RÔLES DE SALON (Table RoomRoles)
        // ═══════════════════════════════════════════════════════════════════════
        private static readonly Dictionary<string, RoleDisplayInfo> _roomRoleDisplayMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Noms complets (comme dans la table RoomRoles)
            { "RoomOwner", new RoleDisplayInfo("Propriétaire du Salon", "#FF0000", "crown", 1) },
            { "RoomSuperAdmin", new RoleDisplayInfo("Super Administrateur", "#FF4500", "shield-star", 2) },
            { "RoomAdmin", new RoleDisplayInfo("Administrateur", "#FFA500", "shield", 3) },
            { "PowerUser", new RoleDisplayInfo("Utilisateur Avancé", "#008000", "lightning", 4) },
            { "RoomModerator", new RoleDisplayInfo("Modérateur", "#0000FF", "gavel", 5) },
            { "RoomMember", new RoleDisplayInfo("Membre", "#808080", "user", 6) },
            
            // Alias courts (pour compatibilité avec les valeurs de RoomAdmins.Role)
            { "Owner", new RoleDisplayInfo("Propriétaire du Salon", "#FF0000", "crown", 1) },
            { "SuperAdmin", new RoleDisplayInfo("Super Administrateur", "#FF4500", "shield-star", 2) },
            { "Admin", new RoleDisplayInfo("Administrateur", "#FFA500", "shield", 3) },
            { "Moderator", new RoleDisplayInfo("Modérateur", "#0000FF", "gavel", 5) },
            { "Member", new RoleDisplayInfo("Membre", "#808080", "user", 6) }
        };

        // ═══════════════════════════════════════════════════════════════════════
        // RÔLES SYSTÈME (Table Roles - Admins du serveur)
        // ═══════════════════════════════════════════════════════════════════════
        private static readonly Dictionary<string, RoleDisplayInfo> _systemRoleDisplayMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ServerMaster", new RoleDisplayInfo("Maître du Serveur", "#FFD700", "🏆", 1) },
            { "ServerEditor", new RoleDisplayInfo("Éditeur", "#9B59B6", "✏️", 2) },
            { "ServerSuperAdmin", new RoleDisplayInfo("Super Administrateur", "#E74C3C", "👑", 3) },
            { "ServerAdmin", new RoleDisplayInfo("Administrateur", "#3498DB", "⚙️", 4) },
            { "ServerModerator", new RoleDisplayInfo("Modérateur", "#2ECC71", "🛡️", 5) },
            { "ServerHelp", new RoleDisplayInfo("Assistant", "#1ABC9C", "🤝", 6) },
            { "User", new RoleDisplayInfo("Utilisateur", "#808080", "user", 7) }
        };

        /// <summary>
        /// Récupère les informations d'un rôle de SALON
        /// </summary>
        public static RoleDisplayInfo GetRoleInfo(string roleName)
        {
            if (string.IsNullOrEmpty(roleName)) 
                return new RoleDisplayInfo("Membre", "#808080", "user", 6);
            
            return _roomRoleDisplayMap.TryGetValue(roleName, out var info) 
                ? info 
                : new RoleDisplayInfo(roleName, "#808080", "user", 99);
        }

        /// <summary>
        /// Récupère les informations d'un rôle SYSTÈME (admin serveur)
        /// </summary>
        public static RoleDisplayInfo GetSystemRoleInfo(string roleName)
        {
            if (string.IsNullOrEmpty(roleName)) 
                return new RoleDisplayInfo("Utilisateur", "#808080", "user", 7);
            
            return _systemRoleDisplayMap.TryGetValue(roleName, out var info) 
                ? info 
                : new RoleDisplayInfo(roleName, "#808080", "user", 99);
        }

        /// <summary>
        /// Vérifie si c'est un rôle système privilégié (niveau 1-6, pas User)
        /// </summary>
        public static bool IsSystemAdmin(string roleName)
        {
            if (string.IsNullOrEmpty(roleName)) return false;
            return _systemRoleDisplayMap.TryGetValue(roleName, out var info) && info.Level <= 6;
        }

        /// <summary>
        /// Convertit un nom technique de rôle en DisplayName français (rôle salon)
        /// </summary>
        public static string GetDisplayName(string roleName)
        {
            if (string.IsNullOrEmpty(roleName)) return "Membre";
            return _roomRoleDisplayMap.TryGetValue(roleName, out var info) ? info.DisplayName : roleName;
        }

        /// <summary>
        /// Vérifie si un rôle de salon existe
        /// </summary>
        public static bool IsValidRole(string roleName)
        {
            return !string.IsNullOrEmpty(roleName) && _roomRoleDisplayMap.ContainsKey(roleName);
        }
    }

    public record RoleDisplayInfo(string DisplayName, string Color, string Icon, int Level);
}
