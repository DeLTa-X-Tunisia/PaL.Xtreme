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
        private readonly IHubContext<RoomHub> _roomHubContext;
        private readonly IHubContext<ChatHub> _chatHubContext; // Pour les notifications utilisateur
        private readonly IAccessControlService _accessControl;

        public RoomService(IConfiguration configuration, IHubContext<RoomHub> roomHubContext, IHubContext<ChatHub> chatHubContext, IAccessControlService accessControl)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string not found.");
            _roomHubContext = roomHubContext;
            _chatHubContext = chatHubContext;
            _accessControl = accessControl;
        }

        public async Task<RoomDto> CreateRoomAsync(int userId, CreateRoomDto dto)
        {
            // ══════════════════════════════════════════════════════════════════════
            // RÈGLE MÉTIER: Tous les nouveaux salons démarrent en Basic (Tier 0)
            // L'utilisateur devra acheter un abonnement Room pour upgrader
            // ══════════════════════════════════════════════════════════════════════
            const int BASIC_SUBSCRIPTION_LEVEL = 0;
            
            // 1. Force Basic level for new rooms (ignore dto.SubscriptionLevel)
            var roomLevel = (RoomSubscriptionLevel)BASIC_SUBSCRIPTION_LEVEL;
            
            // 2. Access Control Check (Basic level, no 18+ by default for new rooms)
            if (!await _accessControl.CanCreateRoomAsync(userId, roomLevel, false))
            {
                throw new UnauthorizedAccessException("Permission denied: Cannot create room.");
            }

            // 3. Enforce Max Users based on Basic Room Level
            var maxAllowed = _accessControl.GetMaxRoomCapacity(roomLevel);
            if (dto.MaxUsers > maxAllowed) dto.MaxUsers = maxAllowed;

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // 4. Insert Room with Basic level (0) - ignore dto.SubscriptionLevel
            var sql = @"
                INSERT INTO ""Rooms"" 
                (""Name"", ""Description"", ""CategoryId"", ""OwnerId"", ""MaxUsers"", ""MaxMics"", ""MaxCams"", ""IsPrivate"", ""Password"", ""Is18Plus"", ""SubscriptionLevel"")
                VALUES (@name, @desc, @cat, @owner, @max, @maxMics, @maxCams, @priv, @pass, FALSE, 0)
                RETURNING ""Id"", ""CreatedAt""";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("name", dto.Name);
            cmd.Parameters.AddWithValue("desc", dto.Description);
            cmd.Parameters.AddWithValue("cat", dto.CategoryId);
            cmd.Parameters.AddWithValue("owner", userId);
            cmd.Parameters.AddWithValue("max", dto.MaxUsers);
            cmd.Parameters.AddWithValue("maxMics", dto.MaxMics);
            cmd.Parameters.AddWithValue("maxCams", dto.MaxCams);
            cmd.Parameters.AddWithValue("priv", dto.IsPrivate);
            cmd.Parameters.AddWithValue("pass", (object?)dto.Password ?? DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var roomId = reader.GetInt32(0);
                
                // 5. Add Owner as RoomOwner (Level 1)
                await reader.CloseAsync();
                await AddMemberToRoomInternal(conn, roomId, userId, (int)RoomRoleLevel.Owner);
                
                // 6. Create Basic subscription entry for this room
                await CreateRoomSubscriptionInternal(conn, roomId, userId, BASIC_SUBSCRIPTION_LEVEL);

                return new RoomDto
                {
                    Id = roomId,
                    Name = dto.Name,
                    Description = dto.Description,
                    CategoryId = dto.CategoryId,
                    OwnerId = userId,
                    MaxUsers = dto.MaxUsers,
                    IsPrivate = dto.IsPrivate,
                    Is18Plus = false, // Basic rooms cannot be 18+
                    SubscriptionLevel = BASIC_SUBSCRIPTION_LEVEL,
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
            var checkMember = "SELECT \"RoleId\" FROM \"RoomMembers\" WHERE \"RoomId\" = @rid AND \"UserId\" = @uid";
            int? currentRole = null;
            using (var cmd = new NpgsqlCommand(checkMember, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                cmd.Parameters.AddWithValue("uid", userId);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    currentRole = (int)result;
                }
            }

            // 6. Determine Correct Role (Owner or Member)
            int targetRoleId = (int)RoomRoleLevel.Member;
            var checkOwner = "SELECT \"OwnerId\" FROM \"Rooms\" WHERE \"Id\" = @rid";
            using (var cmd = new NpgsqlCommand(checkOwner, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                var ownerId = (int?)await cmd.ExecuteScalarAsync();
                if (ownerId == userId) targetRoleId = (int)RoomRoleLevel.Owner;
            }

            if (currentRole.HasValue)
            {
                // Already joined. Check if role needs update (e.g. Owner was demoted or joined as member)
                if (currentRole.Value != targetRoleId && targetRoleId == (int)RoomRoleLevel.Owner)
                {
                    // Fix role to Owner
                    var updateSql = "UPDATE \"RoomMembers\" SET \"RoleId\" = @role WHERE \"RoomId\" = @rid AND \"UserId\" = @uid";
                    using (var cmd = new NpgsqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("role", targetRoleId);
                        cmd.Parameters.AddWithValue("rid", roomId);
                        cmd.Parameters.AddWithValue("uid", userId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return true; 
            }

            // 7. Add Member
            await AddMemberToRoomInternal(conn, roomId, userId, targetRoleId);
            
            // Get Member Details for Notification
            var memberDto = await GetRoomMemberDetailsAsync(conn, roomId, userId);

            // Notify SignalR
            await _roomHubContext.Clients.Group($"Room_{roomId}").SendAsync("UserJoined", memberDto);

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

        /// <summary>
        /// Creates a subscription entry for a room. All new rooms start with Basic (Tier 0).
        /// To upgrade, the owner must purchase a higher tier subscription.
        /// </summary>
        private async Task CreateRoomSubscriptionInternal(NpgsqlConnection conn, int roomId, int purchasedByUserId, int tierLevel)
        {
            // Get the Tier ID from RoomSubscriptionTiers based on tier level
            var getTierSql = @"SELECT ""Id"" FROM ""RoomSubscriptionTiers"" WHERE ""Tier"" = @tier";
            int tierId;
            using (var cmd = new NpgsqlCommand(getTierSql, conn))
            {
                cmd.Parameters.AddWithValue("tier", tierLevel);
                var result = await cmd.ExecuteScalarAsync();
                if (result == null)
                {
                    // Fallback to first tier if not found
                    tierId = 1;
                }
                else
                {
                    tierId = (int)result;
                }
            }

            // Insert subscription (Basic tier has no expiration - it's free)
            var sql = @"
                INSERT INTO ""RoomSubscriptions"" 
                (""RoomId"", ""TierId"", ""PurchasedBy"", ""StartedAt"", ""ExpiresAt"", ""IsActive"", ""AutoRenew"")
                VALUES (@roomId, @tierId, @purchasedBy, NOW(), NULL, TRUE, FALSE)
                ON CONFLICT (""RoomId"") DO UPDATE SET 
                    ""TierId"" = @tierId, 
                    ""UpdatedAt"" = NOW()";
            
            using var insertCmd = new NpgsqlCommand(sql, conn);
            insertCmd.Parameters.AddWithValue("roomId", roomId);
            insertCmd.Parameters.AddWithValue("tierId", tierId);
            insertCmd.Parameters.AddWithValue("purchasedBy", purchasedByUserId);
            await insertCmd.ExecuteNonQueryAsync();
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
            await _roomHubContext.Clients.Group($"Room_{roomId}").SendAsync("UserLeft", userId);
        }

        public async Task<List<RoomDto>> GetRoomsAsync(int userId, int? categoryId = null)
        {
            Console.WriteLine($"[RoomService] ========== GetRoomsAsync for userId={userId} ==========");
            
            var rooms = new List<RoomDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT r.*, c.""Name"" as CatName, 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as OwnerName,
                       (SELECT COUNT(*) FROM ""RoomMembers"" rm WHERE rm.""RoomId"" = r.""Id"") as UserCount,
                       ra.""Role"" as UserRole
                FROM ""Rooms"" r
                JOIN ""RoomCategories"" c ON r.""CategoryId"" = c.""Id""
                JOIN ""Users"" u ON r.""OwnerId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                LEFT JOIN ""RoomAdmins"" ra ON r.""Id"" = ra.""RoomId"" AND ra.""UserId"" = @uid
                WHERE (r.""IsActive"" = TRUE OR r.""OwnerId"" = @uid)";

            if (categoryId.HasValue)
            {
                sql += " AND r.\"CategoryId\" = @cat";
            }

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("uid", userId);
            if (categoryId.HasValue) cmd.Parameters.AddWithValue("cat", categoryId.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var userRole = reader.IsDBNull(reader.GetOrdinal("UserRole")) ? null : reader.GetString(reader.GetOrdinal("UserRole"));
                var roomId = reader.GetInt32(reader.GetOrdinal("Id"));
                var roomName = reader.GetString(reader.GetOrdinal("Name"));
                
                Console.WriteLine($"[RoomService]   Room {roomId} ({roomName}) - UserRole for user {userId}: {userRole ?? "null"}");
                
                rooms.Add(new RoomDto
                {
                    Id = roomId,
                    Name = roomName,
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString(reader.GetOrdinal("Description")),
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CatName")),
                    OwnerId = reader.GetInt32(reader.GetOrdinal("OwnerId")),
                    OwnerName = reader.GetString(reader.GetOrdinal("OwnerName")),
                    MaxUsers = reader.GetInt32(reader.GetOrdinal("MaxUsers")),
                    IsPrivate = reader.GetBoolean(reader.GetOrdinal("IsPrivate")),
                    Is18Plus = reader.IsDBNull(reader.GetOrdinal("Is18Plus")) ? false : reader.GetBoolean(reader.GetOrdinal("Is18Plus")),
                    SubscriptionLevel = reader.GetInt32(reader.GetOrdinal("SubscriptionLevel")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    UserCount = (int)reader.GetInt64(reader.GetOrdinal("UserCount")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    UserRole = userRole
                });
            }
            
            Console.WriteLine($"[RoomService] Total rooms returned: {rooms.Count}");
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
                       rm.""IsMuted"", rm.""HasHandRaised"", rm.""IsCamOn"", rm.""IsMicOn"", p.""Gender""
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
                    IsMicOn = reader.GetBoolean(11),
                    Gender = reader.IsDBNull(12) ? "Unknown" : reader.GetString(12)
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

            // Get User Info for DTO (including role name)
            var userSql = @"
                SELECT u.""Username"", 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as DisplayName,
                       p.""AvatarPath"",
                       rr.""Color"",
                       COALESCE(rr.""Name"", 'Membre') as RoleName
                FROM ""RoomMembers"" rm
                JOIN ""Users"" u ON rm.""UserId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                JOIN ""RoomRoles"" rr ON rm.""RoleId"" = rr.""Id""
                WHERE rm.""RoomId"" = @rid AND rm.""UserId"" = @uid";

            string username = "", displayName = "", avatarPath = "", roleColor = "#000000", roleName = "Membre";
            using (var cmd = new NpgsqlCommand(userSql, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                cmd.Parameters.AddWithValue("uid", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    username = reader.GetString(0);
                    displayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(reader.GetString(1).ToLower());
                    avatarPath = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    roleColor = reader.IsDBNull(3) ? "#000000" : reader.GetString(3);
                    roleName = reader.GetString(4);
                }
            }

            var dto = new RoomMessageDto
            {
                Id = msgId,
                RoomId = roomId,
                UserId = userId,
                Username = username,
                DisplayName = displayName,
                AvatarPath = avatarPath,
                RoleName = roleName,
                RoleColor = roleColor,
                Content = content,
                MessageType = type,
                Timestamp = timestamp,
                AttachmentUrl = attachmentUrl
            };

            // Broadcast via SignalR
            await _roomHubContext.Clients.Group($"Room_{roomId}").SendAsync("ReceiveMessage", dto);

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
                       p.""AvatarPath"",
                       rr.""Color"",
                       COALESCE(rr.""Name"", 'Membre') as RoleName
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
                    AvatarPath = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    RoleColor = reader.IsDBNull(9) ? "#000000" : reader.GetString(9),
                    RoleName = reader.GetString(10)
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
                await _roomHubContext.Clients.Group($"Room_{roomId}").SendAsync("MemberStatusUpdated", userId, isCamOn, isMicOn, hasHandRaised);
                return true;
            }
            return false;
        }

        public async Task<List<RoomCategoryDto>> GetCategoriesAsync()
        {
            var list = new List<RoomCategoryDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            
            // Get categories with subcategory count
            var sql = @"
                SELECT c.""Id"", c.""Name"", c.""Description"", c.""Icon"", c.""Color"",
                       (SELECT COUNT(*) FROM ""RoomSubCategories"" s WHERE s.""CategoryId"" = c.""Id"" AND s.""IsActive"" = TRUE) as SubCount
                FROM ""RoomCategories"" c
                WHERE c.""IsActive"" = TRUE AND c.""IsVisible"" = TRUE
                ORDER BY c.""Order"", c.""Name""";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RoomCategoryDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Icon = reader.IsDBNull(3) ? "chat" : reader.GetString(3),
                    Color = reader.IsDBNull(4) ? "#3498DB" : reader.GetString(4),
                    SubCategoryCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                });
            }
            return list;
        }

        public async Task<List<RoomSubCategoryDto>> GetSubCategoriesAsync(int categoryId)
        {
            var list = new List<RoomSubCategoryDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            
            var sql = @"
                SELECT ""Id"", ""Name"", ""Description"", ""Icon"", ""Color""
                FROM ""RoomSubCategories""
                WHERE ""CategoryId"" = @catId AND ""IsActive"" = TRUE AND ""IsVisible"" = TRUE
                ORDER BY ""DisplayOrder"", ""Name""";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("catId", categoryId);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RoomSubCategoryDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Icon = reader.IsDBNull(3) ? "chat" : reader.GetString(3),
                    Color = reader.IsDBNull(4) ? "#6C757D" : reader.GetString(4)
                });
            }
            return list;
        }

        public async Task<List<RoomSubscriptionTierDto>> GetRoomSubscriptionTiersAsync()
        {
            var list = new List<RoomSubscriptionTierDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            
            var sql = @"
                SELECT ""Tier"", ""Name"", ""Description"", ""Color"", ""Icon"",
                       ""MaxUsers"", ""MaxMic"", ""MaxCam"", ""AlwaysOnline"",
                       ""MonthlyPriceCents"", ""YearlyPriceCents""
                FROM ""RoomSubscriptionTiers""
                WHERE ""IsAvailable"" = TRUE
                ORDER BY ""Tier""";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RoomSubscriptionTierDto
                {
                    Tier = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Color = reader.IsDBNull(3) ? "#95A5A6" : reader.GetString(3),
                    Icon = reader.IsDBNull(4) ? "home" : reader.GetString(4),
                    MaxUsers = reader.GetInt32(5),
                    MaxMic = reader.GetInt32(6),
                    MaxCam = reader.GetInt32(7),
                    AlwaysOnline = reader.GetBoolean(8),
                    MonthlyPriceCents = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                    YearlyPriceCents = reader.IsDBNull(10) ? 0 : reader.GetInt32(10)
                });
            }
            return list;
        }

        public async Task<List<MyRoomDto>> GetMyRoomsAsync(int userId)
        {
            var list = new List<MyRoomDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            
            var sql = @"
                SELECT r.""Id"", r.""Name"", c.""Name"" as CategoryName,
                       COALESCE(rst.""Name"", 'Basic') as TierName,
                       COALESCE(rst.""Color"", '#95A5A6') as TierColor,
                       (SELECT COUNT(*) FROM ""RoomMembers"" WHERE ""RoomId"" = r.""Id"") as UserCount,
                       r.""MaxUsers"", r.""IsActive"", r.""CreatedAt""
                FROM ""Rooms"" r
                JOIN ""RoomCategories"" c ON r.""CategoryId"" = c.""Id""
                LEFT JOIN ""RoomSubscriptions"" rs ON r.""Id"" = rs.""RoomId""
                LEFT JOIN ""RoomSubscriptionTiers"" rst ON rs.""TierId"" = rst.""Id""
                WHERE r.""OwnerId"" = @userId
                ORDER BY r.""CreatedAt"" DESC";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("userId", userId);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new MyRoomDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    CategoryName = reader.GetString(2),
                    TierName = reader.GetString(3),
                    TierColor = reader.GetString(4),
                    UserCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    MaxUsers = reader.GetInt32(6),
                    IsActive = reader.GetBoolean(7),
                    CreatedAt = reader.GetDateTime(8)
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
                       rm.""IsCamOn"", rm.""IsMicOn"", rm.""HasHandRaised"", p.""Gender""
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
                    HasHandRaised = reader.GetBoolean(8),
                    Gender = reader.IsDBNull(9) ? "Unknown" : reader.GetString(9)
                };
            }
            return null!;
        }

        public async Task DeleteRoomAsync(int userId, int roomId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            
            var checkSql = "SELECT \"OwnerId\" FROM \"Rooms\" WHERE \"Id\" = @rid";
            using (var cmd = new NpgsqlCommand(checkSql, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                var ownerId = (int?)await cmd.ExecuteScalarAsync();
                if (ownerId == null) throw new Exception("Room not found");
                if (ownerId != userId) throw new UnauthorizedAccessException("Not owner");
            }

            var sql = "DELETE FROM \"Rooms\" WHERE \"Id\" = @rid";
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<RoomDto> UpdateRoomAsync(int userId, int roomId, CreateRoomDto dto)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Check Ownership
            var checkSql = "SELECT \"OwnerId\" FROM \"Rooms\" WHERE \"Id\" = @rid";
            using (var cmd = new NpgsqlCommand(checkSql, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                var ownerId = (int?)await cmd.ExecuteScalarAsync();
                if (ownerId == null) throw new Exception("Room not found");
                if (ownerId != userId) throw new UnauthorizedAccessException("Not owner");
            }

            var sql = @"
                UPDATE ""Rooms"" 
                SET ""Name"" = @name, ""Description"" = @desc, ""CategoryId"" = @cat, 
                    ""MaxUsers"" = @max, ""IsPrivate"" = @priv, ""Password"" = @pass, 
                    ""Is18Plus"" = @adult, ""SubscriptionLevel"" = @sub
                WHERE ""Id"" = @rid
                RETURNING ""Id"", ""CreatedAt"", ""OwnerId""";

            using var updateCmd = new NpgsqlCommand(sql, conn);
            updateCmd.Parameters.AddWithValue("name", dto.Name);
            updateCmd.Parameters.AddWithValue("desc", dto.Description);
            updateCmd.Parameters.AddWithValue("cat", dto.CategoryId);
            updateCmd.Parameters.AddWithValue("max", dto.MaxUsers);
            updateCmd.Parameters.AddWithValue("priv", dto.IsPrivate);
            updateCmd.Parameters.AddWithValue("pass", (object?)dto.Password ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("adult", dto.Is18Plus);
            updateCmd.Parameters.AddWithValue("sub", dto.SubscriptionLevel);
            updateCmd.Parameters.AddWithValue("rid", roomId);

            using var reader = await updateCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new RoomDto
                {
                    Id = reader.GetInt32(0),
                    Name = dto.Name,
                    Description = dto.Description,
                    CategoryId = dto.CategoryId,
                    OwnerId = reader.GetInt32(2),
                    MaxUsers = dto.MaxUsers,
                    IsPrivate = dto.IsPrivate,
                    Is18Plus = dto.Is18Plus,
                    SubscriptionLevel = dto.SubscriptionLevel,
                    CreatedAt = reader.GetDateTime(1)
                };
            }
            throw new Exception("Failed to update room");
        }

        public async Task<bool> ToggleRoomVisibilityAsync(int userId, int roomId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Check Ownership
            var checkSql = "SELECT \"OwnerId\", \"IsActive\" FROM \"Rooms\" WHERE \"Id\" = @rid";
            bool currentStatus = false;
            using (var cmd = new NpgsqlCommand(checkSql, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var ownerId = reader.GetInt32(0);
                    if (ownerId != userId) throw new UnauthorizedAccessException("Not owner");
                    currentStatus = reader.GetBoolean(1);
                }
                else throw new Exception("Room not found");
            }

            var newStatus = !currentStatus;
            var sql = "UPDATE \"Rooms\" SET \"IsActive\" = @status WHERE \"Id\" = @rid";
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("status", newStatus);
                cmd.Parameters.AddWithValue("rid", roomId);
                await cmd.ExecuteNonQueryAsync();
            }
            return newStatus;
        }

        /// <summary>
        /// Upgrades a room's subscription tier. Only the room owner can upgrade.
        /// This method should be called after payment verification.
        /// </summary>
        /// <param name="userId">The user requesting the upgrade (must be owner)</param>
        /// <param name="roomId">The room to upgrade</param>
        /// <param name="newTierLevel">The new tier level (1-9)</param>
        /// <param name="transactionId">Payment transaction ID for records</param>
        /// <param name="paymentMethod">Payment method used</param>
        /// <returns>True if upgrade successful</returns>
        public async Task<bool> UpgradeRoomSubscriptionAsync(int userId, int roomId, int newTierLevel, string? transactionId = null, string? paymentMethod = null)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // 1. Verify user is the room owner
            var ownerCheck = @"SELECT ""OwnerId"" FROM ""Rooms"" WHERE ""Id"" = @rid";
            using (var cmd = new NpgsqlCommand(ownerCheck, conn))
            {
                cmd.Parameters.AddWithValue("rid", roomId);
                var result = await cmd.ExecuteScalarAsync();
                if (result == null) throw new Exception("Room not found");
                if ((int)result != userId) throw new UnauthorizedAccessException("Only room owner can upgrade subscription");
            }

            // 2. Get the new tier ID
            var getTierSql = @"SELECT ""Id"", ""MaxUsers"", ""CanBe18Plus"" FROM ""RoomSubscriptionTiers"" WHERE ""Tier"" = @tier";
            int tierId;
            int newMaxUsers;
            bool canBe18Plus;
            using (var cmd = new NpgsqlCommand(getTierSql, conn))
            {
                cmd.Parameters.AddWithValue("tier", newTierLevel);
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new Exception("Invalid subscription tier");
                tierId = reader.GetInt32(0);
                newMaxUsers = reader.GetInt32(1);
                canBe18Plus = reader.GetBoolean(2);
            }

            // 3. Update room subscription
            var updateSubSql = @"
                UPDATE ""RoomSubscriptions"" 
                SET ""TierId"" = @tierId, 
                    ""PurchasedBy"" = @userId,
                    ""StartedAt"" = NOW(),
                    ""ExpiresAt"" = NOW() + INTERVAL '1 month',
                    ""TransactionId"" = @txn,
                    ""PaymentMethod"" = @payment,
                    ""UpdatedAt"" = NOW()
                WHERE ""RoomId"" = @roomId";
            
            using (var cmd = new NpgsqlCommand(updateSubSql, conn))
            {
                cmd.Parameters.AddWithValue("tierId", tierId);
                cmd.Parameters.AddWithValue("userId", userId);
                cmd.Parameters.AddWithValue("roomId", roomId);
                cmd.Parameters.AddWithValue("txn", (object?)transactionId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("payment", (object?)paymentMethod ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            // 4. Update room's SubscriptionLevel
            var updateRoomSql = @"UPDATE ""Rooms"" SET ""SubscriptionLevel"" = @level WHERE ""Id"" = @rid";
            using (var cmd = new NpgsqlCommand(updateRoomSql, conn))
            {
                cmd.Parameters.AddWithValue("level", newTierLevel);
                cmd.Parameters.AddWithValue("rid", roomId);
                await cmd.ExecuteNonQueryAsync();
            }

            return true;
        }

        /// <summary>
        /// Gets the current subscription tier info for a room.
        /// </summary>
        public async Task<RoomSubscriptionInfoDto?> GetRoomSubscriptionAsync(int roomId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT rs.""Id"", rst.""Tier"", rst.""Name"", rst.""Color"", rst.""Icon"",
                       rst.""MaxUsers"", rst.""MaxMic"", rst.""MaxCam"", rst.""AlwaysOnline"",
                       rs.""StartedAt"", rs.""ExpiresAt"", rs.""IsActive""
                FROM ""RoomSubscriptions"" rs
                JOIN ""RoomSubscriptionTiers"" rst ON rs.""TierId"" = rst.""Id""
                WHERE rs.""RoomId"" = @roomId";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new RoomSubscriptionInfoDto
                {
                    TierLevel = reader.GetInt32(1),
                    TierName = reader.GetString(2),
                    Color = reader.IsDBNull(3) ? "#95A5A6" : reader.GetString(3),
                    Icon = reader.IsDBNull(4) ? "home" : reader.GetString(4),
                    MaxUsers = reader.GetInt32(5),
                    MaxMic = reader.GetInt32(6),
                    MaxCam = reader.GetInt32(7),
                    AlwaysOnline = reader.GetBoolean(8),
                    StartedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    ExpiresAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                    IsActive = reader.GetBoolean(11)
                };
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════════════════
        // ROOM ADMINS MANAGEMENT (Simplified)
        // ═══════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Récupère les admins/modérateurs d'un salon
        /// </summary>
        public async Task<List<RoomRoleInfoDto>> GetRoomRolesAsync(int requesterId, int roomId)
        {
            Console.WriteLine($"[RoomService] ========== GetRoomRolesAsync for room {roomId} ==========");
            
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                Console.WriteLine($"[RoomService] Database connection opened");

                var sql = @"
                    SELECT ra.""UserId"", u.""Username"", 
                           COALESCE(up.""FirstName"" || ' ' || up.""LastName"", u.""Username"") as DisplayName,
                           up.""AvatarPath"", ra.""Role""
                    FROM ""RoomAdmins"" ra
                    JOIN ""Users"" u ON ra.""UserId"" = u.""Id""
                    LEFT JOIN ""UserProfiles"" up ON u.""Id"" = up.""UserId""
                    WHERE ra.""RoomId"" = @roomId";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("roomId", roomId);
                Console.WriteLine($"[RoomService] Executing SQL query...");

                var roles = new List<RoomRoleInfoDto>();
                using var reader = await cmd.ExecuteReaderAsync();
                Console.WriteLine($"[RoomService] Query executed, reading results...");
                
                while (await reader.ReadAsync())
                {
                    var role = new RoomRoleInfoDto
                    {
                        UserId = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        DisplayName = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),
                        AvatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Role = reader.GetString(4)
                    };
                    roles.Add(role);
                    Console.WriteLine($"[RoomService]   -> Found: UserId={role.UserId}, Username={role.Username}, Role={role.Role}");
                }
                
                Console.WriteLine($"[RoomService] Total roles found: {roles.Count}");
                return roles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomService] ERROR in GetRoomRolesAsync: {ex.Message}");
                Console.WriteLine($"[RoomService] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Attribue directement un rôle à un utilisateur (SuperAdmin, Admin, Moderator)
        /// </summary>
        public async Task AssignRoleAsync(int ownerId, int roomId, int targetUserId, string role)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier que le demandeur est le propriétaire du salon
            var checkSql = @"SELECT ""OwnerId"", ""Name"" FROM ""Rooms"" WHERE ""Id"" = @roomId";
            using var checkCmd = new NpgsqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("roomId", roomId);
            using var checkReader = await checkCmd.ExecuteReaderAsync();
            
            if (!await checkReader.ReadAsync())
                throw new Exception("Room not found");
            
            var roomOwnerId = checkReader.GetInt32(0);
            await checkReader.CloseAsync();
            
            if (roomOwnerId != ownerId)
                throw new UnauthorizedAccessException("Only room owner can assign roles");

            // Vérifier que le role est valide
            if (!new[] { "SuperAdmin", "Admin", "Moderator" }.Contains(role))
                throw new Exception("Invalid role");

            // Vérifier que l'utilisateur cible existe et est ami
            var friendSql = @"
                SELECT 1 FROM ""Friendships"" 
                WHERE ((""RequesterId"" = @ownerId AND ""ReceiverId"" = @targetId)
                   OR (""RequesterId"" = @targetId AND ""ReceiverId"" = @ownerId))
                   AND ""Status"" = 1";
            using var friendCmd = new NpgsqlCommand(friendSql, conn);
            friendCmd.Parameters.AddWithValue("ownerId", ownerId);
            friendCmd.Parameters.AddWithValue("targetId", targetUserId);
            var isFriend = await friendCmd.ExecuteScalarAsync();
            
            if (isFriend == null)
                throw new Exception("Target user is not a friend");

            // Insérer ou mettre à jour le rôle (UPSERT)
            var insertSql = @"
                INSERT INTO ""RoomAdmins"" (""RoomId"", ""UserId"", ""Role"", ""AssignedBy"", ""AssignedAt"")
                VALUES (@roomId, @userId, @role, @assignedBy, @now)
                ON CONFLICT (""RoomId"", ""UserId"") 
                DO UPDATE SET ""Role"" = @role, ""AssignedBy"" = @assignedBy, ""AssignedAt"" = @now";

            using var insertCmd = new NpgsqlCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("roomId", roomId);
            insertCmd.Parameters.AddWithValue("userId", targetUserId);
            insertCmd.Parameters.AddWithValue("role", role);
            insertCmd.Parameters.AddWithValue("assignedBy", ownerId);
            insertCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
            await insertCmd.ExecuteNonQueryAsync();

            Console.WriteLine($"[RoomService] Role '{role}' assigned to user {targetUserId} in room {roomId}");

            // ═══════════════════════════════════════════════════════════════════════
            // NOTIFICATION SIGNALR: Informer l'utilisateur qu'il a reçu un rôle
            // ═══════════════════════════════════════════════════════════════════════
            try
            {
                // Récupérer le nom du salon ET le username de l'utilisateur cible
                var infoSql = @"
                    SELECT r.""Name"", u.""Username"" 
                    FROM ""Rooms"" r, ""Users"" u 
                    WHERE r.""Id"" = @roomId AND u.""Id"" = @userId";
                using var infoCmd = new NpgsqlCommand(infoSql, conn);
                infoCmd.Parameters.AddWithValue("roomId", roomId);
                infoCmd.Parameters.AddWithValue("userId", targetUserId);
                
                string roomName = "Salon";
                string targetUsername = "";
                using var infoReader = await infoCmd.ExecuteReaderAsync();
                if (await infoReader.ReadAsync())
                {
                    roomName = infoReader.GetString(0);
                    targetUsername = infoReader.GetString(1);
                }
                await infoReader.CloseAsync();

                // SignalR utilise le USERNAME comme UserIdentifier, pas l'ID
                await _chatHubContext.Clients.User(targetUsername)
                    .SendAsync("RoleAssigned", roomId, roomName, role);
                Console.WriteLine($"[RoomService] SignalR RoleAssigned notification sent to '{targetUsername}' for room {roomId} with role {role}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomService] Warning: Failed to send RoleAssigned notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Retire le rôle d'un utilisateur dans un salon
        /// </summary>
        public async Task RemoveRoomRoleAsync(int ownerId, int roomId, int targetUserId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier que le demandeur est le propriétaire du salon
            var checkSql = @"SELECT ""OwnerId"", ""Name"" FROM ""Rooms"" WHERE ""Id"" = @roomId";
            using var checkCmd = new NpgsqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("roomId", roomId);
            
            int actualOwnerId = 0;
            string roomName = "";
            
            using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                actualOwnerId = reader.GetInt32(0);
                roomName = reader.GetString(1);
            }
            await reader.CloseAsync();
            
            if (actualOwnerId != ownerId)
                throw new UnauthorizedAccessException("Only room owner can remove roles");

            // Supprimer le rôle
            var deleteSql = @"DELETE FROM ""RoomAdmins"" WHERE ""RoomId"" = @roomId AND ""UserId"" = @userId";
            using var deleteCmd = new NpgsqlCommand(deleteSql, conn);
            deleteCmd.Parameters.AddWithValue("roomId", roomId);
            deleteCmd.Parameters.AddWithValue("userId", targetUserId);
            await deleteCmd.ExecuteNonQueryAsync();

            Console.WriteLine($"[RoomService] Role removed for user {targetUserId} in room {roomId}");

            // ═══════════════════════════════════════════════════════════════════════
            // NOTIFICATION SIGNALR: Informer l'utilisateur que son rôle a été retiré
            // ═══════════════════════════════════════════════════════════════════════
            try
            {
                // Récupérer le username de l'utilisateur cible
                var usernameSql = @"SELECT ""Username"" FROM ""Users"" WHERE ""Id"" = @userId";
                using var usernameCmd = new NpgsqlCommand(usernameSql, conn);
                usernameCmd.Parameters.AddWithValue("userId", targetUserId);
                var targetUsername = await usernameCmd.ExecuteScalarAsync() as string ?? "";

                // SignalR utilise le USERNAME comme UserIdentifier, pas l'ID
                await _chatHubContext.Clients.User(targetUsername)
                    .SendAsync("RoleRemoved", roomId, roomName);
                Console.WriteLine($"[RoomService] SignalR RoleRemoved notification sent to '{targetUsername}' for room {roomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomService] Warning: Failed to send RoleRemoved notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Vérifie si un utilisateur a un rôle spécifique dans un salon
        /// </summary>
        public async Task<string?> GetUserRoleInRoomAsync(int userId, int roomId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT ""Role"" FROM ""RoomAdmins"" WHERE ""RoomId"" = @roomId AND ""UserId"" = @userId";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            cmd.Parameters.AddWithValue("userId", userId);
            
            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }
    }
}