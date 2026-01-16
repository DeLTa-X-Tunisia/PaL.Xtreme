using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PaLX.API.Models;
using PaLX.API.Services;
using System.Security.Claims;

namespace PaLX.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;

        public AuthController(IAuthService authService, IUserService userService)
        {
            _authService = authService;
            _userService = userService;
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")] // 5 tentatives par minute max
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            // Validation des entrées
            if (string.IsNullOrWhiteSpace(model.Username) || model.Username.Length < 3 || model.Username.Length > 50)
                return BadRequest(new { message = "Nom d'utilisateur invalide (3-50 caractères)" });
            
            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 8)
                return BadRequest(new { message = "Mot de passe invalide (minimum 8 caractères)" });

            var result = await _authService.AuthenticateAsync(model);

            if (result == null)
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            return Ok(result);
        }

        /// <summary>
        /// Endpoint pour l'authentification admin (Panel React)
        /// Vérifie que l'utilisateur a un RoleLevel 1-6 (SystemAdmin)
        /// </summary>
        [HttpPost("admin/login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> AdminLogin([FromBody] LoginModel model)
        {
            // Validation des entrées
            if (string.IsNullOrWhiteSpace(model.Username) || model.Username.Length < 3 || model.Username.Length > 50)
                return BadRequest(new { message = "Nom d'utilisateur invalide (3-50 caractères)" });
            
            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 8)
                return BadRequest(new { message = "Mot de passe invalide (minimum 8 caractères)" });

            var result = await _authService.AuthenticateAsync(model);

            if (result == null)
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            // Vérifier que c'est bien un admin système (RoleLevel 1-6)
            var user = await _userService.GetByUsernameAsync(model.Username);
            if (user == null || !Constants.RoleLevels.IsSystemAdmin(user.RoleLevel ?? 0))
            {
                return Forbid(); // 403 Forbidden - pas un admin
            }

            // Retourner les infos complètes pour le panel admin
            return Ok(new
            {
                result.Token,
                User = new
                {
                    user.Id,
                    user.Username,
                    user.DisplayName,
                    user.FirstName,
                    user.LastName,
                    user.Role,
                    user.RoleLevel,
                    RoleName = user.Role, // Nom technique (ServerMaster)
                    user.RoleDisplayName, // Nom affiché (Maître du Serveur)
                    user.RoleColor, // Couleur du rôle (#FFD700)
                    user.Avatar,
                    user.AvatarPath, // Chemin vers la photo de profil
                    user.CreatedAt
                }
            });
        }

        /// <summary>
        /// Valide un token JWT et retourne les informations utilisateur
        /// Utilisé par le React Admin Panel pour vérifier la session
        /// </summary>
        [HttpGet("validate")]
        [Authorize]
        public async Task<IActionResult> ValidateToken()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid token" });
            }

            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized(new { message = "User not found" });
            }

            return Ok(new
            {
                user.Id,
                user.Username,
                user.DisplayName,
                user.FirstName,
                user.LastName,
                user.Role,
                user.RoleLevel,
                RoleName = user.Role, // Nom technique
                user.RoleDisplayName, // Nom affiché (Maître du Serveur, Modérateur, etc.)
                user.RoleColor, // Couleur du rôle (#FFD700)
                IsSystemAdmin = Constants.RoleLevels.IsSystemAdmin(user.RoleLevel ?? 0),
                user.Avatar,
                user.AvatarPath // Chemin vers la photo de profil
            });
        }
    }
}