using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PaLX.API.Models;
using PaLX.API.Services;

namespace PaLX.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")] // 5 tentatives par minute max
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            // Validation des entrées
            if (string.IsNullOrWhiteSpace(model.Username) || model.Username.Length < 3 || model.Username.Length > 50)
                return BadRequest(new { message = "Nom d'utilisateur invalide (3-50 caractères)" });
            
            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
                return BadRequest(new { message = "Mot de passe invalide (minimum 6 caractères)" });

            var result = await _authService.AuthenticateAsync(model);

            if (result == null)
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            return Ok(result);
        }
    }
}