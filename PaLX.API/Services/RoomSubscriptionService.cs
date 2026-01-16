using Npgsql;
using PaLX.API.DTOs;

namespace PaLX.API.Services;

public class RoomSubscriptionService
{
    private readonly string _connectionString;
    private readonly ILogger<RoomSubscriptionService> _logger;

    public RoomSubscriptionService(IConfiguration configuration, ILogger<RoomSubscriptionService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _logger = logger;
    }

    // ========================= TIERS =========================

    public async Task<List<RoomSubscriptionTierDto>> GetAllTiersAsync()
    {
        var tiers = new List<RoomSubscriptionTierDto>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT 
                rst.""Id"", rst.""Tier"", rst.""Name"", rst.""Description"", rst.""Color"", rst.""Icon"",
                rst.""MaxUsers"", rst.""MaxModerators"", rst.""MaxAdmins"", rst.""MaxMic"", rst.""MaxCam"",
                rst.""CanHavePassword"", rst.""CanBe18Plus"", rst.""CanHaveSubRooms"", rst.""MaxSubRooms"",
                rst.""CanCustomizeBanner"", rst.""CanCustomizeBackground"", rst.""HasPriorityListing"",
                rst.""CanUseBot"", rst.""StorageLimitMB"", rst.""AlwaysOnline"",
                rst.""MonthlyPriceCents"", rst.""YearlyPriceCents"", rst.""IsAvailable"",
                rst.""CreatedAt"", rst.""UpdatedAt"",
                (SELECT COUNT(*) FROM ""RoomSubscriptions"" rs WHERE rs.""TierId"" = rst.""Id"" AND rs.""IsActive"" = true) as ActiveSubscriptions
            FROM ""RoomSubscriptionTiers"" rst
            ORDER BY rst.""Tier""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tiers.Add(new RoomSubscriptionTierDto
            {
                Id = reader.GetInt32(0),
                Tier = reader.GetInt32(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                Color = reader.GetString(4),
                Icon = reader.IsDBNull(5) ? null : reader.GetString(5),
                MaxUsers = reader.GetInt32(6),
                MaxModerators = reader.GetInt32(7),
                MaxAdmins = reader.GetInt32(8),
                MaxMic = reader.GetInt32(9),
                MaxCam = reader.GetInt32(10),
                CanHavePassword = reader.GetBoolean(11),
                CanBe18Plus = reader.GetBoolean(12),
                CanHaveSubRooms = reader.GetBoolean(13),
                MaxSubRooms = reader.GetInt32(14),
                CanCustomizeBanner = reader.GetBoolean(15),
                CanCustomizeBackground = reader.GetBoolean(16),
                HasPriorityListing = reader.GetBoolean(17),
                CanUseBot = reader.GetBoolean(18),
                StorageLimitMB = reader.GetInt32(19),
                AlwaysOnline = reader.GetBoolean(20),
                MonthlyPriceCents = reader.GetInt32(21),
                YearlyPriceCents = reader.GetInt32(22),
                IsAvailable = reader.GetBoolean(23),
                CreatedAt = reader.GetDateTime(24),
                UpdatedAt = reader.IsDBNull(25) ? null : reader.GetDateTime(25),
                ActiveSubscriptions = reader.GetInt64(26)
            });
        }

        return tiers;
    }

