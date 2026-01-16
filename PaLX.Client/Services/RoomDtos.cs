namespace PaLX.Client.Services
{
    public class RoomMemberDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarPath { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string RoleColor { get; set; } = "#000000";
        public string RoleIcon { get; set; } = string.Empty;
        public bool IsMuted { get; set; }
        public bool HasHandRaised { get; set; }
        public bool IsCamOn { get; set; }
        public bool IsMicOn { get; set; }
        public string Gender { get; set; } = "Unknown";
        public bool IsInvisible { get; set; } = false;
    }

    public class RoomMessageDto
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarPath { get; set; } = string.Empty;
        public string RoleName { get; set; } = "Membre";
        public string RoleColor { get; set; } = "#000000";
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "Text";
        public DateTime Timestamp { get; set; }
        public string? AttachmentUrl { get; set; }
    }

    public class SendMessageDto
    {
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = "Text";
        public string? AttachmentUrl { get; set; }
    }

    public class UpdateStatusDto
    {
        public bool? IsCamOn { get; set; }
        public bool? IsMicOn { get; set; }
        public bool? HasHandRaised { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // KICK & BAN DTOs - v1.8.4
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Résultat d'une action de kick ou ban
    /// </summary>
    public class KickBanResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public int TargetUserId { get; set; }
        public string TargetUsername { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Représente un ban d'utilisateur dans un salon
    /// </summary>
    public class RoomBan
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public int BannedById { get; set; }
        public string BannedByUsername { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string BanType { get; set; } = "Permanent";
        public int? DurationMinutes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public string? TimeRemaining { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // GLOBAL ANNOUNCEMENT DTO - Admin Broadcast
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Représente une annonce globale envoyée par un administrateur
    /// </summary>
    public class GlobalAnnouncementDto
    {
        /// <summary>
        /// Type d'annonce: "info", "warning", "alert", "success"
        /// </summary>
        public string Type { get; set; } = "info";
        
        /// <summary>
        /// Titre de l'annonce
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Contenu du message
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Nom d'utilisateur de l'admin qui a envoyé l'annonce
        /// </summary>
        public string SentBy { get; set; } = string.Empty;
        
        /// <summary>
        /// Date/heure d'envoi
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
