using Npgsql;
using PaLX.API.Models;
using PaLX.API.DTOs;
using Microsoft.AspNetCore.SignalR;
using PaLX.API.Hubs;

namespace PaLX.API.Services
{
    public class UserService : IUserService
    {
        private readonly string _connectionString;
        private readonly IHubContext<ChatHub> _hubContext;

        public UserService(IConfiguration configuration, IHubContext<ChatHub> hubContext)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string not found.");
            _hubContext = hubContext;
        }

        public async Task<bool> RegisterUserAsync(string username, string password)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

                // 1. Insert User
                var sqlUser = "INSERT INTO \"Users\" (\"Username\", \"PasswordHash\", \"CreatedAt\") VALUES (@u, @p, @c) RETURNING \"Id\"";
                int userId;
                using (var cmd = new NpgsqlCommand(sqlUser, conn))
                {
                    cmd.Parameters.AddWithValue("u", username);
                    cmd.Parameters.AddWithValue("p", passwordHash);
                    cmd.Parameters.AddWithValue("c", DateTime.Now);
                    userId = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                }

                // 2. Get Role ID (Default User = 7)
                var sqlRole = "SELECT \"Id\" FROM \"Roles\" WHERE \"RoleLevel\" = 7";
                int roleId;
                using (var cmd = new NpgsqlCommand(sqlRole, conn))
                {
                    var res = await cmd.ExecuteScalarAsync();
                    if (res == null) return false;
                    roleId = (int)res;
                }

                // 3. Assign Role
                var sqlAssign = "INSERT INTO \"UserRoles\" (\"UserId\", \"RoleId\") VALUES (@u, @r)";
                using (var cmd = new NpgsqlCommand(sqlAssign, conn))
                {
                    cmd.Parameters.AddWithValue("u", userId);
                    cmd.Parameters.AddWithValue("r", roleId);
                    await cmd.ExecuteNonQueryAsync();
                }

