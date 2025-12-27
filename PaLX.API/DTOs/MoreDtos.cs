using System;

namespace PaLX.API.DTOs
{
    public class BlockRequestModel
    {
        public string BlockedUsername { get; set; }
        public int BlockType { get; set; } // 0: Indefinite, 1: 1 Week, 2: Custom
        public DateTime? EndDate { get; set; }
        public string Reason { get; set; }
    }

    public class BlockedUserDto
    {
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string AvatarPath { get; set; }
        public int BlockType { get; set; }
        public DateTime? EndDate { get; set; }
        public string Reason { get; set; }
        public string Role { get; set; }
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string Sender { get; set; }
        public string Receiver { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }
}
