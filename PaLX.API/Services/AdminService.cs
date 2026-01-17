using Npgsql;
using PaLX.API.DTOs;

namespace PaLX.API.Services
{
    // Cache key constants for AdminService
    public static class AdminCacheKeys
    {
        public const string Roles = "admin:roles";
        public const string Categories = "admin:categories";
        public const string SubCategories = "admin:subcategories";
        public static string SubCategoriesByCategory(int categoryId) => $"admin:subcategories:cat:{categoryId}";
    }

    public interface IAdminService
    {
        // Dashboard
        Task<AdminDashboardStats> GetDashboardStatsAsync();
        
        // Users
        Task<PaginatedResult<AdminUserDto>> GetUsersAsync(int page, int pageSize, string? search, int? roleLevel, bool? isOnline, bool? isBanned);
        Task<AdminUserDetailDto?> GetUserByIdAsync(int id);
        Task<string?> GetUsernameByIdAsync(int id);
        Task<ServiceResult> BanUserAsync(int userId, string reason, int? durationDays, int adminId, int adminRoleLevel);
        Task<ServiceResult> UnbanUserAsync(int userId, int adminId);
        Task<ServiceResult> ChangeUserRoleAsync(int userId, int newRoleLevel, int adminId);
        Task<ServiceResult> WarnUserAsync(int userId, string reason, int adminId);
        
        // Roles
        Task<List<AdminRoleDto>> GetRolesAsync();
        
        // Broadcast / Annonces globales
        Task SaveBroadcastAsync(int sentByUserId, string type, string title, string message);
        Task<PaginatedResult<BroadcastHistoryDto>> GetBroadcastHistoryAsync(int page, int pageSize);
        Task<BroadcastHistoryDto?> GetBroadcastByIdAsync(int id);
        Task<ServiceResult> UpdateBroadcastAsync(int id, string type, string title, string message);
        Task<ServiceResult> DeleteBroadcastAsync(int id);
        
        // Categories & SubCategories
        Task<List<AdminRoomCategoryDto>> GetCategoriesAsync();
        Task<AdminRoomCategoryDto?> GetCategoryByIdAsync(int id);
        Task<ServiceResult> CreateCategoryAsync(CreateCategoryDto dto, int adminId);
        Task<ServiceResult> UpdateCategoryAsync(int id, UpdateCategoryDto dto, int adminId);
        Task<ServiceResult> DeleteCategoryAsync(int id, int adminId);
        Task<List<AdminRoomSubCategoryDto>> GetSubCategoriesAsync(int? categoryId = null);
        Task<AdminRoomSubCategoryDto?> GetSubCategoryByIdAsync(int id);
        Task<ServiceResult> CreateSubCategoryAsync(CreateSubCategoryDto dto, int adminId);
        Task<ServiceResult> UpdateSubCategoryAsync(int id, UpdateSubCategoryDto dto, int adminId);
        Task<ServiceResult> DeleteSubCategoryAsync(int id, int adminId);
        
        // Rooms
        Task<PaginatedResult<AdminRoomDto>> GetRoomsAsync(int page, int pageSize, string? search, bool? isActive);
        Task<AdminRoomDto?> GetRoomByIdAsync(int id);
        Task<ServiceResult> CloseRoomAsync(int roomId, string? reason, int adminId);
        Task<ServiceResult> DeleteRoomAsync(int roomId, int adminId);
        
        // Reports
        Task<PaginatedResult<AdminReportDto>> GetReportsAsync(int page, int pageSize, string? status);
        Task<ServiceResult> ResolveReportAsync(int reportId, string resolution, string? action, int adminId);
        Task<ServiceResult> DismissReportAsync(int reportId, string? reason, int adminId);
        
        // Logs
        Task<PaginatedResult<AdminAuditLogDto>> GetAuditLogsAsync(int page, int pageSize);
        Task LogActionAsync(int adminId, string action, string targetType, int? targetId, string? details);
        
        // System
        Task SetMaintenanceModeAsync(bool enabled, string? message);
    }

    public class AdminService : IAdminService
    {
        private readonly string _connectionString;
        private readonly ICacheService _cache;
        private static bool _maintenanceMode = false;
        private static string? _maintenanceMessage;

        public AdminService(IConfiguration configuration, ICacheService cacheService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found");
            _cache = cacheService;
        }

        // ============================================
        // Dashboard
        // ============================================

        public async Task<AdminDashboardStats> GetDashboardStatsAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var stats = new AdminDashboardStats();