                return true;
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // Unique violation
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<UserProfileDto?> GetUserProfileAsync(string username)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT p.""FirstName"", p.""LastName"", p.""Email"", p.""Gender"", p.""Country"", p.""PhoneNumber"", p.""AvatarPath"", p.""DateOfBirth""
                FROM ""UserProfiles"" p
                JOIN ""Users"" u ON p.""UserId"" = u.""Id""
                WHERE u.""Username"" = @u";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", username);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new UserProfileDto
                {
                    FirstName = reader.GetString(0),
                    LastName = reader.GetString(1),
                    Email = reader.GetString(2),
                    Gender = reader.GetString(3),
                    Country = reader.GetString(4),
                    PhoneNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                    AvatarPath = reader.IsDBNull(6) ? null : reader.GetString(6),
                    DateOfBirth = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7)
                };
            }
            return null;
        }

        public async Task<bool> UpdateUserProfileAsync(string username, UserProfileDto profile)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Get UserId
            var idCmd = new NpgsqlCommand("SELECT \"Id\" FROM \"Users\" WHERE \"Username\" = @u", conn);
            idCmd.Parameters.AddWithValue("u", username);
            var userId = (int?)await idCmd.ExecuteScalarAsync();
            if (userId == null) return false;

            var sql = @"
                INSERT INTO ""UserProfiles"" (""UserId"", ""FirstName"", ""LastName"", ""Email"", ""Gender"", ""Country"", ""PhoneNumber"", ""AvatarPath"", ""DateOfBirth"", ""IsComplete"")
                VALUES (@uid, @fn, @ln, @em, @gn, @co, @ph, @av, @dob, TRUE)
                ON CONFLICT (""UserId"") DO UPDATE SET
                    ""FirstName"" = EXCLUDED.""FirstName"",
                    ""LastName"" = EXCLUDED.""LastName"",
                    ""Email"" = EXCLUDED.""Email"",
                    ""Gender"" = EXCLUDED.""Gender"",
                    ""Country"" = EXCLUDED.""Country"",
                    ""PhoneNumber"" = EXCLUDED.""PhoneNumber"",
                    ""AvatarPath"" = EXCLUDED.""AvatarPath"",
                    ""DateOfBirth"" = EXCLUDED.""DateOfBirth"",
                    ""IsComplete"" = TRUE";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("uid", userId);
            cmd.Parameters.AddWithValue("fn", profile.FirstName);
            cmd.Parameters.AddWithValue("ln", profile.LastName);
            cmd.Parameters.AddWithValue("em", profile.Email);
            cmd.Parameters.AddWithValue("gn", profile.Gender);
            cmd.Parameters.AddWithValue("co", profile.Country);
            cmd.Parameters.AddWithValue("ph", profile.PhoneNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("av", profile.AvatarPath ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("dob", profile.DateOfBirth ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        public async Task<List<FriendDto>> GetFriendsAsync(string username)
        {
            var friends = new List<FriendDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Logic to get friends (Accepted status = 1)
            // Also join with UserSessions to get Status
            // Join with UserProfiles to get Name/Avatar
            var sql = @"
                SELECT u.""Id"", u.""Username"", 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as DisplayName,
                       COALESCE(p.""AvatarPath"", '') as AvatarPath,
                       COALESCE(s.""DisplayedStatus"", 6) as StatusInt,
                       CASE WHEN b.""Id"" IS NOT NULL THEN TRUE ELSE FALSE END as IsBlocked,
                       COALESCE(r.""RoleLevel"", 7) as RoleLevel
                FROM ""Friendships"" f
                JOIN ""Users"" u ON (f.""RequesterId"" = u.""Id"" OR f.""ReceiverId"" = u.""Id"")
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                LEFT JOIN ""UserSessions"" s ON u.""Id"" = s.""UserId"" AND s.""DéconnectéLe"" IS NULL
                LEFT JOIN ""BlockedUsers"" b ON b.""BlockerId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @me) AND b.""BlockedId"" = u.""Id""
                LEFT JOIN ""UserRoles"" ur ON u.""Id"" = ur.""UserId""
                LEFT JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id""
                WHERE (f.""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @me) OR 
                       f.""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @me))
                  AND u.""Username"" != @me
                  AND f.""Status"" = 1";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("me", username);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int statusInt = reader.GetInt32(4);
                bool isBlocked = reader.GetBoolean(5);
                int roleLevel = reader.GetInt32(6);
                
                string statusStr = statusInt switch
                {
                    0 => "En ligne",
                    1 => "Occupé",
                    2 => "Absent",
                    3 => "En appel",
                    4 => "Ne pas déranger",
                    _ => "Hors ligne"
                };

                friends.Add(new FriendDto
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    DisplayName = reader.GetString(2),
                    AvatarPath = reader.GetString(3),
                    Status = statusStr,
                    StatusValue = statusInt,
                    IsBlocked = isBlocked,
                    RoleLevel = roleLevel
                });
            }

            return friends;
        }

        public async Task<bool> BlockUserAsync(string blocker, BlockRequestModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Check Role Hierarchy
            var hierarchySql = @"
                SELECT 
                    (SELECT r.""RoleLevel"" FROM ""Users"" u 
                     JOIN ""UserRoles"" ur ON u.""Id"" = ur.""UserId"" 
                     JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id"" 
                     WHERE u.""Username"" = @b1) as BlockerLevel,
                    (SELECT r.""RoleLevel"" FROM ""Users"" u 
                     JOIN ""UserRoles"" ur ON u.""Id"" = ur.""UserId"" 
                     JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id"" 
                     WHERE u.""Username"" = @b2) as BlockedLevel";
            
            int blockerLevel = 7, blockedLevel = 7; // Default to User
            using (var hierarchyCmd = new NpgsqlCommand(hierarchySql, conn))
            {
                hierarchyCmd.Parameters.AddWithValue("b1", blocker);
                hierarchyCmd.Parameters.AddWithValue("b2", model.BlockedUsername);
                using var reader = await hierarchyCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0)) blockerLevel = reader.GetInt32(0);
                    if (!reader.IsDBNull(1)) blockedLevel = reader.GetInt32(1);
                }
            }

            // Rule: BlockerLevel must be <= BlockedLevel (Lower value = Higher Rank)
            if (blockerLevel > blockedLevel)
            {
                throw new InvalidOperationException("Action non autorisée : vous ne pouvez pas bloquer un rôle supérieur.");
            }

            var sql = @"
                INSERT INTO ""BlockedUsers"" (""BlockerId"", ""BlockedId"", ""BlockType"", ""StartDate"", ""EndDate"", ""Reason"")
                SELECT u1.""Id"", u2.""Id"", @bt, NOW(), @ed, @rs
                FROM ""Users"" u1, ""Users"" u2
                WHERE u1.""Username"" = @b1 AND u2.""Username"" = @b2
                ON CONFLICT (""BlockerId"", ""BlockedId"") DO UPDATE SET
                    ""BlockType"" = EXCLUDED.""BlockType"",
                    ""StartDate"" = NOW(),
                    ""EndDate"" = EXCLUDED.""EndDate"",
                    ""Reason"" = EXCLUDED.""Reason""";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("b1", blocker);
            cmd.Parameters.AddWithValue("b2", model.BlockedUsername);
            cmd.Parameters.AddWithValue("bt", model.BlockType);
            cmd.Parameters.AddWithValue("ed", model.EndDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("rs", model.Reason ?? (object)DBNull.Value);
            
            var result = await cmd.ExecuteNonQueryAsync() > 0;
            if (result)
            {
                // Insert System Message for History Persistence
                var msgSql = @"
                    INSERT INTO ""Messages"" (""SenderUsername"", ""ReceiverUsername"", ""Content"", ""Timestamp"", ""IsRead"")
                    VALUES (@b1, @b2, '[SYSTEM_BLOCK]', NOW(), FALSE)";
                using (var msgCmd = new NpgsqlCommand(msgSql, conn))
                {
                    msgCmd.Parameters.AddWithValue("b1", blocker);
                    msgCmd.Parameters.AddWithValue("b2", model.BlockedUsername);
                    await msgCmd.ExecuteNonQueryAsync();
                }

                // Notify both parties
                await _hubContext.Clients.User(blocker).SendAsync("UserBlocked", model.BlockedUsername);
                await _hubContext.Clients.User(model.BlockedUsername).SendAsync("UserBlockedBy", blocker);
            }
            return result;
        }

        public async Task<List<BlockedUserDto>> GetBlockedUsersAsync(string username)
        {
            var list = new List<BlockedUserDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT u.""Username"", 
                       p.""LastName"",
                       p.""FirstName"",
                       p.""AvatarPath"", 
                       b.""BlockType"", 
                       b.""EndDate"", 
                       b.""Reason"",
                       r.""RoleName"" as RoleName
                FROM ""BlockedUsers"" b
                JOIN ""Users"" u ON b.""BlockedId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                LEFT JOIN ""UserRoles"" ur ON u.""Id"" = ur.""UserId""
                LEFT JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id""
                WHERE b.""BlockerId"" = (SELECT ""Id"" FROM ""Users"" WHERE LOWER(""Username"") = LOWER(@u))";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", username);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string usernameDb = reader.GetString(0);
                string? lastName = reader.IsDBNull(1) ? null : reader.GetString(1);
                string? firstName = reader.IsDBNull(2) ? null : reader.GetString(2);
                string displayName = (!string.IsNullOrEmpty(lastName) && !string.IsNullOrEmpty(firstName)) 
                                     ? $"{lastName} {firstName}" 
                                     : usernameDb;

                list.Add(new BlockedUserDto
                {
                    Username = usernameDb,
                    DisplayName = displayName,
                    AvatarPath = reader.IsDBNull(3) ? null : reader.GetString(3),
                    BlockType = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    EndDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    Reason = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Role = reader.IsDBNull(7) ? "Utilisateur" : reader.GetString(7)
                });
            }
            return list;
        }

        public async Task<List<ChatMessageDto>> GetChatHistoryAsync(string user1, string user2)
        {
            var messages = new List<ChatMessageDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT ""Id"", ""SenderUsername"", ""ReceiverUsername"", ""Content"", ""Timestamp"", ""IsRead""
                FROM ""Messages""
                WHERE (LOWER(""SenderUsername"") = LOWER(@u1) AND LOWER(""ReceiverUsername"") = LOWER(@u2) AND ""DeletedBySender"" = FALSE)
                   OR (LOWER(""SenderUsername"") = LOWER(@u2) AND LOWER(""ReceiverUsername"") = LOWER(@u1) AND ""DeletedByReceiver"" = FALSE)
                UNION ALL
                SELECT ""Id"", ""SenderUsername"", ""ReceiverUsername"", '[FILE_REQUEST:' || ""Id"" || ':' || ""FileName"" || ':' || ""FileUrl"" || ':' || ""Status"" || ']', ""Timestamp"", (""Status"" != 0)
                FROM ""FileTransfers""
                WHERE (LOWER(""SenderUsername"") = LOWER(@u1) AND LOWER(""ReceiverUsername"") = LOWER(@u2) AND ""DeletedBySender"" = FALSE)
                   OR (LOWER(""SenderUsername"") = LOWER(@u2) AND LOWER(""ReceiverUsername"") = LOWER(@u1) AND ""DeletedByReceiver"" = FALSE)
                ORDER BY ""Timestamp"" ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u1", user1);
            cmd.Parameters.AddWithValue("u2", user2);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(new ChatMessageDto
                {
                    Id = reader.GetInt32(0),
                    Sender = reader.GetString(1),
                    Receiver = reader.GetString(2),
                    Content = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Timestamp = reader.GetDateTime(4),
                    IsRead = reader.GetBoolean(5)
                });
            }
            return messages;
        }

        public async Task MarkMessagesAsReadAsync(string sender, string receiver)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Mark Messages as Read
            var sqlMessages = @"
                UPDATE ""Messages""
                SET ""IsRead"" = TRUE
                WHERE LOWER(""SenderUsername"") = LOWER(@s) AND LOWER(""ReceiverUsername"") = LOWER(@r) AND ""IsRead"" = FALSE";

            using (var cmd = new NpgsqlCommand(sqlMessages, conn))
            {
                cmd.Parameters.AddWithValue("s", sender);
                cmd.Parameters.AddWithValue("r", receiver);
                await cmd.ExecuteNonQueryAsync();
            }

            // Mark FileTransfers as Read
            var sqlFiles = @"
                UPDATE ""FileTransfers""
                SET ""IsRead"" = TRUE
                WHERE LOWER(""SenderUsername"") = LOWER(@s) AND LOWER(""ReceiverUsername"") = LOWER(@r) AND ""IsRead"" = FALSE";

            using (var cmd = new NpgsqlCommand(sqlFiles, conn))
            {
                cmd.Parameters.AddWithValue("s", sender);
                cmd.Parameters.AddWithValue("r", receiver);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<bool> UnblockUserAsync(string blocker, string blocked)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                DELETE FROM ""BlockedUsers""
                WHERE ""BlockerId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @b1)
                  AND ""BlockedId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @b2)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("b1", blocker);
            cmd.Parameters.AddWithValue("b2", blocked);
            
            var result = await cmd.ExecuteNonQueryAsync() > 0;
            if (result)
            {
                // Insert System Message for History Persistence
                var msgSql = @"
                    INSERT INTO ""Messages"" (""SenderUsername"", ""ReceiverUsername"", ""Content"", ""Timestamp"", ""IsRead"")
                    VALUES (@b1, @b2, '[SYSTEM_UNBLOCK]', NOW(), FALSE)";
                using (var msgCmd = new NpgsqlCommand(msgSql, conn))
                {
                    msgCmd.Parameters.AddWithValue("b1", blocker);
                    msgCmd.Parameters.AddWithValue("b2", blocked);
                    await msgCmd.ExecuteNonQueryAsync();
                }

                // Notify both parties
                await _hubContext.Clients.User(blocker).SendAsync("UserUnblocked", blocked);
                await _hubContext.Clients.User(blocked).SendAsync("UserUnblockedBy", blocker);
            }
            return result;
        }

        public async Task<bool> IsUserBlockedAsync(string user1, string user2)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT 1 FROM ""BlockedUsers""
                WHERE ""BlockerId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u1)
                  AND ""BlockedId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u2)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u1", user1);
            cmd.Parameters.AddWithValue("u2", user2);
            return await cmd.ExecuteScalarAsync() != null;
        }

        public async Task UpdateStatusAsync(string username, int status)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                UPDATE ""UserSessions""
                SET ""DisplayedStatus"" = @s
                WHERE ""UserId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u)
                  AND ""DéconnectéLe"" IS NULL";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("s", status);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> RemoveFriendAsync(string username, string friendUsername)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                DELETE FROM ""Friendships""
                WHERE (""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u1) 
                   AND ""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u2))
                   OR (""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u2) 
                   AND ""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u1))";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u1", username);
            cmd.Parameters.AddWithValue("u2", friendUsername);
            
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task<List<FriendDto>> GetPendingRequestsAsync(string username)
        {
            var requests = new List<FriendDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT u.""Username"", 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as DisplayName,
                       p.""AvatarPath""
                FROM ""Friendships"" f
                JOIN ""Users"" u ON f.""RequesterId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                WHERE f.""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u)
                  AND f.""Status"" = 0"; // 0 = Pending

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", username);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                requests.Add(new FriendDto
                {
                    Username = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    AvatarPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                    StatusValue = 0 // Pending
                });
            }
            return requests;
        }

        public async Task<List<FriendDto>> SearchUsersAsync(string query, string currentUsername)
        {
            var users = new List<FriendDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT u.""Username"", 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as DisplayName,
                       p.""AvatarPath"",
                       (SELECT f.""Status"" FROM ""Friendships"" f 
                        WHERE (f.""RequesterId"" = u.""Id"" AND f.""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @cu))
                           OR (f.""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @cu) AND f.""ReceiverId"" = u.""Id"")) as FriendStatus,
                       p.""Gender"",
                       p.""Country"",
                       p.""DateOfBirth""
                FROM ""Users"" u
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                WHERE u.""Username"" != @cu
                  AND (u.""Username"" ILIKE @q OR p.""FirstName"" ILIKE @q OR p.""LastName"" ILIKE @q)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("cu", currentUsername);
            cmd.Parameters.AddWithValue("q", $"%{query}%");
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int? status = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                DateTime? dob = reader.IsDBNull(6) ? null : reader.GetDateTime(6);
                int age = 0;
                if (dob.HasValue)
                {
                    var today = DateTime.Today;
                    age = today.Year - dob.Value.Year;
                    if (dob.Value.Date > today.AddYears(-age)) age--;
                }
                
                users.Add(new FriendDto
                {
                    Username = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    AvatarPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                    StatusValue = status ?? 2, // 2 = No relation (for UI)
                    Gender = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Country = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Age = age
                });
            }
            return users;
        }

        public async Task<bool> SendFriendRequestAsync(string fromUser, string toUser)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO ""Friendships"" (""RequesterId"", ""ReceiverId"", ""Status"")
                SELECT u1.""Id"", u2.""Id"", 0
                FROM ""Users"" u1, ""Users"" u2
                WHERE u1.""Username"" = @u1 AND u2.""Username"" = @u2
                ON CONFLICT DO NOTHING";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u1", fromUser);
            cmd.Parameters.AddWithValue("u2", toUser);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> RespondToFriendRequestAsync(string responder, string requester, int response)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            if (response == 1 || response == 2) // Accept
            {
                var sql = @"
                    UPDATE ""Friendships""
                    SET ""Status"" = 1
                    WHERE ""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @req)
                      AND ""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @res)";
                
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("req", requester);
                cmd.Parameters.AddWithValue("res", responder);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            else // Decline
            {
                var sql = @"
                    DELETE FROM ""Friendships""
                    WHERE ""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @req)
                      AND ""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @res)";
                
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("req", requester);
                cmd.Parameters.AddWithValue("res", responder);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<List<string>> GetUnreadSendersAsync(string username)
        {
            var senders = new List<string>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT DISTINCT ""SenderUsername"" FROM ""Messages"" WHERE LOWER(""ReceiverUsername"") = LOWER(@u) AND ""IsRead"" = false
                UNION
                SELECT DISTINCT ""SenderUsername"" FROM ""FileTransfers"" WHERE LOWER(""ReceiverUsername"") = LOWER(@u) AND ""IsRead"" = false";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", username);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                senders.Add(reader.GetString(0));
            }
            return senders;
        }
    }
}