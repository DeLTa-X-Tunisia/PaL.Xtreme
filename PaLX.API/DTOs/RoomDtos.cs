using System;

namespace PaLX.API.DTOs
{
    public class CreateRoomDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int MaxUsers { get; set; } = 50;
        public int MaxMics { get; set; } = 1;
        public int MaxCams { get; set; } = 2;
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
        
        /// <summary>
        /// Rôle de l'utilisateur connecté dans ce salon (null si aucun rôle)
        /// Valeurs possibles: "SuperAdmin", "Admin", "Moderator", null
        /// </summary>
        public string? UserRole { get; set; }
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
        public string AvatarPath { get; set; } = string.Empty;
        public string RoleName { get; set; } = "Membre";
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
        public string Color { get; set; } = "#3498DB";
        public int SubCategoryCount { get; set; }
    }

    public class RoomSubCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Icon { get; set; } = "chat";
        public string Color { get; set; } = "#6C757D";
    }

    public class RoomSubscriptionTierDto
    {
        public int Tier { get; set; }
        public string Name { get; set; } = "Basic";
        public string? Description { get; set; }
        public string Color { get; set; } = "#95A5A6";
        public string Icon { get; set; } = "home";
        public int MaxUsers { get; set; }
        public int MaxMic { get; set; }
        public int MaxCam { get; set; }
        public bool AlwaysOnline { get; set; }
        public int MonthlyPriceCents { get; set; }
        public int YearlyPriceCents { get; set; }
    }

    public class MyRoomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string TierName { get; set; } = "Basic";
        public string TierColor { get; set; } = "#95A5A6";
        public int UserCount { get; set; }
        public int MaxUsers { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Information about a room's subscription tier
    /// </summary>
    public class RoomSubscriptionInfoDto
    {
        public int TierLevel { get; set; }
        public string TierName { get; set; } = "Basic";
        public string Color { get; set; } = "#95A5A6";
        public string Icon { get; set; } = "home";
        public int MaxUsers { get; set; }
        public int MaxMic { get; set; }
        public int MaxCam { get; set; }
        public bool AlwaysOnline { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// DTO for upgrading a room's subscription
    /// </summary>
    public class UpgradeRoomSubscriptionDto
    {
        public int RoomId { get; set; }
        public int NewTierLevel { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymentMethod { get; set; }
    }

    /// <summary>
    /// DTO for room role information
    /// </summary>
    public class RoomRoleInfoDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty; // RoomSuperAdmin, RoomAdmin, RoomModerator
    }

    /// <summary>
    /// DTO for pending role request (for target user)
    /// </summary>
    public class RoomRoleRequestDto
    {
        public int RequestId { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public int RequesterId { get; set; }
        public string RequesterName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO for pending role request info (for owner)
    /// </summary>
    public class RoomRoleRequestInfoDto
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int TargetUserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
    }
}
