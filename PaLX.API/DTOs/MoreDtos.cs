using System;

namespace PaLX.API.DTOs
{
    public class BlockRequestModel
    {
        public string BlockedUsername { get; set; } = string.Empty;
        public int BlockType { get; set; } // 0: Indefinite, 1: 1 Week, 2: Custom
        public DateTime? EndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class BlockedUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public int BlockType { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Reason { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }
}