    public async Task<RoomSubscriptionTierDto?> GetTierByIdAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT 
                rst.""Id"", rst.""Tier"", rst.""Name"", rst.""Description"", rst.""Color"", rst.""Icon"",
                rst.""MaxUsers"", rst.""MaxModerators"", rst.""MaxAdmins"", rst.""MaxMic"", rst.""MaxCam"",
                rst.""CanHavePassword"", rst.""CanBe18Plus"", rst.""CanHaveSubRooms"", rst.""MaxSubRooms"",
                rst.""CanCustomizeBanner"", rst.""CanCustomizeBackground"", rst.""HasPriorityListing"",
                rst.""CanUseBot"", rst.""StorageLimitMB"", rst.""AlwaysOnline"",
                rst.""MonthlyPriceCents"", rst.""YearlyPriceCents"", rst.""IsAvailable"",
                rst.""CreatedAt"", rst.""UpdatedAt"",
                (SELECT COUNT(*) FROM ""RoomSubscriptions"" rs WHERE rs.""TierId"" = rst.""Id"" AND rs.""IsActive"" = true) as ActiveSubscriptions
            FROM ""RoomSubscriptionTiers"" rst
            WHERE rst.""Id"" = @Id";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new RoomSubscriptionTierDto
            {
                Id = reader.GetInt32(0),
                Tier = reader.GetInt32(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                Color = reader.GetString(4),
                Icon = reader.IsDBNull(5) ? null : reader.GetString(5),
                MaxUsers = reader.GetInt32(6),
                MaxModerators = reader.GetInt32(7),
                MaxAdmins = reader.GetInt32(8),
                MaxMic = reader.GetInt32(9),
                MaxCam = reader.GetInt32(10),
                CanHavePassword = reader.GetBoolean(11),
                CanBe18Plus = reader.GetBoolean(12),
                CanHaveSubRooms = reader.GetBoolean(13),
                MaxSubRooms = reader.GetInt32(14),
                CanCustomizeBanner = reader.GetBoolean(15),
                CanCustomizeBackground = reader.GetBoolean(16),
                HasPriorityListing = reader.GetBoolean(17),
                CanUseBot = reader.GetBoolean(18),
                StorageLimitMB = reader.GetInt32(19),
                AlwaysOnline = reader.GetBoolean(20),
                MonthlyPriceCents = reader.GetInt32(21),
                YearlyPriceCents = reader.GetInt32(22),
                IsAvailable = reader.GetBoolean(23),
                CreatedAt = reader.GetDateTime(24),
                UpdatedAt = reader.IsDBNull(25) ? null : reader.GetDateTime(25),
                ActiveSubscriptions = reader.GetInt64(26)
            };
        }

