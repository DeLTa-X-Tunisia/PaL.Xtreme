using PaLX.API.Models;

namespace PaLX.API.Services
{
    public interface IAuthService
    {
        Task<AuthResult?> AuthenticateAsync(LoginModel model);
        Task<User?> GetUserAsync(string username);
    }
}