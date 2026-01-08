namespace PaLX.API.Services
{
    /// <summary>
    /// Classe utilitaire pour mapper les noms techniques de rôles vers des DisplayNames lisibles.
    /// Référence: Table RoomRoles
    /// </summary>
    public static class RoleDisplayMapper
    {
        private static readonly Dictionary<string, RoleDisplayInfo> _roleDisplayMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "RoomOwner", new RoleDisplayInfo("Propriétaire du Salon", "#FF0000", "crown", 1) },
            { "RoomSuperAdmin", new RoleDisplayInfo("Super Administrateur", "#FF4500", "shield-star", 2) },
            { "RoomAdmin", new RoleDisplayInfo("Administrateur", "#FFA500", "shield", 3) },
            { "PowerUser", new RoleDisplayInfo("Utilisateur Avancé", "#008000", "lightning", 4) },
            { "RoomModerator", new RoleDisplayInfo("Modérateur", "#0000FF", "gavel", 5) },
            { "RoomMember", new RoleDisplayInfo("Membre", "#808080", "user", 6) }
        };

        /// <summary>
        /// Convertit un nom technique de rôle en DisplayName français
        /// </summary>
        public static string GetDisplayName(string roleName)
        {
            if (string.IsNullOrEmpty(roleName)) return "Membre";
            return _roleDisplayMap.TryGetValue(roleName, out var info) ? info.DisplayName : roleName;
        }

        /// <summary>
        /// Récupère toutes les informations d'affichage d'un rôle
        /// </summary>
        public static RoleDisplayInfo GetRoleInfo(string roleName)
        {
            if (string.IsNullOrEmpty(roleName)) 
                return new RoleDisplayInfo("Membre", "#808080", "user", 6);
            
            return _roleDisplayMap.TryGetValue(roleName, out var info) 
                ? info 
                : new RoleDisplayInfo(roleName, "#808080", "user", 99);
        }

        /// <summary>
        /// Vérifie si un rôle existe
        /// </summary>
        public static bool IsValidRole(string roleName)
        {
            return !string.IsNullOrEmpty(roleName) && _roleDisplayMap.ContainsKey(roleName);
        }
    }

    public record RoleDisplayInfo(string DisplayName, string Color, string Icon, int Level);
}