            // Total users
            using (var cmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""Users""", conn))
            {
                stats.TotalUsers = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Online users (sessions actives)
            using (var cmd = new NpgsqlCommand(@"SELECT COUNT(DISTINCT ""UserId"") FROM ""UserSessions"" WHERE ""DéconnectéLe"" IS NULL", conn))
            {
                stats.OnlineUsers = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Active rooms
            using (var cmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""Rooms"" WHERE ""IsActive"" = true", conn))
            {
                stats.ActiveRooms = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Total messages (estimation basée sur les historiques)
            using (var cmd = new NpgsqlCommand(@"SELECT COALESCE(COUNT(*), 0) FROM ""PrivateMessages""", conn))
            {
                stats.TotalMessages = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // New users today
            using (var cmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""Users"" WHERE ""CreatedAt""::date = CURRENT_DATE", conn))
            {
                stats.NewUsersToday = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Pending reports
            using (var cmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""Reports"" WHERE ""Status"" = 'Pending'", conn))
            {
                stats.PendingReports = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Free users (Member - TierId = 1, qui correspond à SubscriptionTiers.Id = 1)
            using (var cmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""UserSubscriptions"" WHERE ""TierId"" = 1 AND ""IsActive"" = true", conn))
            {
                stats.FreeUsers = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Premium users (Deluxe à Gold - TierId 2 à 7)
            using (var cmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""UserSubscriptions"" WHERE ""TierId"" >= 2 AND ""TierId"" <= 7 AND ""IsActive"" = true AND (""ExpiresAt"" IS NULL OR ""ExpiresAt"" > NOW())", conn))
            {
                stats.PremiumUsers = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // VIP users (Platinum à Legend - TierId 8 à 10)
            using (var cmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""UserSubscriptions"" WHERE ""TierId"" >= 8 AND ""IsActive"" = true AND (""ExpiresAt"" IS NULL OR ""ExpiresAt"" > NOW())", conn))
            {
                stats.VipUsers = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Compléter les utilisateurs sans abonnement
            var usersWithoutSub = stats.TotalUsers - stats.FreeUsers - stats.PremiumUsers - stats.VipUsers;
            if (usersWithoutSub > 0) stats.FreeUsers += usersWithoutSub;

            // Server uptime (simulé basé sur le temps depuis le dernier redémarrage)
            stats.ServerUptime = 99.9;

            // Subscription distribution (répartition détaillée par tier)
            var subscriptionDistSql = @"
                SELECT st.""Id"", st.""Name"", st.""Color"", COUNT(us.""Id"") as count
                FROM ""SubscriptionTiers"" st
                LEFT JOIN ""UserSubscriptions"" us ON st.""Id"" = us.""TierId"" AND us.""IsActive"" = true
                GROUP BY st.""Id"", st.""Name"", st.""Color""
                ORDER BY st.""Id""";

            using (var cmd = new NpgsqlCommand(subscriptionDistSql, conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    stats.SubscriptionDistribution.Add(new SubscriptionTierStatsDto
                    {
                        TierId = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Color = reader.IsDBNull(2) ? "#808080" : reader.GetString(2),
                        Count = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetInt64(3))
                    });
                }
            }

            // Weekly activity (connexions par jour sur les 7 derniers jours)
            var frenchDays = new[] { "Dim", "Lun", "Mar", "Mer", "Jeu", "Ven", "Sam" };
            var weeklyActivitySql = @"
                SELECT 
                    d.day::date as date,
                    COUNT(DISTINCT s.""UserId"") as active_users,
                    COUNT(s.""Id"") as connections,
                    COALESCE((SELECT COUNT(*) FROM ""PrivateMessages"" pm WHERE pm.""SentAt""::date = d.day::date), 0) as messages
                FROM generate_series(CURRENT_DATE - INTERVAL '6 days', CURRENT_DATE, INTERVAL '1 day') as d(day)
                LEFT JOIN ""UserSessions"" s ON s.""ConnectéLe""::date = d.day::date
                GROUP BY d.day
                ORDER BY d.day";

            using (var cmd = new NpgsqlCommand(weeklyActivitySql, conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var date = reader.GetDateTime(0);
                    stats.WeeklyActivity.Add(new DailyActivityDto
                    {
                        Date = date,
                        Day = frenchDays[(int)date.DayOfWeek],
                        ActiveUsers = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        Connections = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetInt64(2)),
                        Messages = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                    });
                }
            }

            // Recent activities (derniers événements)
            var recentActivities = new List<RecentActivityDto>();

            // Derniers utilisateurs inscrits
            var recentUsersSql = @"
                SELECT u.""Id"", u.""Username"", u.""CreatedAt"", p.""FirstName"", p.""LastName""
                FROM ""Users"" u
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                ORDER BY u.""CreatedAt"" DESC
                LIMIT 3";

            using (var cmd = new NpgsqlCommand(recentUsersSql, conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var firstName = reader.IsDBNull(3) ? null : reader.GetString(3);
                    var lastName = reader.IsDBNull(4) ? null : reader.GetString(4);
                    var displayName = !string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName)
                        ? $"{lastName ?? ""} {firstName ?? ""}".Trim()
                        : reader.GetString(1);

                    recentActivities.Add(new RecentActivityDto
                    {
                        Type = "user_registered",
                        Title = "Nouvel utilisateur inscrit",
                        Description = $"par {displayName}",
                        Username = reader.GetString(1),
                        DisplayName = displayName,
                        CreatedAt = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2)
                    });
                }
            }

            // Derniers signalements
            var recentReportsSql = @"
                SELECT r.""Id"", r.""Reason"", r.""CreatedAt"", u.""Username"", p.""FirstName"", p.""LastName""
                FROM ""Reports"" r
                LEFT JOIN ""Users"" u ON r.""ReporterId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                ORDER BY r.""CreatedAt"" DESC
                LIMIT 3";

            using (var cmd = new NpgsqlCommand(recentReportsSql, conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var firstName = reader.IsDBNull(4) ? null : reader.GetString(4);
                    var lastName = reader.IsDBNull(5) ? null : reader.GetString(5);
                    var username = reader.IsDBNull(3) ? "Inconnu" : reader.GetString(3);
                    var displayName = !string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName)
                        ? $"{lastName ?? ""} {firstName ?? ""}".Trim()
                        : username;

                    recentActivities.Add(new RecentActivityDto
                    {
                        Type = "report_created",
                        Title = "Signalement créé",
                        Description = $"par {displayName}",
                        Username = username,
                        DisplayName = displayName,
                        CreatedAt = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2)
                    });
                }
            }

            // Derniers bans
            var recentBansSql = @"
                SELECT b.""Id"", b.""Reason"", b.""BannedAt"", u.""Username"", p.""FirstName"", p.""LastName""
                FROM ""BannedUsers"" b
                LEFT JOIN ""Users"" u ON b.""UserId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                WHERE b.""IsActive"" = true
                ORDER BY b.""BannedAt"" DESC
                LIMIT 2";

            using (var cmd = new NpgsqlCommand(recentBansSql, conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var firstName = reader.IsDBNull(4) ? null : reader.GetString(4);
                    var lastName = reader.IsDBNull(5) ? null : reader.GetString(5);
                    var username = reader.IsDBNull(3) ? "Inconnu" : reader.GetString(3);
                    var displayName = !string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName)
                        ? $"{lastName ?? ""} {firstName ?? ""}".Trim()
                        : username;

                    recentActivities.Add(new RecentActivityDto
                    {
                        Type = "user_banned",
                        Title = "Utilisateur banni",
                        Description = displayName,
                        Username = username,
                        DisplayName = displayName,
                        CreatedAt = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2)
                    });
                }
            }

            // Trier par date décroissante et limiter à 10
            stats.RecentActivities = recentActivities
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .ToList();

            return stats;
        }

        // ============================================
        // Roles
        // ============================================

        public async Task<List<AdminRoleDto>> GetRolesAsync()
        {
            return await _cache.GetOrSetAsync(
                AdminCacheKeys.Roles,
                async () => await FetchRolesFromDatabaseAsync(),
                CacheOptions.MediumTerm // 15 minutes - roles don't change often
            );
        }

        private async Task<List<AdminRoleDto>> FetchRolesFromDatabaseAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var roles = new List<AdminRoleDto>();
            var sql = @"
                SELECT r.""Id"", r.""RoleLevel"", r.""RoleName"", r.""DisplayName"", r.""Icon"", r.""Color"", r.""Description"",
                       (SELECT COUNT(*) FROM ""UserRoles"" ur WHERE ur.""RoleId"" = r.""Id"") as UserCount
                FROM ""Roles"" r
                ORDER BY r.""RoleLevel"" ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                roles.Add(new AdminRoleDto
                {
                    Id = reader.GetInt32(0),
                    RoleLevel = reader.GetInt32(1),
                    RoleName = reader.GetString(2),
                    DisplayName = reader.IsDBNull(3) ? reader.GetString(2) : reader.GetString(3),
                    Icon = reader.IsDBNull(4) ? "user" : reader.GetString(4),
                    Color = reader.IsDBNull(5) ? "#808080" : reader.GetString(5),
                    Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    UserCount = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetInt64(7))
                });
            }

            return roles;
        }

        // ============================================
        // Broadcast / Annonces globales
        // ============================================

        public async Task SaveBroadcastAsync(int sentByUserId, string type, string title, string message)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Créer la table si elle n'existe pas
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS ""AdminBroadcasts"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""SentByUserId"" INTEGER NOT NULL,
                    ""Type"" VARCHAR(50) NOT NULL DEFAULT 'info',
                    ""Title"" VARCHAR(255) NOT NULL,
                    ""Message"" TEXT NOT NULL,
                    ""SentAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                )";
            using (var createCmd = new NpgsqlCommand(createTableSql, conn))
            {
                await createCmd.ExecuteNonQueryAsync();
            }

            var sql = @"
                INSERT INTO ""AdminBroadcasts"" (""SentByUserId"", ""Type"", ""Title"", ""Message"", ""SentAt"")
                VALUES (@userId, @type, @title, @message, NOW())";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("userId", sentByUserId);
            cmd.Parameters.AddWithValue("type", type);
            cmd.Parameters.AddWithValue("title", title);
            cmd.Parameters.AddWithValue("message", message);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<PaginatedResult<BroadcastHistoryDto>> GetBroadcastHistoryAsync(int page, int pageSize)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier si la table existe
            var checkTableSql = @"
                SELECT EXISTS (
                    SELECT FROM information_schema.tables 
                    WHERE table_name = 'AdminBroadcasts'
                )";
            using (var checkCmd = new NpgsqlCommand(checkTableSql, conn))
            {
                var exists = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);
                if (!exists)
                {
                    return new PaginatedResult<BroadcastHistoryDto>
                    {
                        Items = new List<BroadcastHistoryDto>(),
                        TotalCount = 0,
                        PageNumber = page,
                        PageSize = pageSize
                    };
                }
            }

            // Compter le total
            var countSql = @"SELECT COUNT(*) FROM ""AdminBroadcasts""";
            int totalCount;
            using (var countCmd = new NpgsqlCommand(countSql, conn))
            {
                totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            // Récupérer les annonces avec pagination
            var sql = @"
                SELECT b.""Id"", b.""SentByUserId"", u.""Username"", 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as DisplayName,
                       b.""Type"", b.""Title"", b.""Message"", b.""SentAt""
                FROM ""AdminBroadcasts"" b
                LEFT JOIN ""Users"" u ON b.""SentByUserId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                ORDER BY b.""SentAt"" DESC
                LIMIT @pageSize OFFSET @offset";

            var items = new List<BroadcastHistoryDto>();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("pageSize", pageSize);
            cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new BroadcastHistoryDto
                {
                    Id = reader.GetInt32(0),
                    SentByUserId = reader.GetInt32(1),
                    SentByUsername = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
                    SentByDisplayName = reader.IsDBNull(3) ? "Unknown" : reader.GetString(3),
                    Type = reader.IsDBNull(4) ? "info" : reader.GetString(4),
                    Title = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Message = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    SentAt = reader.GetDateTime(7)
                });
            }

            return new PaginatedResult<BroadcastHistoryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<BroadcastHistoryDto?> GetBroadcastByIdAsync(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT b.""Id"", b.""SentByUserId"", u.""Username"", 
                       COALESCE(p.""LastName"" || ' ' || p.""FirstName"", u.""Username"") as DisplayName,
                       b.""Type"", b.""Title"", b.""Message"", b.""SentAt""
                FROM ""AdminBroadcasts"" b
                LEFT JOIN ""Users"" u ON b.""SentByUserId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                WHERE b.""Id"" = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new BroadcastHistoryDto
                {
                    Id = reader.GetInt32(0),
                    SentByUserId = reader.GetInt32(1),
                    SentByUsername = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
                    SentByDisplayName = reader.IsDBNull(3) ? "Unknown" : reader.GetString(3),
                    Type = reader.IsDBNull(4) ? "info" : reader.GetString(4),
                    Title = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Message = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    SentAt = reader.GetDateTime(7)
                };
            }
            return null;
        }

        public async Task<ServiceResult> UpdateBroadcastAsync(int id, string type, string title, string message)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                UPDATE ""AdminBroadcasts"" 
                SET ""Type"" = @type, ""Title"" = @title, ""Message"" = @message
                WHERE ""Id"" = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("type", type);
            cmd.Parameters.AddWithValue("title", title);
            cmd.Parameters.AddWithValue("message", message);
            
            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0)
                return new ServiceResult { Success = false, Message = "Annonce non trouvée" };

            return new ServiceResult { Success = true, Message = "Annonce mise à jour avec succès" };
        }

        public async Task<ServiceResult> DeleteBroadcastAsync(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"DELETE FROM ""AdminBroadcasts"" WHERE ""Id"" = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            
            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0)
                return new ServiceResult { Success = false, Message = "Annonce non trouvée" };

            return new ServiceResult { Success = true, Message = "Annonce supprimée avec succès" };
        }

        // ============================================
        // Categories
        // ============================================

        public async Task<List<AdminRoomCategoryDto>> GetCategoriesAsync()
        {
            return await _cache.GetOrSetAsync(
                AdminCacheKeys.Categories,
                async () => await FetchCategoriesFromDatabaseAsync(),
                CacheOptions.MediumTerm // 15 minutes
            );
        }

        private async Task<List<AdminRoomCategoryDto>> FetchCategoriesFromDatabaseAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var categories = new List<AdminRoomCategoryDto>();
            var sql = @"
                SELECT c.""Id"", c.""Name"", c.""Description"", c.""Icon"", c.""Color"", c.""TextColor"",
                       c.""Order"", c.""IsVisible"", c.""IsActive"", c.""CreatedAt"", c.""UpdatedAt"",
                       (SELECT COUNT(*) FROM ""RoomSubCategories"" sc WHERE sc.""CategoryId"" = c.""Id"") as SubCategoriesCount,
                       (SELECT COUNT(*) FROM ""Rooms"" r WHERE r.""CategoryId"" = c.""Id"") as RoomsCount
                FROM ""RoomCategories"" c
                ORDER BY c.""Order"" ASC, c.""Name"" ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new AdminRoomCategoryDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Icon = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Color = reader.IsDBNull(4) ? "#3498DB" : reader.GetString(4),
                    TextColor = reader.IsDBNull(5) ? "#FFFFFF" : reader.GetString(5),
                    Order = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    IsVisible = reader.IsDBNull(7) ? true : reader.GetBoolean(7),
                    IsActive = reader.IsDBNull(8) ? true : reader.GetBoolean(8),
                    CreatedAt = reader.IsDBNull(9) ? DateTime.UtcNow : reader.GetDateTime(9),
                    UpdatedAt = reader.IsDBNull(10) ? DateTime.UtcNow : reader.GetDateTime(10),
                    SubCategoriesCount = reader.IsDBNull(11) ? 0 : Convert.ToInt32(reader.GetInt64(11)),
                    RoomsCount = reader.IsDBNull(12) ? 0 : Convert.ToInt32(reader.GetInt64(12))
                });
            }

            return categories;
        }

        public async Task<AdminRoomCategoryDto?> GetCategoryByIdAsync(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT c.""Id"", c.""Name"", c.""Description"", c.""Icon"", c.""Color"", c.""TextColor"",
                       c.""Order"", c.""IsVisible"", c.""IsActive"", c.""CreatedAt"", c.""UpdatedAt"",
                       (SELECT COUNT(*) FROM ""RoomSubCategories"" sc WHERE sc.""CategoryId"" = c.""Id"") as SubCategoriesCount,
                       (SELECT COUNT(*) FROM ""Rooms"" r WHERE r.""CategoryId"" = c.""Id"") as RoomsCount
                FROM ""RoomCategories"" c
                WHERE c.""Id"" = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            using var reader = await cmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return new AdminRoomCategoryDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Icon = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Color = reader.IsDBNull(4) ? "#3498DB" : reader.GetString(4),
                    TextColor = reader.IsDBNull(5) ? "#FFFFFF" : reader.GetString(5),
                    Order = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    IsVisible = reader.IsDBNull(7) ? true : reader.GetBoolean(7),
                    IsActive = reader.IsDBNull(8) ? true : reader.GetBoolean(8),
                    CreatedAt = reader.IsDBNull(9) ? DateTime.UtcNow : reader.GetDateTime(9),
                    UpdatedAt = reader.IsDBNull(10) ? DateTime.UtcNow : reader.GetDateTime(10),
                    SubCategoriesCount = reader.IsDBNull(11) ? 0 : Convert.ToInt32(reader.GetInt64(11)),
                    RoomsCount = reader.IsDBNull(12) ? 0 : Convert.ToInt32(reader.GetInt64(12))
                };
            }
            return null;
        }

