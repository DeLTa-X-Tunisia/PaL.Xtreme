namespace PaLX.API.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public int? RoleLevel { get; set; } = 7;
        public string? RoleDisplayName { get; set; } // Ex: "Maître du Serveur" au lieu de "ServerMaster"
        public string? RoleColor { get; set; } // Couleur du rôle (#FFD700)
        public string? Avatar { get; set; }
        public string? AvatarPath { get; set; } // Chemin vers la photo de profil
        public DateTime? CreatedAt { get; set; }
        
        // Profile info from UserProfiles table
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        
        /// <summary>
        /// Retourne le nom complet (LastName FirstName) ou le username si pas de profil
        /// Ex: "Admin A" au lieu de "A Admin"
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(FirstName) || !string.IsNullOrEmpty(LastName)
            ? $"{LastName ?? ""} {FirstName ?? ""}".Trim()
            : Username;
    }
}