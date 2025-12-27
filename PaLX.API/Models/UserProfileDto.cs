namespace PaLX.API.Models
{
    public class UserProfileDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; }
        public string Country { get; set; }
        public string? PhoneNumber { get; set; }
        public string? AvatarPath { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }
}