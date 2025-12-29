using System;

namespace PaLX.API.DTOs
{
    public class CreateRoomDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int MaxUsers { get; set; } = 50;
        public bool IsPrivate { get; set; }
        public string? Password { get; set; }
        public bool Is18Plus { get; set; }
        public int SubscriptionLevel { get; set; }
    }

    public class RoomDto
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
        public int SubscriptionLevel { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RoomMemberDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarPath { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string RoleColor { get; set; } = string.Empty;
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
        public string RoleColor { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "Text";
        public DateTime Timestamp { get; set; }
        public string? AttachmentUrl { get; set; }
    }

    public class JoinRoomDto
    {
        public int RoomId { get; set; }
        public string? Password { get; set; }
    }

    public class RoomCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
