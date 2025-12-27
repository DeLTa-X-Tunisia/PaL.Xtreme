namespace PaLX.API.Models
{
    public class FriendDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty; // Name + Surname
        public string Status { get; set; } = "Hors ligne";
        public int StatusValue { get; set; } // For UI logic (Online status or Friendship status)
        public string AvatarPath { get; set; } = string.Empty;
        public int UnreadCount { get; set; }
        public string LastMessage { get; set; } = string.Empty;
        public DateTime LastMessageTime { get; set; }
        
        // Extra fields for Search/Profile
        public string Gender { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public int Age { get; set; }
        public int FriendshipStatus => StatusValue; // Alias for Admin UI
        public bool IsBlocked { get; set; }
    }
}