using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PaLX.API.Hubs;
using PaLX.API.Models;

namespace PaLX.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly IHubContext<ChatHub> _hubContext;

        public AuthService(IConfiguration configuration, IHubContext<ChatHub> hubContext)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _hubContext = hubContext;
        }

        public async Task<AuthResult?> AuthenticateAsync(LoginModel model)
        {
            var user = await GetUserAsync(model.Username);
            if (user == null) return null;

            if (!BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                return null;
            }

            // Check for existing active session (unless force connect)
            var activeSession = await GetActiveSessionAsync(user.Id);
            if (activeSession != null && !model.ForceConnect)
            {
                // Ask user if they want to force disconnect
                return new AuthResult
                {
                    UserId = user.Id,
                    IsAlreadyConnected = true,
                    ActiveSessionDevice = activeSession.DeviceName,
                    ActiveSessionIP = activeSession.IP,
                    ActiveSessionSince = activeSession.ConnectedAt
                };
            }

            // Check Profile Completion
            bool isProfileComplete = await IsProfileCompleteAsync(user.Id);

            // Create Session if info provided
            int? newSessionId = null;
            if (!string.IsNullOrEmpty(model.IpAddress))
            {
                // If Admin Login, only create session if RoleLevel <= 6 (Admin roles)
                if (!model.IsAdminLogin || user.RoleLevel <= 6)
                {
                    // Create new session FIRST, then close old ones (atomic approach)
                    newSessionId = await CreateSessionAndCloseOthersAsync(user.Id, model.IpAddress, model.DeviceName, model.DeviceNumber);
                }
            }

            // If there was an active session and we're doing force connect, notify the old client
            if (activeSession != null && model.ForceConnect)
            {
                try
                {
                    await _hubContext.Clients.User(user.Username).SendAsync("ForceDisconnect", 
                        "Vous avez été déconnecté car une nouvelle session a été ouverte depuis un autre appareil.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AuthService] Error sending ForceDisconnect signal: {ex.Message}");
                }
            }

            var token = GenerateJwtToken(user);

            return new AuthResult
            {
                UserId = user.Id,
                Token = token,
                IsProfileComplete = isProfileComplete,
                Role = user.Role,
                RoleLevel = user.RoleLevel ?? 0
            };
        }

        private async Task<ActiveSessionInfo?> GetActiveSessionAsync(int userId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT ""DeviceName"", ""IP"", ""ConnectéLe""
                FROM ""UserSessions""
                WHERE ""UserId"" = @uid AND ""DéconnectéLe"" IS NULL
                ORDER BY ""ConnectéLe"" DESC
                LIMIT 1";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ActiveSessionInfo
                {
                    DeviceName = reader.IsDBNull(0) ? null : reader.GetString(0),
                    IP = reader.IsDBNull(1) ? null : reader.GetString(1),
                    ConnectedAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2)
                };
            }
            return null;
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

        /// <summary>
        /// Creates a new session and closes all other sessions for this user in a single atomic transaction.
        /// This prevents race conditions where another client could connect during the gap.
        /// </summary>
        private async Task<int?> CreateSessionAndCloseOthersAsync(int userId, string ip, string? deviceName, string? deviceNumber)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // Use a transaction to ensure atomicity
                using var transaction = await conn.BeginTransactionAsync();

                try
                {
                    // Step 1: Create the new session FIRST and get its ID
                    var insertSql = @"
                        INSERT INTO ""UserSessions"" (""UserId"", ""Nom"", ""Prenom"", ""IP"", ""DeviceName"", ""DeviceNumber"", ""ConnectéLe"", ""DisplayedStatus"")
                        SELECT u.""Id"", p.""LastName"", p.""FirstName"", @ip, @dn, @dnum, NOW(), 0
                        FROM ""Users"" u
                        LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                        WHERE u.""Id"" = @uid
                        RETURNING ""Id""";

                    int newSessionId;
                    using (var insertCmd = new NpgsqlCommand(insertSql, conn, transaction))
                    {
                        insertCmd.Parameters.AddWithValue("uid", userId);
                        insertCmd.Parameters.AddWithValue("ip", ip);
                        insertCmd.Parameters.AddWithValue("dn", deviceName ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("dnum", deviceNumber ?? (object)DBNull.Value);
                        newSessionId = (int)(await insertCmd.ExecuteScalarAsync() ?? 0);
                    }

                    // Step 2: Close ALL OTHER sessions for this user (excluding the one we just created)
                    var closeSql = @"
                        UPDATE ""UserSessions""
                        SET ""DéconnectéLe"" = NOW(), ""DisplayedStatus"" = 6
                        WHERE ""UserId"" = @uid 
                          AND ""DéconnectéLe"" IS NULL 
                          AND ""Id"" != @newId";

                    using (var closeCmd = new NpgsqlCommand(closeSql, conn, transaction))
                    {
                        closeCmd.Parameters.AddWithValue("uid", userId);
                        closeCmd.Parameters.AddWithValue("newId", newSessionId);
                        await closeCmd.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    return newSessionId;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] Session creation failed: {ex.Message}");
                return null;
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
                new Claim(ClaimTypes.Role, user.Role ?? "User"),
                new Claim("RoleLevel", (user.RoleLevel ?? 0).ToString()),
                new Claim("UserId", user.Id.ToString()) // Custom claim for API Controllers
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(24), // 24 heures au lieu de 7 jours (sécurité)
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}