        return null;
    }

    public async Task<bool> UpdateTierAsync(int id, UpdateRoomTierDto dto)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            UPDATE ""RoomSubscriptionTiers"" SET
                ""Name"" = @Name,
                ""Description"" = @Description,
                ""Color"" = @Color,
                ""Icon"" = @Icon,
                ""MaxUsers"" = @MaxUsers,
                ""MaxModerators"" = @MaxModerators,
                ""MaxAdmins"" = @MaxAdmins,
                ""MaxMic"" = @MaxMic,
                ""MaxCam"" = @MaxCam,
                ""CanHavePassword"" = @CanHavePassword,
                ""CanBe18Plus"" = @CanBe18Plus,
                ""CanHaveSubRooms"" = @CanHaveSubRooms,
                ""MaxSubRooms"" = @MaxSubRooms,
                ""CanCustomizeBanner"" = @CanCustomizeBanner,
                ""CanCustomizeBackground"" = @CanCustomizeBackground,
                ""HasPriorityListing"" = @HasPriorityListing,
                ""CanUseBot"" = @CanUseBot,
                ""StorageLimitMB"" = @StorageLimitMB,
                ""AlwaysOnline"" = @AlwaysOnline,
                ""MonthlyPriceCents"" = @MonthlyPriceCents,
                ""YearlyPriceCents"" = @YearlyPriceCents,
                ""IsAvailable"" = @IsAvailable,
                ""UpdatedAt"" = NOW()
            WHERE ""Id"" = @Id";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Name", dto.Name);
        cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Color", dto.Color);
        cmd.Parameters.AddWithValue("@Icon", (object?)dto.Icon ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MaxUsers", dto.MaxUsers);
        cmd.Parameters.AddWithValue("@MaxModerators", dto.MaxModerators);
        cmd.Parameters.AddWithValue("@MaxAdmins", dto.MaxAdmins);
        cmd.Parameters.AddWithValue("@MaxMic", dto.MaxMic);
        cmd.Parameters.AddWithValue("@MaxCam", dto.MaxCam);
        cmd.Parameters.AddWithValue("@CanHavePassword", dto.CanHavePassword);
        cmd.Parameters.AddWithValue("@CanBe18Plus", dto.CanBe18Plus);
        cmd.Parameters.AddWithValue("@CanHaveSubRooms", dto.CanHaveSubRooms);
        cmd.Parameters.AddWithValue("@MaxSubRooms", dto.MaxSubRooms);
        cmd.Parameters.AddWithValue("@CanCustomizeBanner", dto.CanCustomizeBanner);
        cmd.Parameters.AddWithValue("@CanCustomizeBackground", dto.CanCustomizeBackground);
        cmd.Parameters.AddWithValue("@HasPriorityListing", dto.HasPriorityListing);
        cmd.Parameters.AddWithValue("@CanUseBot", dto.CanUseBot);
        cmd.Parameters.AddWithValue("@StorageLimitMB", dto.StorageLimitMB);
        cmd.Parameters.AddWithValue("@AlwaysOnline", dto.AlwaysOnline);
        cmd.Parameters.AddWithValue("@MonthlyPriceCents", dto.MonthlyPriceCents);
        cmd.Parameters.AddWithValue("@YearlyPriceCents", dto.YearlyPriceCents);
        cmd.Parameters.AddWithValue("@IsAvailable", dto.IsAvailable);

        var result = await cmd.ExecuteNonQueryAsync();
        return result > 0;
    }

    // ========================= ROOM SUBSCRIPTIONS =========================

    public async Task<List<RoomSubscriptionDto>> GetAllRoomSubscriptionsAsync()
    {
        var subscriptions = new List<RoomSubscriptionDto>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT 
                rs.""Id"", rs.""RoomId"", rs.""TierId"", rs.""PurchasedBy"",
                rs.""StartedAt"", rs.""ExpiresAt"", rs.""IsActive"", rs.""AutoRenew"",
                rs.""PaymentMethod"", rs.""TransactionId"", rs.""CreatedAt"", rs.""UpdatedAt"",
                r.""Name"" as RoomName,
                rst.""Name"" as TierName, rst.""Color"" as TierColor,
                u.""Username"" as PurchasedByUsername
            FROM ""RoomSubscriptions"" rs
            INNER JOIN ""Rooms"" r ON rs.""RoomId"" = r.""Id""
            INNER JOIN ""RoomSubscriptionTiers"" rst ON rs.""TierId"" = rst.""Id""
            LEFT JOIN ""Users"" u ON rs.""PurchasedBy"" = u.""Id""
            ORDER BY rs.""CreatedAt"" DESC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            subscriptions.Add(new RoomSubscriptionDto
            {
                Id = reader.GetInt32(0),
                RoomId = reader.GetInt32(1),
                TierId = reader.GetInt32(2),
                PurchasedBy = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                StartedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                ExpiresAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                IsActive = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                AutoRenew = reader.IsDBNull(7) ? false : reader.GetBoolean(7),
                PaymentMethod = reader.IsDBNull(8) ? null : reader.GetString(8),
                TransactionId = reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                UpdatedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                RoomName = reader.GetString(12),
                TierName = reader.GetString(13),
                TierColor = reader.GetString(14),
                PurchasedByUsername = reader.IsDBNull(15) ? null : reader.GetString(15)
            });
        }

        return subscriptions;
    }

    public async Task<RoomSubscriptionDto?> GetRoomSubscriptionAsync(int roomId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT 
                rs.""Id"", rs.""RoomId"", rs.""TierId"", rs.""PurchasedBy"",
                rs.""StartedAt"", rs.""ExpiresAt"", rs.""IsActive"", rs.""AutoRenew"",
                rs.""PaymentMethod"", rs.""TransactionId"", rs.""CreatedAt"", rs.""UpdatedAt"",
                r.""Name"" as RoomName,
                rst.""Name"" as TierName, rst.""Color"" as TierColor,
                u.""Username"" as PurchasedByUsername
            FROM ""RoomSubscriptions"" rs
            INNER JOIN ""Rooms"" r ON rs.""RoomId"" = r.""Id""
            INNER JOIN ""RoomSubscriptionTiers"" rst ON rs.""TierId"" = rst.""Id""
            LEFT JOIN ""Users"" u ON rs.""PurchasedBy"" = u.""Id""
            WHERE rs.""RoomId"" = @RoomId
            ORDER BY rs.""CreatedAt"" DESC
            LIMIT 1";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@RoomId", roomId);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new RoomSubscriptionDto
            {
                Id = reader.GetInt32(0),
                RoomId = reader.GetInt32(1),
                TierId = reader.GetInt32(2),
                PurchasedBy = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                StartedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                ExpiresAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                IsActive = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                AutoRenew = reader.IsDBNull(7) ? false : reader.GetBoolean(7),
                PaymentMethod = reader.IsDBNull(8) ? null : reader.GetString(8),
                TransactionId = reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                UpdatedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                RoomName = reader.GetString(12),
                TierName = reader.GetString(13),
                TierColor = reader.GetString(14),
                PurchasedByUsername = reader.IsDBNull(15) ? null : reader.GetString(15)
            };
        }

        return null;
    }

    public async Task<bool> GrantRoomSubscriptionAsync(GrantRoomSubscriptionDto dto)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if room already has an active subscription
        var checkSql = @"SELECT COUNT(*) FROM ""RoomSubscriptions"" WHERE ""RoomId"" = @RoomId AND ""IsActive"" = true";
        await using var checkCmd = new NpgsqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@RoomId", dto.RoomId);
        var existingCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());

        if (existingCount > 0)
        {
            // Deactivate existing subscription
            var deactivateSql = @"UPDATE ""RoomSubscriptions"" SET ""IsActive"" = false, ""UpdatedAt"" = NOW() WHERE ""RoomId"" = @RoomId AND ""IsActive"" = true";
            await using var deactivateCmd = new NpgsqlCommand(deactivateSql, conn);
            deactivateCmd.Parameters.AddWithValue("@RoomId", dto.RoomId);
            await deactivateCmd.ExecuteNonQueryAsync();
        }

        var expiresAt = dto.DurationDays.HasValue 
            ? DateTime.UtcNow.AddDays(dto.DurationDays.Value) 
            : (DateTime?)null;

        var sql = @"
            INSERT INTO ""RoomSubscriptions"" 
                (""RoomId"", ""TierId"", ""PurchasedBy"", ""StartedAt"", ""ExpiresAt"", ""IsActive"", ""AutoRenew"", ""PaymentMethod"", ""TransactionId"", ""CreatedAt"", ""UpdatedAt"")
            VALUES 
                (@RoomId, @TierId, @PurchasedBy, NOW(), @ExpiresAt, true, @AutoRenew, @PaymentMethod, @TransactionId, NOW(), NOW())";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@RoomId", dto.RoomId);
        cmd.Parameters.AddWithValue("@TierId", dto.TierId);
        cmd.Parameters.AddWithValue("@PurchasedBy", (object?)dto.GrantedByAdminId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ExpiresAt", (object?)expiresAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AutoRenew", false);
        cmd.Parameters.AddWithValue("@PaymentMethod", "admin_grant");
        cmd.Parameters.AddWithValue("@TransactionId", $"ADMIN-{DateTime.UtcNow:yyyyMMddHHmmss}-{dto.RoomId}");

        var result = await cmd.ExecuteNonQueryAsync();
        return result > 0;
    }

    public async Task<bool> RevokeRoomSubscriptionAsync(int roomId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"UPDATE ""RoomSubscriptions"" SET ""IsActive"" = false, ""UpdatedAt"" = NOW() WHERE ""RoomId"" = @RoomId AND ""IsActive"" = true";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@RoomId", roomId);

        var result = await cmd.ExecuteNonQueryAsync();
        return result > 0;
    }

    public async Task<bool> ExtendRoomSubscriptionAsync(int roomId, int additionalDays)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            UPDATE ""RoomSubscriptions"" SET 
                ""ExpiresAt"" = CASE 
                    WHEN ""ExpiresAt"" IS NULL THEN NOW() + INTERVAL '1 day' * @Days
                    WHEN ""ExpiresAt"" < NOW() THEN NOW() + INTERVAL '1 day' * @Days
                    ELSE ""ExpiresAt"" + INTERVAL '1 day' * @Days
                END,
                ""UpdatedAt"" = NOW()
            WHERE ""RoomId"" = @RoomId AND ""IsActive"" = true";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@RoomId", roomId);
        cmd.Parameters.AddWithValue("@Days", additionalDays);

        var result = await cmd.ExecuteNonQueryAsync();
        return result > 0;
    }

    // ========================= ROOMS SEARCH =========================

    public async Task<List<RoomSearchResultDto>> SearchRoomsAsync(string? query, int limit = 50)
    {
        var rooms = new List<RoomSearchResultDto>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT 
                r.""Id"", r.""Name"", r.""OwnerId"", r.""CreatedAt"",
                u.""Username"" as OwnerUsername,
                rs.""TierId"", rst.""Name"" as CurrentTierName, rst.""Color"" as CurrentTierColor, rs.""IsActive"", rs.""ExpiresAt""
            FROM ""Rooms"" r
            LEFT JOIN ""Users"" u ON r.""OwnerId"" = u.""Id""
            LEFT JOIN ""RoomSubscriptions"" rs ON r.""Id"" = rs.""RoomId"" AND rs.""IsActive"" = true
            LEFT JOIN ""RoomSubscriptionTiers"" rst ON rs.""TierId"" = rst.""Id""
            WHERE (@Query IS NULL OR r.""Name"" ILIKE '%' || @Query || '%' OR CAST(r.""Id"" AS TEXT) = @Query)
            ORDER BY r.""Name""
            LIMIT @Limit";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Query", (object?)query ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rooms.Add(new RoomSearchResultDto
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                OwnerId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                CreatedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                OwnerUsername = reader.IsDBNull(4) ? null : reader.GetString(4),
                CurrentTierId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                CurrentTierName = reader.IsDBNull(6) ? null : reader.GetString(6),
                CurrentTierColor = reader.IsDBNull(7) ? null : reader.GetString(7),
                HasActiveSubscription = reader.IsDBNull(8) ? false : reader.GetBoolean(8),
                SubscriptionExpiresAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
        }

        return rooms;
    }

    // ========================= STATISTICS =========================

    public async Task<RoomSubscriptionStatsDto> GetStatsAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var stats = new RoomSubscriptionStatsDto();

        // Total tiers
        var tierCountSql = @"SELECT COUNT(*) FROM ""RoomSubscriptionTiers""";
        await using var tierCountCmd = new NpgsqlCommand(tierCountSql, conn);
        stats.TotalTiers = Convert.ToInt32(await tierCountCmd.ExecuteScalarAsync());

        // Total rooms with active subscriptions
        var activeSubsSql = @"SELECT COUNT(DISTINCT ""RoomId"") FROM ""RoomSubscriptions"" WHERE ""IsActive"" = true";
        await using var activeSubsCmd = new NpgsqlCommand(activeSubsSql, conn);
        stats.ActiveSubscriptions = Convert.ToInt32(await activeSubsCmd.ExecuteScalarAsync());

        // Subscriptions expiring soon (within 7 days)
        var expiringSoonSql = @"SELECT COUNT(*) FROM ""RoomSubscriptions"" WHERE ""IsActive"" = true AND ""ExpiresAt"" IS NOT NULL AND ""ExpiresAt"" <= NOW() + INTERVAL '7 days'";
        await using var expiringSoonCmd = new NpgsqlCommand(expiringSoonSql, conn);
        stats.ExpiringSoon = Convert.ToInt32(await expiringSoonCmd.ExecuteScalarAsync());

        // Subscription by tier breakdown
        var tierBreakdownSql = @"
            SELECT rst.""Name"", rst.""Color"", COUNT(rs.""Id"") as Count
            FROM ""RoomSubscriptionTiers"" rst
            LEFT JOIN ""RoomSubscriptions"" rs ON rst.""Id"" = rs.""TierId"" AND rs.""IsActive"" = true
            GROUP BY rst.""Id"", rst.""Name"", rst.""Color""
            ORDER BY rst.""Tier""";
        await using var tierBreakdownCmd = new NpgsqlCommand(tierBreakdownSql, conn);
        await using var tierBreakdownReader = await tierBreakdownCmd.ExecuteReaderAsync();

        stats.SubscriptionsByTier = new List<TierBreakdownDto>();
        while (await tierBreakdownReader.ReadAsync())
        {
            stats.SubscriptionsByTier.Add(new TierBreakdownDto
            {
                TierName = tierBreakdownReader.GetString(0),
                TierColor = tierBreakdownReader.GetString(1),
                Count = Convert.ToInt32(tierBreakdownReader.GetInt64(2))
            });
        }

        return stats;
    }
}

// ========================= DTOs =========================

public class RoomSubscriptionTierDto
{
    public int Id { get; set; }
    public int Tier { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int MaxUsers { get; set; }
    public int MaxModerators { get; set; }
    public int MaxAdmins { get; set; }
    public int MaxMic { get; set; }
    public int MaxCam { get; set; }
    public bool CanHavePassword { get; set; }
    public bool CanBe18Plus { get; set; }
    public bool CanHaveSubRooms { get; set; }
    public int MaxSubRooms { get; set; }
    public bool CanCustomizeBanner { get; set; }
    public bool CanCustomizeBackground { get; set; }
    public bool HasPriorityListing { get; set; }
    public bool CanUseBot { get; set; }
    public int StorageLimitMB { get; set; }
    public bool AlwaysOnline { get; set; }
    public int MonthlyPriceCents { get; set; }
    public int YearlyPriceCents { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long ActiveSubscriptions { get; set; }
}

public class UpdateRoomTierDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int MaxUsers { get; set; }
    public int MaxModerators { get; set; }
    public int MaxAdmins { get; set; }
    public int MaxMic { get; set; }
    public int MaxCam { get; set; }
    public bool CanHavePassword { get; set; }
    public bool CanBe18Plus { get; set; }
    public bool CanHaveSubRooms { get; set; }
    public int MaxSubRooms { get; set; }
    public bool CanCustomizeBanner { get; set; }
    public bool CanCustomizeBackground { get; set; }
    public bool HasPriorityListing { get; set; }
    public bool CanUseBot { get; set; }
    public int StorageLimitMB { get; set; }
    public bool AlwaysOnline { get; set; }
    public int MonthlyPriceCents { get; set; }
    public int YearlyPriceCents { get; set; }
    public bool IsAvailable { get; set; }
}

public class RoomSubscriptionDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int TierId { get; set; }
    public int? PurchasedBy { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public bool AutoRenew { get; set; }
    public string? PaymentMethod { get; set; }
    public string? TransactionId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string TierName { get; set; } = string.Empty;
    public string TierColor { get; set; } = string.Empty;
    public string? PurchasedByUsername { get; set; }
}

public class GrantRoomSubscriptionDto
{
    public int RoomId { get; set; }
    public int TierId { get; set; }
    public int? GrantedByAdminId { get; set; }
    public int? DurationDays { get; set; }
}

public class RoomSearchResultDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? OwnerId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? OwnerUsername { get; set; }
    public int? CurrentTierId { get; set; }
    public string? CurrentTierName { get; set; }
    public string? CurrentTierColor { get; set; }
    public bool HasActiveSubscription { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
}

public class RoomSubscriptionStatsDto
{
    public int TotalTiers { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int ExpiringSoon { get; set; }
    public List<TierBreakdownDto> SubscriptionsByTier { get; set; } = new();
}

public class TierBreakdownDto
{
    public string TierName { get; set; } = string.Empty;
    public string TierColor { get; set; } = string.Empty;
    public int Count { get; set; }
}