        public async Task<ServiceResult> CreateCategoryAsync(CreateCategoryDto dto, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO ""RoomCategories"" (""Name"", ""Description"", ""Icon"", ""Color"", ""TextColor"", ""Order"", ""IsVisible"", ""IsActive"", ""CreatedBy"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (@name, @desc, @icon, @color, @textColor, @order, @visible, @active, @adminId, NOW(), NOW())
                RETURNING ""Id""";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("name", dto.Name);
            cmd.Parameters.AddWithValue("desc", (object?)dto.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("icon", (object?)dto.Icon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("color", dto.Color);
            cmd.Parameters.AddWithValue("textColor", dto.TextColor);
            cmd.Parameters.AddWithValue("order", dto.Order);
            cmd.Parameters.AddWithValue("visible", dto.IsVisible);
            cmd.Parameters.AddWithValue("active", dto.IsActive);
            cmd.Parameters.AddWithValue("adminId", adminId);

            var newId = await cmd.ExecuteScalarAsync();
            await LogActionAsync(adminId, "CreateCategory", "Category", Convert.ToInt32(newId), $"Créé: {dto.Name}");

            // Invalidate cache
            await _cache.RemoveAsync(AdminCacheKeys.Categories);

            return new ServiceResult { Success = true, Message = "Catégorie créée avec succès" };
        }

        public async Task<ServiceResult> UpdateCategoryAsync(int id, UpdateCategoryDto dto, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var updates = new List<string>();
            var cmd = new NpgsqlCommand { Connection = conn };

            if (dto.Name != null) { updates.Add(@"""Name"" = @name"); cmd.Parameters.AddWithValue("name", dto.Name); }
            if (dto.Description != null) { updates.Add(@"""Description"" = @desc"); cmd.Parameters.AddWithValue("desc", dto.Description); }
            if (dto.Icon != null) { updates.Add(@"""Icon"" = @icon"); cmd.Parameters.AddWithValue("icon", dto.Icon); }
            if (dto.Color != null) { updates.Add(@"""Color"" = @color"); cmd.Parameters.AddWithValue("color", dto.Color); }
            if (dto.TextColor != null) { updates.Add(@"""TextColor"" = @textColor"); cmd.Parameters.AddWithValue("textColor", dto.TextColor); }
            if (dto.Order.HasValue) { updates.Add(@"""Order"" = @order"); cmd.Parameters.AddWithValue("order", dto.Order.Value); }
            if (dto.IsVisible.HasValue) { updates.Add(@"""IsVisible"" = @visible"); cmd.Parameters.AddWithValue("visible", dto.IsVisible.Value); }
            if (dto.IsActive.HasValue) { updates.Add(@"""IsActive"" = @active"); cmd.Parameters.AddWithValue("active", dto.IsActive.Value); }

            if (updates.Count == 0)
                return new ServiceResult { Success = false, Message = "Aucune modification à effectuer" };

            updates.Add(@"""UpdatedAt"" = NOW()");
            updates.Add(@"""UpdatedBy"" = @adminId");
            cmd.Parameters.AddWithValue("adminId", adminId);
            cmd.Parameters.AddWithValue("id", id);

            cmd.CommandText = $@"UPDATE ""RoomCategories"" SET {string.Join(", ", updates)} WHERE ""Id"" = @id";
            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected == 0)
                return new ServiceResult { Success = false, Message = "Catégorie non trouvée" };

            await LogActionAsync(adminId, "UpdateCategory", "Category", id, $"Modifié: {dto.Name ?? "N/A"}");
            
            // Invalidate cache
            await _cache.RemoveAsync(AdminCacheKeys.Categories);
            
            return new ServiceResult { Success = true, Message = "Catégorie mise à jour" };
        }

        public async Task<ServiceResult> DeleteCategoryAsync(int id, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier s'il y a des sous-catégories ou des salons
            var checkSql = @"
                SELECT 
                    (SELECT COUNT(*) FROM ""RoomSubCategories"" WHERE ""CategoryId"" = @id) as SubCount,
                    (SELECT COUNT(*) FROM ""Rooms"" WHERE ""CategoryId"" = @id) as RoomCount";
            using var checkCmd = new NpgsqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("id", id);
            using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var subCount = reader.GetInt64(0);
                var roomCount = reader.GetInt64(1);
                if (subCount > 0 || roomCount > 0)
                {
                    await reader.CloseAsync();
                    return new ServiceResult { Success = false, Message = $"Impossible de supprimer: {subCount} sous-catégories et {roomCount} salons associés" };
                }
            }
            await reader.CloseAsync();

            var sql = @"DELETE FROM ""RoomCategories"" WHERE ""Id"" = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected == 0)
                return new ServiceResult { Success = false, Message = "Catégorie non trouvée" };

            await LogActionAsync(adminId, "DeleteCategory", "Category", id, "Supprimé");
            
            // Invalidate cache
            await _cache.RemoveAsync(AdminCacheKeys.Categories);
            
            return new ServiceResult { Success = true, Message = "Catégorie supprimée" };
        }

        // ============================================
        // SubCategories
        // ============================================

        public async Task<List<AdminRoomSubCategoryDto>> GetSubCategoriesAsync(int? categoryId = null)
        {
            var cacheKey = categoryId.HasValue 
                ? AdminCacheKeys.SubCategoriesByCategory(categoryId.Value) 
                : AdminCacheKeys.SubCategories;

            return await _cache.GetOrSetAsync(
                cacheKey,
                async () => await FetchSubCategoriesFromDatabaseAsync(categoryId),
                CacheOptions.MediumTerm // 15 minutes
            );
        }

        private async Task<List<AdminRoomSubCategoryDto>> FetchSubCategoriesFromDatabaseAsync(int? categoryId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var subCategories = new List<AdminRoomSubCategoryDto>();
            var whereClause = categoryId.HasValue ? @"WHERE sc.""CategoryId"" = @categoryId" : "";
            var sql = $@"
                SELECT sc.""Id"", sc.""CategoryId"", c.""Name"" as CategoryName, sc.""Name"", sc.""Description"", 
                       sc.""Icon"", sc.""Color"", sc.""TextColor"", sc.""DisplayOrder"", sc.""IsVisible"", sc.""IsActive"",
                       sc.""CreatedAt"", sc.""UpdatedAt"",
                       (SELECT COUNT(*) FROM ""Rooms"" r WHERE r.""SubCategoryId"" = sc.""Id"") as RoomsCount
                FROM ""RoomSubCategories"" sc
                LEFT JOIN ""RoomCategories"" c ON sc.""CategoryId"" = c.""Id""
                {whereClause}
                ORDER BY c.""Order"" ASC, sc.""DisplayOrder"" ASC, sc.""Name"" ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            if (categoryId.HasValue)
                cmd.Parameters.AddWithValue("categoryId", categoryId.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                subCategories.Add(new AdminRoomSubCategoryDto
                {
                    Id = reader.GetInt32(0),
                    CategoryId = reader.GetInt32(1),
                    CategoryName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Name = reader.GetString(3),
                    Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Icon = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Color = reader.IsDBNull(6) ? "#6C757D" : reader.GetString(6),
                    TextColor = reader.IsDBNull(7) ? "#FFFFFF" : reader.GetString(7),
                    DisplayOrder = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                    IsVisible = reader.IsDBNull(9) ? true : reader.GetBoolean(9),
                    IsActive = reader.IsDBNull(10) ? true : reader.GetBoolean(10),
                    CreatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11),
                    UpdatedAt = reader.IsDBNull(12) ? DateTime.UtcNow : reader.GetDateTime(12),
                    RoomsCount = reader.IsDBNull(13) ? 0 : Convert.ToInt32(reader.GetInt64(13))
                });
            }

            return subCategories;
        }

        public async Task<AdminRoomSubCategoryDto?> GetSubCategoryByIdAsync(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT sc.""Id"", sc.""CategoryId"", c.""Name"" as CategoryName, sc.""Name"", sc.""Description"", 
                       sc.""Icon"", sc.""Color"", sc.""TextColor"", sc.""DisplayOrder"", sc.""IsVisible"", sc.""IsActive"",
                       sc.""CreatedAt"", sc.""UpdatedAt"",
                       (SELECT COUNT(*) FROM ""Rooms"" r WHERE r.""SubCategoryId"" = sc.""Id"") as RoomsCount
                FROM ""RoomSubCategories"" sc
                LEFT JOIN ""RoomCategories"" c ON sc.""CategoryId"" = c.""Id""
                WHERE sc.""Id"" = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new AdminRoomSubCategoryDto
                {
                    Id = reader.GetInt32(0),
                    CategoryId = reader.GetInt32(1),
                    CategoryName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Name = reader.GetString(3),
                    Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Icon = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Color = reader.IsDBNull(6) ? "#6C757D" : reader.GetString(6),
                    TextColor = reader.IsDBNull(7) ? "#FFFFFF" : reader.GetString(7),
                    DisplayOrder = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                    IsVisible = reader.IsDBNull(9) ? true : reader.GetBoolean(9),
                    IsActive = reader.IsDBNull(10) ? true : reader.GetBoolean(10),
                    CreatedAt = reader.IsDBNull(11) ? DateTime.UtcNow : reader.GetDateTime(11),
                    UpdatedAt = reader.IsDBNull(12) ? DateTime.UtcNow : reader.GetDateTime(12),
                    RoomsCount = reader.IsDBNull(13) ? 0 : Convert.ToInt32(reader.GetInt64(13))
                };
            }
            return null;
        }

        public async Task<ServiceResult> CreateSubCategoryAsync(CreateSubCategoryDto dto, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier que la catégorie existe
            var checkSql = @"SELECT COUNT(*) FROM ""RoomCategories"" WHERE ""Id"" = @categoryId";
            using var checkCmd = new NpgsqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("categoryId", dto.CategoryId);
            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
            if (!exists)
                return new ServiceResult { Success = false, Message = "Catégorie parente non trouvée" };

            var sql = @"
                INSERT INTO ""RoomSubCategories"" (""CategoryId"", ""Name"", ""Description"", ""Icon"", ""Color"", ""TextColor"", ""DisplayOrder"", ""IsVisible"", ""IsActive"", ""CreatedBy"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (@categoryId, @name, @desc, @icon, @color, @textColor, @order, @visible, @active, @adminId, NOW(), NOW())
                RETURNING ""Id""";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("categoryId", dto.CategoryId);
            cmd.Parameters.AddWithValue("name", dto.Name);
            cmd.Parameters.AddWithValue("desc", (object?)dto.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("icon", (object?)dto.Icon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("color", dto.Color);
            cmd.Parameters.AddWithValue("textColor", dto.TextColor);
            cmd.Parameters.AddWithValue("order", dto.DisplayOrder);
            cmd.Parameters.AddWithValue("visible", dto.IsVisible);
            cmd.Parameters.AddWithValue("active", dto.IsActive);
            cmd.Parameters.AddWithValue("adminId", adminId);

            var newId = await cmd.ExecuteScalarAsync();
            await LogActionAsync(adminId, "CreateSubCategory", "SubCategory", Convert.ToInt32(newId), $"Créé: {dto.Name}");

            // Invalidate cache
            await _cache.RemoveAsync(AdminCacheKeys.SubCategories);
            await _cache.RemoveAsync(AdminCacheKeys.SubCategoriesByCategory(dto.CategoryId));
            await _cache.RemoveAsync(AdminCacheKeys.Categories); // SubCategories count changed

            return new ServiceResult { Success = true, Message = "Sous-catégorie créée avec succès" };
        }

        public async Task<ServiceResult> UpdateSubCategoryAsync(int id, UpdateSubCategoryDto dto, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var updates = new List<string>();
            var cmd = new NpgsqlCommand { Connection = conn };

            if (dto.CategoryId.HasValue) { updates.Add(@"""CategoryId"" = @categoryId"); cmd.Parameters.AddWithValue("categoryId", dto.CategoryId.Value); }
            if (dto.Name != null) { updates.Add(@"""Name"" = @name"); cmd.Parameters.AddWithValue("name", dto.Name); }
            if (dto.Description != null) { updates.Add(@"""Description"" = @desc"); cmd.Parameters.AddWithValue("desc", dto.Description); }
            if (dto.Icon != null) { updates.Add(@"""Icon"" = @icon"); cmd.Parameters.AddWithValue("icon", dto.Icon); }
            if (dto.Color != null) { updates.Add(@"""Color"" = @color"); cmd.Parameters.AddWithValue("color", dto.Color); }
            if (dto.TextColor != null) { updates.Add(@"""TextColor"" = @textColor"); cmd.Parameters.AddWithValue("textColor", dto.TextColor); }
            if (dto.DisplayOrder.HasValue) { updates.Add(@"""DisplayOrder"" = @order"); cmd.Parameters.AddWithValue("order", dto.DisplayOrder.Value); }
            if (dto.IsVisible.HasValue) { updates.Add(@"""IsVisible"" = @visible"); cmd.Parameters.AddWithValue("visible", dto.IsVisible.Value); }
            if (dto.IsActive.HasValue) { updates.Add(@"""IsActive"" = @active"); cmd.Parameters.AddWithValue("active", dto.IsActive.Value); }

            if (updates.Count == 0)
                return new ServiceResult { Success = false, Message = "Aucune modification à effectuer" };

            updates.Add(@"""UpdatedAt"" = NOW()");
            updates.Add(@"""UpdatedBy"" = @adminId");
            cmd.Parameters.AddWithValue("adminId", adminId);
            cmd.Parameters.AddWithValue("id", id);

            cmd.CommandText = $@"UPDATE ""RoomSubCategories"" SET {string.Join(", ", updates)} WHERE ""Id"" = @id";
            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected == 0)
                return new ServiceResult { Success = false, Message = "Sous-catégorie non trouvée" };

            await LogActionAsync(adminId, "UpdateSubCategory", "SubCategory", id, $"Modifié: {dto.Name ?? "N/A"}");
            
            // Invalidate cache
            await _cache.RemoveAsync(AdminCacheKeys.SubCategories);
            // We don't know the categoryId here, so invalidate all category-specific caches is not efficient
            // But it's a rare operation, so it's acceptable
            
            return new ServiceResult { Success = true, Message = "Sous-catégorie mise à jour" };
        }

        public async Task<ServiceResult> DeleteSubCategoryAsync(int id, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier s'il y a des salons
            var checkSql = @"SELECT COUNT(*) FROM ""Rooms"" WHERE ""SubCategoryId"" = @id";
            using var checkCmd = new NpgsqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("id", id);
            var roomCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
            if (roomCount > 0)
                return new ServiceResult { Success = false, Message = $"Impossible de supprimer: {roomCount} salons associés" };

            var sql = @"DELETE FROM ""RoomSubCategories"" WHERE ""Id"" = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected == 0)
                return new ServiceResult { Success = false, Message = "Sous-catégorie non trouvée" };

            await LogActionAsync(adminId, "DeleteSubCategory", "SubCategory", id, "Supprimé");
            
            // Invalidate cache
            await _cache.RemoveAsync(AdminCacheKeys.SubCategories);
            await _cache.RemoveAsync(AdminCacheKeys.Categories); // SubCategories count changed
            
            return new ServiceResult { Success = true, Message = "Sous-catégorie supprimée" };
        }

        // ============================================
        // Users
        // ============================================

        public async Task<PaginatedResult<AdminUserDto>> GetUsersAsync(int page, int pageSize, string? search, int? roleLevel, bool? isOnline, bool? isBanned)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var whereClause = "WHERE 1=1";
            if (!string.IsNullOrEmpty(search))
                whereClause += " AND (u.\"Username\" ILIKE @search OR p.\"FirstName\" ILIKE @search OR p.\"LastName\" ILIKE @search)";
            if (roleLevel.HasValue)
                whereClause += " AND r.\"RoleLevel\" = @roleLevel";
            if (isBanned.HasValue)
                whereClause += isBanned.Value ? " AND b.\"Id\" IS NOT NULL AND b.\"IsActive\" = true AND (b.\"ExpiresAt\" IS NULL OR b.\"ExpiresAt\" > NOW())" : " AND (b.\"Id\" IS NULL OR b.\"IsActive\" = false OR (b.\"ExpiresAt\" IS NOT NULL AND b.\"ExpiresAt\" <= NOW()))";

            // Count total
            var countSql = $@"
                SELECT COUNT(DISTINCT u.""Id"")
                FROM ""Users"" u
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                LEFT JOIN ""UserRoles"" ur ON u.""Id"" = ur.""UserId""
                LEFT JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id""
                LEFT JOIN ""BannedUsers"" b ON u.""Id"" = b.""UserId"" AND b.""IsActive"" = true
                {whereClause}";

            int totalCount;
            using (var cmd = new NpgsqlCommand(countSql, conn))
            {
                if (!string.IsNullOrEmpty(search))
                    cmd.Parameters.AddWithValue("search", $"%{search}%");
                if (roleLevel.HasValue)
                    cmd.Parameters.AddWithValue("roleLevel", roleLevel.Value);
                totalCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Get users
            var sql = $@"
                SELECT DISTINCT ON (u.""Id"")
                    u.""Id"", u.""Username"", u.""CreatedAt"",
                    COALESCE(r.""RoleName"", 'User') as RoleName,
                    COALESCE(r.""RoleLevel"", 7) as RoleLevel,
                    p.""FirstName"", p.""LastName"",
                    (SELECT COUNT(*) FROM ""Rooms"" WHERE ""OwnerId"" = u.""Id"") as RoomsCreated,
                    (SELECT COUNT(*) FROM ""PrivateMessages"" WHERE ""SenderId"" = u.""Id"") as MessagesCount,
                    (SELECT COUNT(*) FROM ""Warnings"" WHERE ""UserId"" = u.""Id"") as WarningsCount,
                    CASE WHEN EXISTS(SELECT 1 FROM ""UserSessions"" s WHERE s.""UserId"" = u.""Id"" AND s.""DéconnectéLe"" IS NULL) THEN true ELSE false END as IsOnline,
                    CASE WHEN EXISTS(SELECT 1 FROM ""BannedUsers"" b WHERE b.""UserId"" = u.""Id"" AND b.""IsActive"" = true AND (b.""ExpiresAt"" IS NULL OR b.""ExpiresAt"" > NOW())) THEN true ELSE false END as IsBanned,
                    (SELECT b.""Reason"" FROM ""BannedUsers"" b WHERE b.""UserId"" = u.""Id"" AND b.""IsActive"" = true ORDER BY b.""BannedAt"" DESC LIMIT 1) as BanReason,
                    COALESCE(r.""DisplayName"", 'Utilisateur') as RoleDisplayName,
                    r.""Color"" as RoleColor,
                    p.""AvatarPath""
                FROM ""Users"" u
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                LEFT JOIN ""UserRoles"" ur ON u.""Id"" = ur.""UserId""
                LEFT JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id""
                LEFT JOIN ""BannedUsers"" b ON u.""Id"" = b.""UserId"" AND b.""IsActive"" = true
                {whereClause}
                ORDER BY u.""Id"", u.""CreatedAt"" DESC
                LIMIT @pageSize OFFSET @offset";

            var users = new List<AdminUserDto>();
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                if (!string.IsNullOrEmpty(search))
                    cmd.Parameters.AddWithValue("search", $"%{search}%");
                if (roleLevel.HasValue)
                    cmd.Parameters.AddWithValue("roleLevel", roleLevel.Value);
                cmd.Parameters.AddWithValue("pageSize", pageSize);
                cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    users.Add(new AdminUserDto
                    {
                        Id = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        CreatedAt = reader.GetDateTime(2),
                        Role = reader.GetString(3),
                        RoleLevel = reader.GetInt32(4),
                        FirstName = reader.IsDBNull(5) ? null : reader.GetString(5),
                        LastName = reader.IsDBNull(6) ? null : reader.GetString(6),
                        RoomsCreated = reader.GetInt32(7),
                        MessagesCount = reader.GetInt32(8),
                        WarningsCount = reader.GetInt32(9),
                        IsOnline = reader.GetBoolean(10),
                        IsBanned = reader.GetBoolean(11),
                        BanReason = reader.IsDBNull(12) ? null : reader.GetString(12),
                        RoleDisplayName = reader.GetString(13),
                        RoleColor = reader.IsDBNull(14) ? null : reader.GetString(14),
                        AvatarPath = reader.IsDBNull(15) ? null : reader.GetString(15)
                    });
                }
            }

            return new PaginatedResult<AdminUserDto>
            {
                Items = users,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<AdminUserDetailDto?> GetUserByIdAsync(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT 
                    u.""Id"", u.""Username"", u.""CreatedAt"",
                    COALESCE(r.""RoleName"", 'User') as RoleName,
                    COALESCE(r.""RoleLevel"", 7) as RoleLevel,
                    p.""FirstName"", p.""LastName"", NULL as Bio, p.""AvatarPath"" as Avatar,
                    (SELECT COUNT(*) FROM ""Rooms"" WHERE ""OwnerId"" = u.""Id"") as RoomsCreated,
                    (SELECT COUNT(*) FROM ""PrivateMessages"" WHERE ""SenderId"" = u.""Id"") as MessagesCount,
                    (SELECT COUNT(*) FROM ""Warnings"" WHERE ""UserId"" = u.""Id"") as WarningsCount,
                    CASE WHEN EXISTS(SELECT 1 FROM ""UserSessions"" s WHERE s.""UserId"" = u.""Id"" AND s.""DéconnectéLe"" IS NULL) THEN true ELSE false END as IsOnline,
                    (SELECT MAX(""ConnectéLe"") FROM ""UserSessions"" WHERE ""UserId"" = u.""Id"") as LastLoginAt,
                    CASE WHEN EXISTS(SELECT 1 FROM ""BannedUsers"" b WHERE b.""UserId"" = u.""Id"" AND b.""IsActive"" = true AND (b.""ExpiresAt"" IS NULL OR b.""ExpiresAt"" > NOW())) THEN true ELSE false END as IsBanned,
                    (SELECT b.""Reason"" FROM ""BannedUsers"" b WHERE b.""UserId"" = u.""Id"" AND b.""IsActive"" = true ORDER BY b.""BannedAt"" DESC LIMIT 1) as BanReason,
                    (SELECT b.""ExpiresAt"" FROM ""BannedUsers"" b WHERE b.""UserId"" = u.""Id"" AND b.""IsActive"" = true ORDER BY b.""BannedAt"" DESC LIMIT 1) as BanExpiresAt,
                    s.""TierId"" as SubscriptionType, s.""ExpiresAt"" as SubscriptionEndDate,
                    COALESCE(r.""DisplayName"", 'Utilisateur') as RoleDisplayName,
                    r.""Color"" as RoleColor
                FROM ""Users"" u
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                LEFT JOIN ""UserRoles"" ur ON u.""Id"" = ur.""UserId""
                LEFT JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id""
                LEFT JOIN ""UserSubscriptions"" s ON u.""Id"" = s.""UserId"" AND s.""IsActive"" = true AND (s.""ExpiresAt"" IS NULL OR s.""ExpiresAt"" > NOW())
                WHERE u.""Id"" = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new AdminUserDetailDto
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    CreatedAt = reader.GetDateTime(2),
                    Role = reader.GetString(3),
                    RoleLevel = reader.GetInt32(4),
                    FirstName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    LastName = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Bio = reader.IsDBNull(7) ? null : reader.GetString(7),
                    ProfilePicture = reader.IsDBNull(8) ? null : reader.GetString(8),
                    RoomsCreated = reader.GetInt32(9),
                    MessagesCount = reader.GetInt32(10),
                    WarningsCount = reader.GetInt32(11),
                    IsOnline = reader.GetBoolean(12),
                    LastLoginAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                    IsBanned = reader.GetBoolean(14),
                    BanReason = reader.IsDBNull(15) ? null : reader.GetString(15),
                    BanExpiresAt = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                    SubscriptionType = reader.IsDBNull(17) ? 0 : reader.GetInt32(17),
                    SubscriptionEndDate = reader.IsDBNull(18) ? null : reader.GetDateTime(18),
                    RoleDisplayName = reader.GetString(19),
                    RoleColor = reader.IsDBNull(20) ? null : reader.GetString(20),
                    AvatarPath = reader.IsDBNull(8) ? null : reader.GetString(8)
                };
            }

            return null;
        }

        public async Task<string?> GetUsernameByIdAsync(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"SELECT ""Username"" FROM ""Users"" WHERE ""Id"" = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        public async Task<ServiceResult> BanUserAsync(int userId, string reason, int? durationDays, int adminId, int adminRoleLevel)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier que l'utilisateur cible n'est pas un admin de niveau supérieur
            int targetRoleLevel;
            using (var cmd = new NpgsqlCommand(@"SELECT COALESCE(r.""RoleLevel"", 7) FROM ""Users"" u LEFT JOIN ""UserRoles"" ur ON u.""Id"" = ur.""UserId"" LEFT JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id"" WHERE u.""Id"" = @id", conn))
            {
                cmd.Parameters.AddWithValue("id", userId);
                var result = await cmd.ExecuteScalarAsync();
                targetRoleLevel = result != null ? Convert.ToInt32(result) : 7;
            }

            if (targetRoleLevel <= adminRoleLevel)
                return new ServiceResult { Success = false, Message = "Vous ne pouvez pas bannir un utilisateur de rang supérieur ou égal" };

            DateTime? expiresAt = durationDays.HasValue ? DateTime.UtcNow.AddDays(durationDays.Value) : null;

            var sql = @"
                INSERT INTO ""BannedUsers"" (""UserId"", ""Reason"", ""BannedBy"", ""ExpiresAt"", ""IsActive"", ""BannedAt"")
                VALUES (@userId, @reason, @adminId, @expiresAt, true, NOW())";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("userId", userId);
                cmd.Parameters.AddWithValue("reason", reason);
                cmd.Parameters.AddWithValue("adminId", adminId);
                cmd.Parameters.AddWithValue("expiresAt", expiresAt ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            // Fermer les sessions actives
            using (var cmd = new NpgsqlCommand(@"UPDATE ""UserSessions"" SET ""DéconnectéLe"" = NOW() WHERE ""UserId"" = @userId AND ""DéconnectéLe"" IS NULL", conn))
            {
                cmd.Parameters.AddWithValue("userId", userId);
                await cmd.ExecuteNonQueryAsync();
            }

            await LogActionAsync(adminId, "Ban", "User", userId, reason);

            return new ServiceResult { Success = true };
        }

        public async Task<ServiceResult> UnbanUserAsync(int userId, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"UPDATE ""BannedUsers"" SET ""IsActive"" = false, ""UnbannedBy"" = @adminId, ""UnbannedAt"" = NOW() WHERE ""UserId"" = @userId AND ""IsActive"" = true";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("userId", userId);
            cmd.Parameters.AddWithValue("adminId", adminId);
            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected == 0)
                return new ServiceResult { Success = false, Message = "Aucun ban actif trouvé" };

            await LogActionAsync(adminId, "Unban", "User", userId, null);

            return new ServiceResult { Success = true };
        }

        public async Task<ServiceResult> ChangeUserRoleAsync(int userId, int newRoleLevel, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Récupérer l'ID du rôle correspondant
            int roleId;
            using (var cmd = new NpgsqlCommand(@"SELECT ""Id"" FROM ""Roles"" WHERE ""RoleLevel"" = @level", conn))
            {
                cmd.Parameters.AddWithValue("level", newRoleLevel);
                var result = await cmd.ExecuteScalarAsync();
                if (result == null)
                    return new ServiceResult { Success = false, Message = "Niveau de rôle invalide" };
                roleId = Convert.ToInt32(result);
            }

            // Mettre à jour le rôle
            var sql = @"
                UPDATE ""UserRoles"" SET ""RoleId"" = @roleId 
                WHERE ""UserId"" = @userId";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("roleId", roleId);
                cmd.Parameters.AddWithValue("userId", userId);
                await cmd.ExecuteNonQueryAsync();
            }

            await LogActionAsync(adminId, "ChangeRole", "User", userId, $"Nouveau niveau: {newRoleLevel}");

            return new ServiceResult { Success = true };
        }

        public async Task<ServiceResult> WarnUserAsync(int userId, string reason, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO ""Warnings"" (""UserId"", ""Reason"", ""AdminId"", ""CreatedAt"")
                VALUES (@userId, @reason, @adminId, NOW())";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("userId", userId);
            cmd.Parameters.AddWithValue("reason", reason);
            cmd.Parameters.AddWithValue("adminId", adminId);
            await cmd.ExecuteNonQueryAsync();

            await LogActionAsync(adminId, "Warning", "User", userId, reason);

            return new ServiceResult { Success = true };
        }

        // ============================================
        // Rooms
        // ============================================

        public async Task<PaginatedResult<AdminRoomDto>> GetRoomsAsync(int page, int pageSize, string? search, bool? isActive)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var whereClause = "WHERE 1=1";
            if (!string.IsNullOrEmpty(search))
                whereClause += " AND r.\"Name\" ILIKE @search";
            if (isActive.HasValue)
                whereClause += " AND r.\"IsActive\" = @isActive";

            // Count
            int totalCount;
            using (var cmd = new NpgsqlCommand($@"SELECT COUNT(*) FROM ""Rooms"" r {whereClause}", conn))
            {
                if (!string.IsNullOrEmpty(search))
                    cmd.Parameters.AddWithValue("search", $"%{search}%");
                if (isActive.HasValue)
                    cmd.Parameters.AddWithValue("isActive", isActive.Value);
                totalCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            var sql = $@"
                SELECT 
                    r.""Id"", r.""Name"", r.""Description"", r.""IsActive"", r.""CreatedAt"",
                    r.""OwnerId"", u.""Username"" as OwnerUsername,
                    COALESCE(NULLIF(TRIM(CONCAT(up.""LastName"", ' ', up.""FirstName"")), ''), u.""Username"") as OwnerDisplayName,
                    r.""MaxUsers"", r.""IsPrivate"", CASE WHEN r.""Password"" IS NOT NULL AND r.""Password"" <> '' THEN true ELSE false END as HasPassword,
                    (SELECT COUNT(*) FROM ""RoomMembers"" rm WHERE rm.""RoomId"" = r.""Id"") as CurrentUsers
                FROM ""Rooms"" r
                LEFT JOIN ""Users"" u ON r.""OwnerId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" up ON u.""Id"" = up.""UserId""
                {whereClause}
                ORDER BY r.""CreatedAt"" DESC
                LIMIT @pageSize OFFSET @offset";

            var rooms = new List<AdminRoomDto>();
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                if (!string.IsNullOrEmpty(search))
                    cmd.Parameters.AddWithValue("search", $"%{search}%");
                if (isActive.HasValue)
                    cmd.Parameters.AddWithValue("isActive", isActive.Value);
                cmd.Parameters.AddWithValue("pageSize", pageSize);
                cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rooms.Add(new AdminRoomDto
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        IsActive = reader.GetBoolean(3),
                        CreatedAt = reader.GetDateTime(4),
                        OwnerId = reader.GetInt32(5),
                        OwnerUsername = reader.IsDBNull(6) ? "Inconnu" : reader.GetString(6),
                        OwnerDisplayName = reader.IsDBNull(7) ? "Inconnu" : reader.GetString(7),
                        MaxUsers = reader.GetInt32(8),
                        IsPrivate = reader.GetBoolean(9),
                        HasPassword = reader.GetBoolean(10),
                        CurrentUsers = reader.GetInt32(11)
                    });
                }
            }

            return new PaginatedResult<AdminRoomDto>
            {
                Items = rooms,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<AdminRoomDto?> GetRoomByIdAsync(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT 
                    r.""Id"", r.""Name"", r.""Description"", r.""IsActive"", r.""CreatedAt"",
                    r.""OwnerId"", u.""Username"" as OwnerUsername,
                    COALESCE(NULLIF(TRIM(CONCAT(up.""LastName"", ' ', up.""FirstName"")), ''), u.""Username"") as OwnerDisplayName,
                    r.""MaxUsers"", r.""IsPrivate"", 
                    CASE WHEN r.""Password"" IS NOT NULL AND r.""Password"" <> '' THEN true ELSE false END as HasPassword,
                    (SELECT COUNT(*) FROM ""RoomMembers"" rm WHERE rm.""RoomId"" = r.""Id"") as CurrentUsers,
                    c.""Name"" as Category,
                    rs.""TierId"", rst.""Name"" as SubscriptionType, rs.""ExpiresAt"" as SubscriptionEndDate
                FROM ""Rooms"" r
                LEFT JOIN ""Users"" u ON r.""OwnerId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" up ON u.""Id"" = up.""UserId""
                LEFT JOIN ""RoomCategories"" c ON r.""CategoryId"" = c.""Id""
                LEFT JOIN ""RoomSubscriptions"" rs ON r.""Id"" = rs.""RoomId"" AND rs.""IsActive"" = true
                LEFT JOIN ""RoomSubscriptionTiers"" rst ON rs.""TierId"" = rst.""Id""
                WHERE r.""Id"" = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new AdminRoomDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    IsActive = reader.GetBoolean(3),
                    CreatedAt = reader.GetDateTime(4),
                    OwnerId = reader.GetInt32(5),
                    OwnerUsername = reader.IsDBNull(6) ? "Inconnu" : reader.GetString(6),
                    OwnerDisplayName = reader.IsDBNull(7) ? "Inconnu" : reader.GetString(7),
                    MaxUsers = reader.GetInt32(8),
                    IsPrivate = reader.GetBoolean(9),
                    HasPassword = reader.GetBoolean(10),
                    CurrentUsers = reader.GetInt32(11),
                    Category = reader.IsDBNull(12) ? null : reader.GetString(12),
                    SubscriptionType = reader.IsDBNull(14) ? "Free" : reader.GetString(14),
                    SubscriptionEndDate = reader.IsDBNull(15) ? null : reader.GetDateTime(15)
                };
            }
            return null;
        }

        public async Task<ServiceResult> CloseRoomAsync(int roomId, string? reason, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"UPDATE ""Rooms"" SET ""IsActive"" = false WHERE ""Id"" = @roomId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected == 0)
                return new ServiceResult { Success = false, Message = "Salon non trouvé" };

            await LogActionAsync(adminId, "CloseRoom", "Room", roomId, reason);

            return new ServiceResult { Success = true };
        }

        public async Task<ServiceResult> DeleteRoomAsync(int roomId, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // D'abord supprimer les membres
            using (var cmd = new NpgsqlCommand(@"DELETE FROM ""RoomMembers"" WHERE ""RoomId"" = @roomId", conn))
            {
                cmd.Parameters.AddWithValue("roomId", roomId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Puis supprimer le salon
            using (var cmd = new NpgsqlCommand(@"DELETE FROM ""Rooms"" WHERE ""Id"" = @roomId", conn))
            {
                cmd.Parameters.AddWithValue("roomId", roomId);
                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected == 0)
                    return new ServiceResult { Success = false, Message = "Salon non trouvé" };
            }

            await LogActionAsync(adminId, "DeleteRoom", "Room", roomId, null);

            return new ServiceResult { Success = true };
        }

        // ============================================
        // Reports
        // ============================================

        public async Task<PaginatedResult<AdminReportDto>> GetReportsAsync(int page, int pageSize, string? status)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var whereClause = "WHERE 1=1";
            if (!string.IsNullOrEmpty(status))
                whereClause += " AND r.\"Status\" = @status";

            int totalCount;
            using (var cmd = new NpgsqlCommand($@"SELECT COUNT(*) FROM ""Reports"" r {whereClause}", conn))
            {
                if (!string.IsNullOrEmpty(status))
                    cmd.Parameters.AddWithValue("status", status);
                totalCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            var sql = $@"
                SELECT 
                    r.""Id"", r.""Reason"", r.""Details"", r.""Status"", r.""CreatedAt"",
                    r.""ReporterId"", reporter.""Username"" as ReporterUsername,
                    COALESCE(NULLIF(TRIM(COALESCE(rp.""LastName"", '') || ' ' || COALESCE(rp.""FirstName"", '')), ''), reporter.""Username"") as ReporterDisplayName,
                    r.""ReportedUserId"", reported.""Username"" as ReportedUsername,
                    COALESCE(NULLIF(TRIM(COALESCE(rdp.""LastName"", '') || ' ' || COALESCE(rdp.""FirstName"", '')), ''), reported.""Username"") as ReportedDisplayName,
                    r.""Resolution"", r.""ResolvedAt"",
                    r.""ResolvedBy"", resolver.""Username"" as ResolverUsername,
                    COALESCE(NULLIF(TRIM(COALESCE(rsp.""LastName"", '') || ' ' || COALESCE(rsp.""FirstName"", '')), ''), resolver.""Username"") as ResolverDisplayName
                FROM ""Reports"" r
                LEFT JOIN ""Users"" reporter ON r.""ReporterId"" = reporter.""Id""
                LEFT JOIN ""UserProfiles"" rp ON reporter.""Id"" = rp.""UserId""
                LEFT JOIN ""Users"" reported ON r.""ReportedUserId"" = reported.""Id""
                LEFT JOIN ""UserProfiles"" rdp ON reported.""Id"" = rdp.""UserId""
                LEFT JOIN ""Users"" resolver ON r.""ResolvedBy"" = resolver.""Id""
                LEFT JOIN ""UserProfiles"" rsp ON resolver.""Id"" = rsp.""UserId""
                {whereClause}
                ORDER BY r.""CreatedAt"" DESC
                LIMIT @pageSize OFFSET @offset";

            var reports = new List<AdminReportDto>();
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                if (!string.IsNullOrEmpty(status))
                    cmd.Parameters.AddWithValue("status", status);
                cmd.Parameters.AddWithValue("pageSize", pageSize);
                cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    reports.Add(new AdminReportDto
                    {
                        Id = reader.GetInt32(0),
                        Reason = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Status = reader.GetString(3),
                        CreatedAt = reader.GetDateTime(4),
                        ReporterId = reader.GetInt32(5),
                        ReporterUsername = reader.GetString(6),
                        ReporterDisplayName = reader.IsDBNull(7) ? null : reader.GetString(7),
                        ReportedUserId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                        ReportedUsername = reader.IsDBNull(9) ? null : reader.GetString(9),
                        ReportedDisplayName = reader.IsDBNull(10) ? null : reader.GetString(10),
                        Resolution = reader.IsDBNull(11) ? null : reader.GetString(11),
                        ResolvedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                        ResolvedById = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                        ResolverUsername = reader.IsDBNull(14) ? null : reader.GetString(14),
                        ResolverDisplayName = reader.IsDBNull(15) ? null : reader.GetString(15)
                    });
                }
            }

            return new PaginatedResult<AdminReportDto>
            {
                Items = reports,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<ServiceResult> ResolveReportAsync(int reportId, string resolution, string? action, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                UPDATE ""Reports"" 
                SET ""Status"" = 'Resolved', ""Resolution"" = @resolution, ""ResolvedAt"" = NOW(), ""ResolvedBy"" = @adminId
                WHERE ""Id"" = @reportId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("reportId", reportId);
            cmd.Parameters.AddWithValue("resolution", resolution);
            cmd.Parameters.AddWithValue("adminId", adminId);
            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected == 0)
                return new ServiceResult { Success = false, Message = "Signalement non trouvé" };

            await LogActionAsync(adminId, "ResolveReport", "Report", reportId, resolution);

            return new ServiceResult { Success = true };
        }

        public async Task<ServiceResult> DismissReportAsync(int reportId, string? reason, int adminId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                UPDATE ""Reports"" 
                SET ""Status"" = 'Dismissed', ""Resolution"" = @reason, ""ResolvedAt"" = NOW(), ""ResolvedBy"" = @adminId
                WHERE ""Id"" = @reportId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("reportId", reportId);
            cmd.Parameters.AddWithValue("reason", reason ?? "Rejeté");
            cmd.Parameters.AddWithValue("adminId", adminId);
            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected == 0)
                return new ServiceResult { Success = false, Message = "Signalement non trouvé" };

            await LogActionAsync(adminId, "DismissReport", "Report", reportId, reason);

            return new ServiceResult { Success = true };
        }

        // ============================================
        // Logs
        // ============================================

        public async Task<PaginatedResult<AdminAuditLogDto>> GetAuditLogsAsync(int page, int pageSize)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            int totalCount;
            using (var cmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""AdminAuditLogs""", conn))
            {
                totalCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            var sql = @"
                SELECT 
                    l.""Id"", l.""AdminId"", u.""Username"" as AdminUsername,
                    COALESCE(NULLIF(TRIM(COALESCE(p.""LastName"", '') || ' ' || COALESCE(p.""FirstName"", '')), ''), u.""Username"") as AdminDisplayName,
                    l.""Action"", l.""TargetType"", l.""TargetId"", l.""Details"", l.""CreatedAt""
                FROM ""AdminAuditLogs"" l
                LEFT JOIN ""Users"" u ON l.""AdminId"" = u.""Id""
                LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                ORDER BY l.""CreatedAt"" DESC
                LIMIT @pageSize OFFSET @offset";

            var logs = new List<AdminAuditLogDto>();
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("pageSize", pageSize);
                cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    logs.Add(new AdminAuditLogDto
                    {
                        Id = reader.GetInt32(0),
                        AdminId = reader.GetInt32(1),
                        AdminUsername = reader.IsDBNull(2) ? "Système" : reader.GetString(2),
                        AdminDisplayName = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Action = reader.GetString(4),
                        TargetType = reader.IsDBNull(5) ? null : reader.GetString(5),
                        TargetId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                        Details = reader.IsDBNull(7) ? null : reader.GetString(7),
                        CreatedAt = reader.GetDateTime(8)
                    });
                }
            }

            return new PaginatedResult<AdminAuditLogDto>
            {
                Items = logs,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task LogActionAsync(int adminId, string action, string targetType, int? targetId, string? details)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // Récupérer le username de l'admin pour un meilleur audit
                string? adminUsername = null;
                using (var userCmd = new NpgsqlCommand(@"SELECT ""Username"" FROM ""Users"" WHERE ""Id"" = @id", conn))
                {
                    userCmd.Parameters.AddWithValue("id", adminId);
                    adminUsername = await userCmd.ExecuteScalarAsync() as string;
                }

                var sql = @"
                    INSERT INTO ""AdminAuditLogs"" (""AdminId"", ""Action"", ""TargetType"", ""TargetId"", ""Details"", ""CreatedAt"")
                    VALUES (@adminId, @action, @targetType, @targetId, @details, NOW())";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("adminId", adminId);
                cmd.Parameters.AddWithValue("action", action);
                cmd.Parameters.AddWithValue("targetType", targetType);
                cmd.Parameters.AddWithValue("targetId", targetId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("details", details ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();

                // Log aussi dans Serilog pour une traçabilité complète
                Serilog.Log.Information(
                    "[AUDIT] Admin {AdminId} ({Username}) - Action: {Action} - Target: {TargetType}:{TargetId} - Details: {Details}",
                    adminId, adminUsername ?? "Unknown", action, targetType, targetId, details ?? "N/A");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[AUDIT] Failed to log admin action: {Action}", action);
            }
        }

        // ============================================
        // System
        // ============================================

        public Task SetMaintenanceModeAsync(bool enabled, string? message)
        {
            _maintenanceMode = enabled;
            _maintenanceMessage = message;
            return Task.CompletedTask;
        }
    }

    // DTOs pour AdminService
    public class AdminDashboardStats
    {
        public int TotalUsers { get; set; }
        public int OnlineUsers { get; set; }
        public int ActiveRooms { get; set; }
        public int TotalMessages { get; set; }
        public int NewUsersToday { get; set; }
        public int PendingReports { get; set; }
        public int PremiumUsers { get; set; } // Deluxe à Gold
        public int VipUsers { get; set; } // Platinum à Legend
        public int FreeUsers { get; set; } // Member (gratuit)
        public double ServerUptime { get; set; } = 99.9;
        public List<DailyActivityDto> WeeklyActivity { get; set; } = new();
        public List<RecentActivityDto> RecentActivities { get; set; } = new();
        public List<SubscriptionTierStatsDto> SubscriptionDistribution { get; set; } = new(); // Répartition détaillée
    }

    public class AdminRoleDto
    {
        public int Id { get; set; }
        public int RoleLevel { get; set; }
        public string RoleName { get; set; } = ""; // Nom technique (ServerMaster)
        public string DisplayName { get; set; } = ""; // Nom affiché (Maître du Serveur)
        public string Icon { get; set; } = "user"; // Icône (trophy, shield, etc.)
        public string Color { get; set; } = "#808080"; // Couleur
        public string Description { get; set; } = ""; // Description du rôle
        public int UserCount { get; set; } // Nombre d'utilisateurs avec ce rôle
    }

    public class SubscriptionTierStatsDto
    {
        public int TierId { get; set; }
        public string Name { get; set; } = "";
        public string Color { get; set; } = "";
        public int Count { get; set; }
    }

    public class DailyActivityDto
    {
        public string Day { get; set; } = ""; // Lun, Mar, Mer...
        public DateTime Date { get; set; }
        public int ActiveUsers { get; set; }
        public int Connections { get; set; }
        public int Messages { get; set; }
    }

    public class RecentActivityDto
    {
        public string Type { get; set; } = ""; // "user_registered", "report_created", "user_banned", etc.
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminUserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Role { get; set; } = "User"; // Nom technique (ServerMaster)
        public string? RoleDisplayName { get; set; } // Nom affiché (Maître du Serveur)
        public string? RoleColor { get; set; } // Couleur du rôle (#FFD700)
        public int RoleLevel { get; set; } = 7;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? AvatarPath { get; set; } // Chemin vers la photo de profil
        
        /// <summary>
        /// Nom complet affiché (LastName FirstName) ou Username si pas de profil
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(FirstName) || !string.IsNullOrEmpty(LastName)
            ? $"{LastName ?? ""} {FirstName ?? ""}".Trim()
            : Username;
            
        public int RoomsCreated { get; set; }
        public int MessagesCount { get; set; }
        public int WarningsCount { get; set; }
        public bool IsOnline { get; set; }
        public bool IsBanned { get; set; }
        public string? BanReason { get; set; }
    }

    public class AdminUserDetailDto : AdminUserDto
    {
        public string? Bio { get; set; }
        public string? ProfilePicture { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? BanExpiresAt { get; set; }
        public int SubscriptionType { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        // RoleColor, RoleDisplayName et AvatarPath hérités de AdminUserDto
    }

    public class AdminRoomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int OwnerId { get; set; }
        public string OwnerUsername { get; set; } = "";
        public string OwnerDisplayName { get; set; } = ""; // Prénom Nom
        public int MaxUsers { get; set; }
        public int CurrentUsers { get; set; }
        public bool IsPrivate { get; set; }
        public bool HasPassword { get; set; }
        public string? Category { get; set; }
        public string? SubscriptionType { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
    }

    public class AdminReportDto
    {
        public int Id { get; set; }
        public string Reason { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
        public int ReporterId { get; set; }
        public string ReporterUsername { get; set; } = "";
        public string? ReporterDisplayName { get; set; } // Nom complet
        public int? ReportedUserId { get; set; }
        public string? ReportedUsername { get; set; }
        public string? ReportedDisplayName { get; set; } // Nom complet
        public string? Resolution { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int? ResolvedById { get; set; }
        public string? ResolverUsername { get; set; }
        public string? ResolverDisplayName { get; set; } // Nom complet
    }

    public class AdminAuditLogDto
    {
        public int Id { get; set; }
        public int AdminId { get; set; }
        public string AdminUsername { get; set; } = "";
        public string? AdminDisplayName { get; set; } // Nom complet (Admin A)
        public string Action { get; set; } = "";
        public string? TargetType { get; set; }
        public int? TargetId { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
