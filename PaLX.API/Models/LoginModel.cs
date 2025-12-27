namespace PaLX.API.Models
{
    public class LoginModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? DeviceName { get; set; }
        public string? DeviceNumber { get; set; }
    }
}