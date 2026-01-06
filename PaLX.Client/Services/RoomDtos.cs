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
    }

    public class RoomMessageDto
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
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
}
