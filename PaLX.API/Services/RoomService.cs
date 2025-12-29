using Npgsql;
using PaLX.API.DTOs;
using PaLX.API.Models;
using PaLX.API.Enums;
using Microsoft.AspNetCore.SignalR;
using PaLX.API.Hubs;

namespace PaLX.API.Services
{
    public class RoomService : IRoomService
    {
        private readonly string _connectionString;
        private readonly IHubContext<RoomHub> _hubContext;
        private readonly IAccessControlService _accessControl;

        public RoomService(IConfiguration configuration, IHubContext<RoomHub> hubContext, IAccessControlService accessControl)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string not found.");
            _hubContext = hubContext;
            _accessControl = accessControl;
        }

        public async Task<RoomDto> CreateRoomAsync(int userId, CreateRoomDto dto)
        {
            // 1. Access Control Check
            var roomLevel = (RoomSubscriptionLevel)dto.SubscriptionLevel;
            if (!await _accessControl.CanCreateRoomAsync(userId, roomLevel, dto.Is18Plus))
            {
                throw new UnauthorizedAccessException("Permission denied: Check your subscription level or room limits.");
            }

            // 2. Enforce Max Users based on Room Level
            var maxAllowed = _accessControl.GetMaxRoomCapacity(roomLevel);
            if (dto.MaxUsers > maxAllowed) dto.MaxUsers = maxAllowed;

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO ""Rooms"" 
                (""Name"", ""Description"", ""CategoryId"", ""OwnerId"", ""MaxUsers"", ""IsPrivate"", ""Password"", ""Is18Plus"", ""SubscriptionLevel"")
                VALUES (@name, @desc, @cat, @owner, @max, @priv, @pass, @adult, @sub)
                RETURNING ""Id"", ""CreatedAt""";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("name", dto.Name);
            cmd.Parameters.AddWithValue("desc", dto.Description);
            cmd.Parameters.AddWithValue("cat", dto.CategoryId);
            cmd.Parameters.AddWithValue("owner", userId);
            cmd.Parameters.AddWithValue("max", dto.MaxUsers);
            cmd.Parameters.AddWithValue("priv", dto.IsPrivate);
            cmd.Parameters.AddWithValue("pass", (object?)dto.Password ?? DBNull.Value);
            cmd.Parameters.AddWithValue("adult", dto.Is18Plus);
            cmd.Parameters.AddWithValue("sub", dto.SubscriptionLevel);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var roomId = reader.GetInt32(0);
                
                // Add Owner as RoomOwner (Level 1)
                await reader.CloseAsync();
                await AddMemberToRoomInternal(conn, roomId, userId, (int)RoomRoleLevel.Owner);

                return new RoomDto
                {
                    Id = roomId,
                    Name = dto.Name,
                    Description = dto.Description,
                    CategoryId = dto.CategoryId,
                    OwnerId = userId,
                    MaxUsers = dto.MaxUsers,
                    IsPrivate = dto.IsPrivate,
                    Is18Plus = dto.Is18Plus,
                    SubscriptionLevel = dto.SubscriptionLevel,
                    UserCount = 1
                };
            }
            throw new Exception("Failed to create room");
        }

        public async Task<bool> JoinRoomAsync(int userId, int roomId, string? password)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // 1. Get Room Details
            var roomSql = "SELECT \"Password\", \"IsPrivate\", \"MaxUsers\", \"Is18Plus\", \"SubscriptionLevel\" FROM \"Rooms\" WHERE \"Id\" = @id";
            string? dbPass = null;
            bool isPrivate = false;
            int maxUsers = 0;
            bool is18Plus = false;
            int subLevel = 0;

            using (var cmd = new NpgsqlCommand(roomSql, conn))
            {
                cmd.Parameters.AddWithValue("id", roomId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return false; // Room not found
                
                dbPass = reader.IsDBNull(0) ? null : reader.GetString(0);
                isPrivate = reader.GetBoolean(1);
                maxUsers = reader.GetInt32(2);
                is18Plus = reader.GetBoolean(3);
                subLevel = reader.GetInt32(4);
            }

            // 2. Check Password
            if (isPrivate && dbPass != password) return false;

            // 3. Check 18+ (Age Check)
            if (is18Plus)
            {
                var ageSql = "SELECT \"DateOfBirth\" FROM \"UserProfiles\" WHERE \"UserId\" = @uid";
                using (var cmd = new NpgsqlCommand(ageSql, conn))
                {
                    cmd.Parameters.AddWithValue("uid", userId);
                    var dobObj = await cmd.ExecuteScalarAsync();
                    if (dobObj != null && dobObj != DBNull.Value)
                    {
                        var dob = (DateTime)dobObj;
                        var age = DateTime.Today.Year - dob.Year;
                        if (dob.Date > DateTime.Today.AddYears(-age)) age--;
                        if (age < 18) return false;
                    }
                    else
                    {
                        // No profile or DOB? Deny access to 18+ room
                        return false;
                    }
                }
            }

            // 4. Check Capacity (DB Count vs MaxUsers)
            var countSql = "SELECT COUNT(*) FROM \"RoomMembers\" WHERE \"RoomId\" = @rid";
            using (var cmd = new NpgsqlCommand(countSql, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                long count = (long)(await cmd.ExecuteScalarAsync() ?? 0);
                if (count >= maxUsers) return false;
            }

            // 5. Check if already member
            var checkMember = "SELECT COUNT(*) FROM \"RoomMembers\" WHERE \"RoomId\" = @rid AND \"UserId\" = @uid";
            using (var cmd = new NpgsqlCommand(checkMember, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                cmd.Parameters.AddWithValue("uid", userId);
                long exists = (long)(await cmd.ExecuteScalarAsync() ?? 0);
                if (exists > 0) return true; // Already joined
            }

            // 6. Add Member (Default Role = Member)
            await AddMemberToRoomInternal(conn, roomId, userId, (int)RoomRoleLevel.Member);
            
            // Get Member Details for Notification
            var memberDto = await GetRoomMemberDetailsAsync(conn, roomId, userId);

            // Notify SignalR
            await _hubContext.Clients.Group($"Room_{roomId}").SendAsync("UserJoined", memberDto);

            return true;
        }

        private async Task AddMemberToRoomInternal(NpgsqlConnection conn, int roomId, int userId, int roleId)
        {
            var sql = @"
                INSERT INTO ""RoomMembers"" (""RoomId"", ""UserId"", ""RoleId"")
                VALUES (@rid, @uid, @role)";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("rid", roomId);
            cmd.Parameters.AddWithValue("uid", userId);
            cmd.Parameters.AddWithValue("role", roleId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task LeaveRoomAsync(int userId, int roomId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "DELETE FROM \"RoomMembers\" WHERE \"RoomId\" = @rid AND \"UserId\" = @uid";
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                cmd.Parameters.AddWithValue("uid", userId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Notify SignalR
            await _hubContext.Clients.Group($"Room_{roomId}").SendAsync("UserLeft", userId);
        }

        public async Task<List<RoomDto>> GetRoomsAsync(int? categoryId = null)
        {
            var rooms = new List<RoomDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT r.*, c.""Name"" as CatName, u.""Username"" as OwnerName,
                       (SELECT COUNT(*) FROM ""RoomMembers"" rm WHERE rm.""RoomId"" = r.""Id"") as UserCount
                FROM ""Rooms"" r
                JOIN ""RoomCategories"" c ON r.""CategoryId"" = c.""Id""
                JOIN ""Users"" u ON r.""OwnerId"" = u.""Id""
                WHERE r.""IsActive"" = TRUE";

            if (categoryId.HasValue)
            {
                sql += " AND r.\"CategoryId\" = @cat";
            }

            using var cmd = new NpgsqlCommand(sql, conn);
            if (categoryId.HasValue) cmd.Parameters.AddWithValue("cat", categoryId.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rooms.Add(new RoomDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString(reader.GetOrdinal("Description")),
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CatName")),
                    OwnerId = reader.GetInt32(reader.GetOrdinal("OwnerId")),
                    OwnerName = reader.GetString(reader.GetOrdinal("OwnerName")),
                    MaxUsers = reader.GetInt32(reader.GetOrdinal("MaxUsers")),
                    IsPrivate = reader.GetBoolean(reader.GetOrdinal("IsPrivate")),
                    Is18Plus = reader.GetBoolean(reader.GetOrdinal("Is18Plus")),
                    SubscriptionLevel = reader.GetInt32(reader.GetOrdinal("SubscriptionLevel")),
                    UserCount = (int)reader.GetInt64(reader.GetOrdinal("UserCount"))
                });
            }
            return rooms;
        }

        public async Task<List<RoomMemberDto>> GetRoomMembersAsync(int roomId)
        {
            var members = new List<RoomMemberDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT u.""Id"", u.""Username"", 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as DisplayName,
                       p.""AvatarPath"",
                       rr.""Id"" as RoleId, rr.""Name"" as RoleName, rr.""Color"" as RoleColor, rr.""Icon"" as RoleIcon,
                       rm.""IsMuted"", rm.""HasHandRaised"", rm.""IsCamOn"", rm.""IsMicOn""
                FROM ""RoomMembers"" rm
                JOIN ""Users"" u ON rm.""UserId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                JOIN ""RoomRoles"" rr ON rm.""RoleId"" = rr.""Id""
                WHERE rm.""RoomId"" = @rid";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("rid", roomId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                members.Add(new RoomMemberDto
                {
                    UserId = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    DisplayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(reader.GetString(2).ToLower()),
                    AvatarPath = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    RoleId = reader.GetInt32(4),
                    RoleName = reader.GetString(5),
                    RoleColor = reader.IsDBNull(6) ? "#000000" : reader.GetString(6),
                    RoleIcon = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    IsMuted = reader.GetBoolean(8),
                    HasHandRaised = reader.GetBoolean(9),
                    IsCamOn = reader.GetBoolean(10),
                    IsMicOn = reader.GetBoolean(11)
                });
            }
            return members;
        }

        public async Task<RoomMessageDto> SendMessageAsync(int userId, int roomId, string content, string type = "Text", string? attachmentUrl = null)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Insert Message
            var sql = @"
                INSERT INTO ""RoomMessages"" (""RoomId"", ""UserId"", ""Content"", ""MessageType"", ""AttachmentUrl"")
                VALUES (@rid, @uid, @content, @type, @url)
                RETURNING ""Id"", ""Timestamp""";

            int msgId;
            DateTime timestamp;

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                cmd.Parameters.AddWithValue("uid", userId);
                cmd.Parameters.AddWithValue("content", content);
                cmd.Parameters.AddWithValue("type", type);
                cmd.Parameters.AddWithValue("url", (object?)attachmentUrl ?? DBNull.Value);
                
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new Exception("Failed to send message");
                msgId = reader.GetInt32(0);
                timestamp = reader.GetDateTime(1);
            }

