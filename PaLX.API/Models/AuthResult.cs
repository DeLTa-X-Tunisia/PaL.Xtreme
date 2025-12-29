namespace PaLX.API.Models
{
    public class AuthResult
    {
        public int UserId { get; set; }
        public string? Token { get; set; }
        public bool IsProfileComplete { get; set; }
        public string Role { get; set; } = string.Empty;
        public int RoleLevel { get; set; }
    }
}