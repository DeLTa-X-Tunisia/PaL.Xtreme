using System;

namespace PaLX.API.Models
{
    public class Room
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int OwnerId { get; set; }
        public int MaxUsers { get; set; } = 50;
        public int MaxMics { get; set; } = 1;
        public int MaxCams { get; set; } = 2;
        public bool IsPrivate { get; set; }
        public string? Password { get; set; }
        public bool Is18Plus { get; set; }
        public int SubscriptionLevel { get; set; } // 0=Basic, 1=Deluxe, etc.
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class RoomCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ParentId { get; set; } // For subcategories
        public int Order { get; set; }
    }

    public class RoomMember
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int UserId { get; set; }
        public int RoleId { get; set; } // Room Role (Owner, Admin, etc.)
        public DateTime JoinedAt { get; set; }
        public bool IsBanned { get; set; }
        public bool IsMuted { get; set; }
        public bool HasHandRaised { get; set; }
        public bool IsCamOn { get; set; }
        public bool IsMicOn { get; set; }
    }

    public class RoomMessage
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int UserId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "Text"; // Text, Image, Video, Gift
        public DateTime Timestamp { get; set; }
        public string? AttachmentUrl { get; set; }
    }

    public class RoomRole
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // RoomOwner, RoomAdmin, etc.
        public int Level { get; set; } // 1=Owner, 2=SuperAdmin... 6=Member
        public string Color { get; set; } = "#000000";
        public string Icon { get; set; } = string.Empty;
    }

    public class UserSubscription
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SubscriptionType { get; set; } // 0=Member, 1=Deluxe... 9=Legend
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
    }
}
