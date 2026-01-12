namespace PaLX.API.Models
{
    /// <summary>
    /// Représente une consultation de profil par un utilisateur
    /// </summary>
    public class ProfileView
    {
        public int Id { get; set; }
        public int ViewerId { get; set; }
        public int ViewedUserId { get; set; }
        public DateTime ViewedAt { get; set; }
        public string Context { get; set; } = "room";
    }

    /// <summary>
    /// DTO pour afficher qui a vu mon profil
    /// </summary>
    public class ProfileViewerDto
    {
        public int ViewerId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public DateTime ViewedAt { get; set; }
        public string Context { get; set; } = "room";
    }

    /// <summary>
    /// Profil public enrichi avec enregistrement de la visite
    /// </summary>
    public class PublicProfileDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public DateTime? MemberSince { get; set; }
    }
}
