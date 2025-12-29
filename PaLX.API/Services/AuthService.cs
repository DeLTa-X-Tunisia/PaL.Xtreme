using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PaLX.API.Models;

namespace PaLX.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AuthService(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public async Task<AuthResult?> AuthenticateAsync(LoginModel model)
        {
            var user = await GetUserAsync(model.Username);
            if (user == null) return null;

            if (!BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                return null;
            }

            // Check Profile Completion
            bool isProfileComplete = await IsProfileCompleteAsync(user.Id);

            // Create Session if info provided
            if (!string.IsNullOrEmpty(model.IpAddress))
            {
                // If Admin Login, only create session if RoleLevel <= 6 (Admin roles)
                // Role 7 (User) should NOT appear online if attempting to login to Admin
                if (!model.IsAdminLogin || user.RoleLevel <= 6)
                {
                    await CreateSessionAsync(user.Id, model.IpAddress, model.DeviceName, model.DeviceNumber);
                }
            }

            var token = GenerateJwtToken(user);

            return new AuthResult
            {
                UserId = user.Id,
                Token = token,
                IsProfileComplete = isProfileComplete,
                Role = user.Role,
                RoleLevel = user.RoleLevel
            };
        }

        public async Task<User?> GetUserAsync(string username)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT u.""Id"", u.""Username"", u.""PasswordHash"", r.""RoleName"", r.""RoleLevel""
                FROM ""Users"" u
                JOIN ""UserRoles"" ur ON u.""Id"" = ur.""UserId""
                JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id""
                WHERE u.""Username"" = @u";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", username);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    Role = reader.GetString(3),
                    RoleLevel = reader.GetInt32(4)
                };
            }

            return null;
        }

        private async Task<bool> IsProfileCompleteAsync(int userId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var sql = @"SELECT ""IsComplete"" FROM ""UserProfiles"" WHERE ""UserId"" = @uid";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("uid", userId);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return (bool)result;
            }
            return false;
        }

        private async Task CreateSessionAsync(int userId, string ip, string? deviceName, string? deviceNumber)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // Close old sessions
                var closeSql = @"
                    UPDATE ""UserSessions""
                    SET ""DéconnectéLe"" = NOW(), ""DisplayedStatus"" = 6
                    WHERE ""UserId"" = @uid AND ""DéconnectéLe"" IS NULL";
                using (var closeCmd = new NpgsqlCommand(closeSql, conn))
                {
                    closeCmd.Parameters.AddWithValue("uid", userId);
                    await closeCmd.ExecuteNonQueryAsync();
                }

                // Create new session
                var sql = @"
                    INSERT INTO ""UserSessions"" (""UserId"", ""Nom"", ""Prenom"", ""IP"", ""DeviceName"", ""DeviceNumber"", ""ConnectéLe"", ""DisplayedStatus"")
                    SELECT u.""Id"", p.""LastName"", p.""FirstName"", @ip, @dn, @dnum, NOW(), 0
                    FROM ""Users"" u
                    LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                    WHERE u.""Id"" = @uid";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("uid", userId);
                    cmd.Parameters.AddWithValue("ip", ip);
                    cmd.Parameters.AddWithValue("dn", deviceName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("dnum", deviceNumber ?? (object)DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Session creation failed: {ex.Message}");
            }
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing in configuration.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Username), // SignalR uses this for UserIdentifier
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("RoleLevel", user.RoleLevel.ToString()),
                new Claim("UserId", user.Id.ToString()) // Custom claim for API Controllers
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}