namespace PaLX.API.Models
{
    public class AuthResult
    {
        public int UserId { get; set; }
        public string? Token { get; set; }
        public bool IsProfileComplete { get; set; }
        public string Role { get; set; } = string.Empty;
        public int RoleLevel { get; set; }
        
        // Session control properties
        public bool IsAlreadyConnected { get; set; } = false;
        public string? ActiveSessionDevice { get; set; }
        public string? ActiveSessionIP { get; set; }
        public DateTime? ActiveSessionSince { get; set; }
    }
    
    public class ActiveSessionInfo
    {
        public string? DeviceName { get; set; }
        public string? IP { get; set; }
        public DateTime? ConnectedAt { get; set; }
    }
}