            // Get User Info for DTO
            var userSql = @"
                SELECT u.""Username"", 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as DisplayName,
                       rr.""Color""
                FROM ""RoomMembers"" rm
                JOIN ""Users"" u ON rm.""UserId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                JOIN ""RoomRoles"" rr ON rm.""RoleId"" = rr.""Id""
                WHERE rm.""RoomId"" = @rid AND rm.""UserId"" = @uid";

            string username = "", displayName = "", roleColor = "#000000";
            using (var cmd = new NpgsqlCommand(userSql, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                cmd.Parameters.AddWithValue("uid", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    username = reader.GetString(0);
                    displayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(reader.GetString(1).ToLower());
                    roleColor = reader.IsDBNull(2) ? "#000000" : reader.GetString(2);
                }
            }

            var dto = new RoomMessageDto
            {
                Id = msgId,
                RoomId = roomId,
                UserId = userId,
                Username = username,
                DisplayName = displayName,
                RoleColor = roleColor,
                Content = content,
                MessageType = type,
                Timestamp = timestamp,
                AttachmentUrl = attachmentUrl
            };

            // Broadcast via SignalR
            await _hubContext.Clients.Group($"Room_{roomId}").SendAsync("ReceiveMessage", dto);

            return dto;
        }

        public async Task<List<RoomMessageDto>> GetRoomMessagesAsync(int roomId, int limit = 50)
        {
            var messages = new List<RoomMessageDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT m.""Id"", m.""UserId"", m.""Content"", m.""MessageType"", m.""Timestamp"", m.""AttachmentUrl"",
                       u.""Username"", 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as DisplayName,
                       rr.""Color""
                FROM ""RoomMessages"" m
                JOIN ""Users"" u ON m.""UserId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                LEFT JOIN ""RoomMembers"" rm ON m.""UserId"" = rm.""UserId"" AND m.""RoomId"" = rm.""RoomId""
                LEFT JOIN ""RoomRoles"" rr ON rm.""RoleId"" = rr.""Id""
                WHERE m.""RoomId"" = @rid
                ORDER BY m.""Timestamp"" DESC
                LIMIT @limit";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("rid", roomId);
            cmd.Parameters.AddWithValue("limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(new RoomMessageDto
                {
                    Id = reader.GetInt32(0),
                    RoomId = roomId,
                    UserId = reader.GetInt32(1),
                    Content = reader.GetString(2),
                    MessageType = reader.GetString(3),
                    Timestamp = reader.GetDateTime(4),
                    AttachmentUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Username = reader.GetString(6),
                    DisplayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(reader.GetString(7).ToLower()),
                    RoleColor = reader.IsDBNull(8) ? "#000000" : reader.GetString(8)
                });
            }
            
            messages.Reverse(); // Return in chronological order
            return messages;
        }

        public async Task<bool> UpdateMemberStatusAsync(int userId, int roomId, bool? isCamOn, bool? isMicOn, bool? hasHandRaised)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var updates = new List<string>();
            if (isCamOn.HasValue) updates.Add("\"IsCamOn\" = @cam");
            if (isMicOn.HasValue) updates.Add("\"IsMicOn\" = @mic");
            if (hasHandRaised.HasValue) updates.Add("\"HasHandRaised\" = @hand");

            if (updates.Count == 0) return false;

            var sql = $"UPDATE \"RoomMembers\" SET {string.Join(", ", updates)} WHERE \"RoomId\" = @rid AND \"UserId\" = @uid";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("rid", roomId);
            cmd.Parameters.AddWithValue("uid", userId);
            if (isCamOn.HasValue) cmd.Parameters.AddWithValue("cam", isCamOn.Value);
            if (isMicOn.HasValue) cmd.Parameters.AddWithValue("mic", isMicOn.Value);
            if (hasHandRaised.HasValue) cmd.Parameters.AddWithValue("hand", hasHandRaised.Value);

            int rows = await cmd.ExecuteNonQueryAsync();
            if (rows > 0)
            {
                // Notify SignalR
                await _hubContext.Clients.Group($"Room_{roomId}").SendAsync("MemberStatusUpdated", userId, isCamOn, isMicOn, hasHandRaised);
                return true;
            }
            return false;
        }

        public async Task<List<RoomCategory>> GetCategoriesAsync()
        {
            var list = new List<RoomCategory>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            
            var sql = "SELECT * FROM \"RoomCategories\" ORDER BY \"Order\"";
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RoomCategory
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    ParentId = reader.GetInt32(reader.GetOrdinal("ParentId")),
                    Order = reader.GetInt32(reader.GetOrdinal("Order"))
                });
            }
            return list;
        }

        private async Task<RoomMemberDto> GetRoomMemberDetailsAsync(NpgsqlConnection conn, int roomId, int userId)
        {
            var sql = @"
                SELECT rm.""UserId"", u.""Username"", 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as DisplayName,
                       p.""AvatarPath"",
                       rr.""Name"" as RoleName, rr.""Color"" as RoleColor,
                       rm.""IsCamOn"", rm.""IsMicOn"", rm.""HasHandRaised""
                FROM ""RoomMembers"" rm
                JOIN ""Users"" u ON rm.""UserId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                JOIN ""RoomRoles"" rr ON rm.""RoleId"" = rr.""Id""
                WHERE rm.""RoomId"" = @rid AND rm.""UserId"" = @uid";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("rid", roomId);
            cmd.Parameters.AddWithValue("uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new RoomMemberDto
                {
                    UserId = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    DisplayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(reader.GetString(2).ToLower()),
                    AvatarPath = reader.IsDBNull(3) ? null : reader.GetString(3),
                    RoleName = reader.GetString(4),
                    RoleColor = reader.GetString(5),
                    IsCamOn = reader.GetBoolean(6),
                    IsMicOn = reader.GetBoolean(7),
                    HasHandRaised = reader.GetBoolean(8)
                };
            }
            return null!;
        }
    }
}
