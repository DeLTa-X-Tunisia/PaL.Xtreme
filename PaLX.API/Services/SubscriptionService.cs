using Npgsql;
using PaLX.API.DTOs;

namespace PaLX.API.Services
{
    public interface ISubscriptionService
    {
        // Tiers
        Task<List<SubscriptionTierDto>> GetTiersAsync();
        Task<SubscriptionTierDto?> GetTierByIdAsync(int id);
        Task<ServiceResult> UpdateTierAsync(int id, UpdateTierDto dto);
        
        // Durées
        Task<List<SubscriptionDurationDto>> GetDurationsAsync();
        Task<ServiceResult> UpdateDurationAsync(int id, UpdateDurationDto dto);
        
        // Prix
        Task<List<SubscriptionPriceDto>> GetAllPricesAsync();
        Task<SubscriptionPriceDto?> GetPriceAsync(int tierId, int durationId);
        Task<ServiceResult> SetCustomPriceAsync(int tierId, int durationId, int priceCents, int points);
        Task<ServiceResult> ResetToCalculatedPriceAsync(int tierId, int durationId);
        
        // Abonnements utilisateur
        Task<List<UserSubscriptionDto>> GetUserSubscriptionsAsync(int page, int pageSize, int? tierId, bool? isActive);
        Task<UserSubscriptionDto?> GetUserCurrentSubscriptionAsync(int userId);
        Task<ServiceResult> GrantSubscriptionAsync(int userId, int tierId, int durationId, int adminId, string paymentMethod);
        Task<ServiceResult> RevokeSubscriptionAsync(int userId, int adminId, string reason);
        Task<ServiceResult> ExtendSubscriptionAsync(int userId, int days, int adminId);
        
        // Points
        Task<UserPointsDto> GetUserPointsAsync(int userId);
        Task<ServiceResult> GrantPointsAsync(int userId, int amount, string description, int adminId);
        Task<ServiceResult> DeductPointsAsync(int userId, int amount, string description, string transactionType, int? referenceId);
        Task<List<PointTransactionDto>> GetPointHistoryAsync(int userId, int page, int pageSize);
        
        // Période d'essai
        Task<bool> CanUseTrialAsync(int userId, int tierId);
        Task<ServiceResult> ActivateTrialAsync(int userId, int tierId);
        
        // Statistiques
        Task<SubscriptionStatsDto> GetStatsAsync();
    }

    public class SubscriptionService : ISubscriptionService
    {
        private readonly string _connectionString;
        private readonly ILogger<SubscriptionService> _logger;
        private const int TRIAL_DAYS = 3;

        public SubscriptionService(IConfiguration configuration, ILogger<SubscriptionService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string not found.");
            _logger = logger;
        }

        // ============================================
        // TIERS (Structure: Id, Tier, Name, Description, Color, Icon, MonthlyPriceCents, YearlyPriceCents, IsAvailable)
        // ============================================

        public async Task<List<SubscriptionTierDto>> GetTiersAsync()
        {
            var tiers = new List<SubscriptionTierDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT st.""Id"", st.""Tier"", st.""Name"", st.""Description"", st.""Color"", st.""Icon"",
                       st.""MonthlyPriceCents"", st.""YearlyPriceCents"", st.""IsAvailable"",
                       (SELECT COUNT(*) FROM ""UserSubscriptions"" us 
                        WHERE us.""TierId"" = st.""Id"" 
                        AND us.""IsActive"" = true
                        AND (us.""ExpiresAt"" IS NULL OR us.""ExpiresAt"" > NOW())) as ActiveUsers
                FROM ""SubscriptionTiers"" st
                ORDER BY st.""Tier""";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                tiers.Add(new SubscriptionTierDto
                {
                    Id = reader.GetInt32(0),
                    Tier = reader.GetInt32(1),
                    Name = reader.GetString(2),
                    DisplayName = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Color = reader.GetString(4),
                    Icon = reader.GetString(5),
                    MonthlyPriceCents = reader.GetInt32(6),
                    YearlyPriceCents = reader.GetInt32(7),
                    BasePricePerDayCents = reader.GetInt32(6) / 30,
                    BasePointsPerDay = reader.GetInt32(6) / 30,
                    IsAvailable = reader.GetBoolean(8),
                    ActiveUsersCount = reader.GetInt32(9)
                });
            }

            return tiers;
        }

        public async Task<SubscriptionTierDto?> GetTierByIdAsync(int id)
        {
            var tiers = await GetTiersAsync();
            return tiers.FirstOrDefault(t => t.Id == id);
        }

        public async Task<ServiceResult> UpdateTierAsync(int id, UpdateTierDto dto)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = @"
                    UPDATE ""SubscriptionTiers"" 
                    SET ""Name"" = COALESCE(@Name, ""Name""),
                        ""Description"" = COALESCE(@Description, ""Description""),
                        ""Color"" = COALESCE(@Color, ""Color""),
                        ""MonthlyPriceCents"" = COALESCE(@MonthlyPriceCents, ""MonthlyPriceCents""),
                        ""YearlyPriceCents"" = COALESCE(@YearlyPriceCents, ""YearlyPriceCents""),
                        ""IsAvailable"" = COALESCE(@IsAvailable, ""IsAvailable""),
                        ""UpdatedAt"" = NOW()
                    WHERE ""Id"" = @Id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Name", (object?)dto.DisplayName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Color", (object?)dto.Color ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MonthlyPriceCents", (object?)dto.MonthlyPriceCents ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@YearlyPriceCents", (object?)dto.YearlyPriceCents ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IsAvailable", (object?)dto.IsAvailable ?? DBNull.Value);

                var affected = await cmd.ExecuteNonQueryAsync();
                return affected > 0 
                    ? ServiceResult.Ok("Tier mis à jour avec succès")
                    : ServiceResult.NotFound("Tier non trouvé");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la mise à jour du tier {TierId}", id);
                return ServiceResult.Error("Erreur lors de la mise à jour");
            }
        }

        // ============================================
        // DURATIONS
        // ============================================

        public async Task<List<SubscriptionDurationDto>> GetDurationsAsync()
        {
            var durations = new List<SubscriptionDurationDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT ""Id"", ""Name"", ""DisplayName"", ""BaseDays"", ""BonusDays"", ""TotalDays"", 
                       ""DiscountPercent"", ""IsAvailable"", ""SortOrder""
                FROM ""SubscriptionDurations""
                ORDER BY ""SortOrder""";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                durations.Add(new SubscriptionDurationDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    DisplayName = reader.GetString(2),
                    BaseDays = reader.GetInt32(3),
                    BonusDays = reader.GetInt32(4),
                    TotalDays = reader.GetInt32(5),
                    DiscountPercent = reader.GetInt32(6),
                    IsAvailable = reader.GetBoolean(7)
                });
            }

            return durations;
        }

        public async Task<ServiceResult> UpdateDurationAsync(int id, UpdateDurationDto dto)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // Construire la requête dynamiquement pour éviter les problèmes avec COALESCE
                // NOTE: TotalDays est une colonne générée (BaseDays + BonusDays), on ne peut pas la modifier
                var updates = new List<string>();
                var cmd = new NpgsqlCommand();
                cmd.Connection = conn;
                
                if (dto.DisplayName != null)
                {
                    updates.Add(@"""DisplayName"" = @DisplayName");
                    cmd.Parameters.AddWithValue("@DisplayName", dto.DisplayName);
                }
                if (dto.BonusDays.HasValue)
                {
                    updates.Add(@"""BonusDays"" = @BonusDays");
                    // TotalDays est une colonne générée, ne pas la mettre à jour manuellement
                    cmd.Parameters.AddWithValue("@BonusDays", dto.BonusDays.Value);
                }
                if (dto.DiscountPercent.HasValue)
                {
                    updates.Add(@"""DiscountPercent"" = @DiscountPercent");
                    cmd.Parameters.AddWithValue("@DiscountPercent", dto.DiscountPercent.Value);
                }
                if (dto.IsAvailable.HasValue)
                {
                    updates.Add(@"""IsAvailable"" = @IsAvailable");
                    cmd.Parameters.AddWithValue("@IsAvailable", dto.IsAvailable.Value);
                }

                if (updates.Count == 0)
                    return ServiceResult.Ok("Aucune modification");

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.CommandText = $@"UPDATE ""SubscriptionDurations"" SET {string.Join(", ", updates)} WHERE ""Id"" = @Id";

                var affected = await cmd.ExecuteNonQueryAsync();
                return affected > 0 
                    ? ServiceResult.Ok("Durée mise à jour avec succès")
                    : ServiceResult.NotFound("Durée non trouvée");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la mise à jour de la durée {DurationId}", id);
                return ServiceResult.Error("Erreur lors de la mise à jour");
            }
        }

        // ============================================
        // PRICES
        // ============================================

        public async Task<List<SubscriptionPriceDto>> GetAllPricesAsync()
        {
            var prices = new List<SubscriptionPriceDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT 
                    t.""Id"" as TierId, t.""Name"" as TierName, t.""Name"" as TierDisplayName, t.""Color"",
                    d.""Id"" as DurationId, d.""Name"" as DurationName, d.""DisplayName"" as DurationDisplayName,
                    d.""BaseDays"", d.""BonusDays"", d.""DiscountPercent"",
                    COALESCE(p.""PriceCents"", 
                        CASE 
                            WHEN t.""Tier"" = 0 THEN 0
                            ELSE ((t.""MonthlyPriceCents"" * d.""TotalDays"" / 30) * (100 - d.""DiscountPercent"") / 100)::int
                        END
                    ) as PriceCents,
                    COALESCE(p.""Points"",
                        CASE 
                            WHEN t.""Tier"" = 0 THEN 0
                            ELSE ((t.""MonthlyPriceCents"" * d.""TotalDays"" / 30) * (100 - d.""DiscountPercent"") / 100)::int
                        END
                    ) as Points,
                    CASE WHEN p.""Id"" IS NOT NULL THEN true ELSE false END as IsCustomPrice
                FROM ""SubscriptionTiers"" t
                CROSS JOIN ""SubscriptionDurations"" d
                LEFT JOIN ""SubscriptionPrices"" p ON p.""TierId"" = t.""Id"" AND p.""DurationId"" = d.""Id""
                WHERE t.""Tier"" > 0
                ORDER BY t.""Tier"", d.""SortOrder""";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                prices.Add(new SubscriptionPriceDto
                {
                    TierId = reader.GetInt32(0),
                    TierName = reader.GetString(1),
                    TierDisplayName = reader.GetString(2),
                    TierColor = reader.GetString(3),
                    DurationId = reader.GetInt32(4),
                    DurationName = reader.GetString(5),
                    DurationDisplayName = reader.GetString(6),
                    BaseDays = reader.GetInt32(7),
                    BonusDays = reader.GetInt32(8),
                    DiscountPercent = reader.GetInt32(9),
                    PriceCents = reader.GetInt32(10),
                    Points = reader.GetInt32(11),
                    IsCustomPrice = reader.GetBoolean(12)
                });
            }

            return prices;
        }

        public async Task<SubscriptionPriceDto?> GetPriceAsync(int tierId, int durationId)
        {
            var prices = await GetAllPricesAsync();
            return prices.FirstOrDefault(p => p.TierId == tierId && p.DurationId == durationId);
        }

        public async Task<ServiceResult> SetCustomPriceAsync(int tierId, int durationId, int priceCents, int points)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // Upsert
                var sql = @"
                    INSERT INTO ""SubscriptionPrices"" (""TierId"", ""DurationId"", ""PriceCents"", ""Points"", ""IsCustom"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES (@TierId, @DurationId, @PriceCents, @Points, true, NOW(), NOW())
                    ON CONFLICT (""TierId"", ""DurationId"") 
                    DO UPDATE SET ""PriceCents"" = @PriceCents, ""Points"" = @Points, ""IsCustom"" = true, ""UpdatedAt"" = NOW()";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@TierId", tierId);
                cmd.Parameters.AddWithValue("@DurationId", durationId);
                cmd.Parameters.AddWithValue("@PriceCents", priceCents);
                cmd.Parameters.AddWithValue("@Points", points);

                await cmd.ExecuteNonQueryAsync();
                return ServiceResult.Ok("Prix personnalisé enregistré");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la définition du prix personnalisé");
                return ServiceResult.Error("Erreur lors de l'enregistrement du prix");
            }
        }

        public async Task<ServiceResult> ResetToCalculatedPriceAsync(int tierId, int durationId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = @"DELETE FROM ""SubscriptionPrices"" WHERE ""TierId"" = @TierId AND ""DurationId"" = @DurationId";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@TierId", tierId);
                cmd.Parameters.AddWithValue("@DurationId", durationId);

                await cmd.ExecuteNonQueryAsync();
                return ServiceResult.Ok("Prix réinitialisé au calcul automatique");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la réinitialisation du prix");
                return ServiceResult.Error("Erreur lors de la réinitialisation");
            }
        }

        // ============================================
        // USER SUBSCRIPTIONS (Structure: Id, UserId, TierId, StartedAt, ExpiresAt, IsActive, AutoRenew, PaymentMethod, TransactionId, GrantedByAdminId, PricePaid, PointsUsed, IsTrial)
        // ============================================

        public async Task<List<UserSubscriptionDto>> GetUserSubscriptionsAsync(int page, int pageSize, int? tierId, bool? isActive)
        {
            var subscriptions = new List<UserSubscriptionDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT us.""Id"", us.""UserId"", u.""Username"", u.""FirstName"", u.""LastName"",
                       us.""TierId"", t.""Name"" as TierName, t.""Color"",
                       us.""StartedAt"", us.""ExpiresAt"", us.""IsActive"",
                       us.""PaymentMethod"", us.""PricePaid"", us.""PointsUsed"",
                       us.""CreatedAt"", us.""IsTrial""
                FROM ""UserSubscriptions"" us
                JOIN ""Users"" u ON u.""Id"" = us.""UserId""
                JOIN ""SubscriptionTiers"" t ON t.""Id"" = us.""TierId""
                WHERE 1=1";

            if (tierId.HasValue) sql += " AND us.\"TierId\" = @TierId";
            if (isActive == true) sql += " AND us.\"IsActive\" = true AND (us.\"ExpiresAt\" IS NULL OR us.\"ExpiresAt\" > NOW())";
            if (isActive == false) sql += " AND (us.\"IsActive\" = false OR us.\"ExpiresAt\" <= NOW())";

            sql += @" ORDER BY us.""CreatedAt"" DESC
                      LIMIT @PageSize OFFSET @Offset";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PageSize", pageSize);
            cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
            if (tierId.HasValue) cmd.Parameters.AddWithValue("@TierId", tierId.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var isActiveNow = reader.GetBoolean(10) && 
                    (reader.IsDBNull(9) || reader.GetDateTime(9) > DateTime.UtcNow);
                
                subscriptions.Add(new UserSubscriptionDto
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Username = reader.GetString(2),
                    UserFullName = $"{(reader.IsDBNull(4) ? "" : reader.GetString(4))} {(reader.IsDBNull(3) ? "" : reader.GetString(3))}".Trim(),
                    TierId = reader.GetInt32(5),
                    TierName = reader.GetString(6),
                    TierColor = reader.GetString(7),
                    StartDate = reader.GetDateTime(8),
                    EndDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    Status = isActiveNow ? "Active" : "Expired",
                    PaymentMethod = reader.IsDBNull(11) ? null : reader.GetString(11),
                    PaidAmountCents = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                    PointsUsed = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                    CreatedAt = reader.GetDateTime(14),
                    IsTrial = reader.IsDBNull(15) ? false : reader.GetBoolean(15)
                });
            }

            return subscriptions;
        }

        public async Task<UserSubscriptionDto?> GetUserCurrentSubscriptionAsync(int userId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT us.""Id"", us.""UserId"", u.""Username"", u.""FirstName"", u.""LastName"",
                       us.""TierId"", t.""Name"" as TierName, t.""Color"",
                       us.""StartedAt"", us.""ExpiresAt"", us.""IsActive"",
                       us.""PaymentMethod"", us.""PricePaid"", us.""PointsUsed"",
                       us.""CreatedAt"", us.""IsTrial""
                FROM ""UserSubscriptions"" us
                JOIN ""Users"" u ON u.""Id"" = us.""UserId""
                JOIN ""SubscriptionTiers"" t ON t.""Id"" = us.""TierId""
                WHERE us.""UserId"" = @UserId
                  AND us.""IsActive"" = true
                  AND (us.""ExpiresAt"" IS NULL OR us.""ExpiresAt"" > NOW())
                ORDER BY us.""CreatedAt"" DESC
                LIMIT 1";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            using var reader = await cmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return new UserSubscriptionDto
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Username = reader.GetString(2),
                    UserFullName = $"{(reader.IsDBNull(4) ? "" : reader.GetString(4))} {(reader.IsDBNull(3) ? "" : reader.GetString(3))}".Trim(),
                    TierId = reader.GetInt32(5),
                    TierName = reader.GetString(6),
                    TierColor = reader.GetString(7),
                    StartDate = reader.GetDateTime(8),
                    EndDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    Status = "Active",
                    PaymentMethod = reader.IsDBNull(11) ? null : reader.GetString(11),
                    PaidAmountCents = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                    PointsUsed = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                    CreatedAt = reader.GetDateTime(14),
                    IsTrial = reader.IsDBNull(15) ? false : reader.GetBoolean(15)
                };
            }

            return null;
        }

        public async Task<ServiceResult> GrantSubscriptionAsync(int userId, int tierId, int durationId, int adminId, string paymentMethod)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = await conn.BeginTransactionAsync();

                // Récupérer la durée
                var durationSql = @"SELECT ""TotalDays"" FROM ""SubscriptionDurations"" WHERE ""Id"" = @DurationId";
                using var durationCmd = new NpgsqlCommand(durationSql, conn, transaction);
                durationCmd.Parameters.AddWithValue("@DurationId", durationId);
                var totalDays = (int?)await durationCmd.ExecuteScalarAsync();
                if (totalDays == null)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.NotFound("Durée non trouvée");
                }

                // Récupérer le prix
                var price = await GetPriceAsync(tierId, durationId);
                if (price == null)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.NotFound("Prix non trouvé");
                }

                // Désactiver les abonnements actifs existants
                var deactivateSql = @"
                    UPDATE ""UserSubscriptions"" 
                    SET ""IsActive"" = false, ""UpdatedAt"" = NOW()
                    WHERE ""UserId"" = @UserId AND ""IsActive"" = true";
                using var deactivateCmd = new NpgsqlCommand(deactivateSql, conn, transaction);
                deactivateCmd.Parameters.AddWithValue("@UserId", userId);
                await deactivateCmd.ExecuteNonQueryAsync();

                // Créer le nouvel abonnement
                var insertSql = @"
                    INSERT INTO ""UserSubscriptions"" 
                    (""UserId"", ""TierId"", ""StartedAt"", ""ExpiresAt"", ""IsActive"", ""AutoRenew"",
                     ""PaymentMethod"", ""PricePaid"", ""PointsUsed"", ""GrantedByAdminId"", ""IsTrial"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES (@UserId, @TierId, NOW(), NOW() + @Days * INTERVAL '1 day', true, false,
                            @PaymentMethod, @PricePaid, @PointsUsed, @AdminId, false, NOW(), NOW())
                    RETURNING ""Id""";

                using var insertCmd = new NpgsqlCommand(insertSql, conn, transaction);
                insertCmd.Parameters.AddWithValue("@UserId", userId);
                insertCmd.Parameters.AddWithValue("@TierId", tierId);
                insertCmd.Parameters.AddWithValue("@Days", totalDays.Value);
                insertCmd.Parameters.AddWithValue("@PaymentMethod", paymentMethod);
                insertCmd.Parameters.AddWithValue("@PricePaid", paymentMethod == "Points" ? 0 : price.PriceCents);
                insertCmd.Parameters.AddWithValue("@PointsUsed", paymentMethod == "Points" ? price.Points : 0);
                insertCmd.Parameters.AddWithValue("@AdminId", adminId);

                var subscriptionId = (int?)await insertCmd.ExecuteScalarAsync();

                // Mettre à jour le TierId de l'utilisateur
                var updateUserSql = @"UPDATE ""Users"" SET ""TierId"" = @TierId WHERE ""Id"" = @UserId";
                using var updateUserCmd = new NpgsqlCommand(updateUserSql, conn, transaction);
                updateUserCmd.Parameters.AddWithValue("@TierId", tierId);
                updateUserCmd.Parameters.AddWithValue("@UserId", userId);
                await updateUserCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Abonnement accordé à l'utilisateur {UserId} par l'admin {AdminId}: Tier={TierId}, Duration={DurationId}", 
                    userId, adminId, tierId, durationId);

                return ServiceResult.Ok($"Abonnement accordé avec succès (ID: {subscriptionId})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'attribution de l'abonnement");
                return ServiceResult.Error("Erreur lors de l'attribution de l'abonnement");
            }
        }

        public async Task<ServiceResult> RevokeSubscriptionAsync(int userId, int adminId, string reason)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = await conn.BeginTransactionAsync();

                var sql = @"
                    UPDATE ""UserSubscriptions"" 
                    SET ""IsActive"" = false, ""UpdatedAt"" = NOW()
                    WHERE ""UserId"" = @UserId AND ""IsActive"" = true";

                using var cmd = new NpgsqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("@UserId", userId);
                var affected = await cmd.ExecuteNonQueryAsync();

                // Remettre l'utilisateur en Member (TierId = 1 = Tier 0)
                var updateUserSql = @"UPDATE ""Users"" SET ""TierId"" = (SELECT ""Id"" FROM ""SubscriptionTiers"" WHERE ""Tier"" = 0 LIMIT 1) WHERE ""Id"" = @UserId";
                using var updateUserCmd = new NpgsqlCommand(updateUserSql, conn, transaction);
                updateUserCmd.Parameters.AddWithValue("@UserId", userId);
                await updateUserCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Abonnement révoqué pour l'utilisateur {UserId} par l'admin {AdminId}. Raison: {Reason}", 
                    userId, adminId, reason);

                return affected > 0 
                    ? ServiceResult.Ok("Abonnement révoqué avec succès")
                    : ServiceResult.NotFound("Aucun abonnement actif trouvé");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la révocation de l'abonnement");
                return ServiceResult.Error("Erreur lors de la révocation");
            }
        }

        public async Task<ServiceResult> ExtendSubscriptionAsync(int userId, int days, int adminId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = @"
                    UPDATE ""UserSubscriptions"" 
                    SET ""ExpiresAt"" = COALESCE(""ExpiresAt"", NOW()) + @Days * INTERVAL '1 day',
                        ""UpdatedAt"" = NOW()
                    WHERE ""UserId"" = @UserId AND ""IsActive"" = true";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Days", days);
                var affected = await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation("Abonnement prolongé de {Days} jours pour l'utilisateur {UserId} par l'admin {AdminId}", 
                    days, userId, adminId);

                return affected > 0 
                    ? ServiceResult.Ok($"Abonnement prolongé de {days} jours")
                    : ServiceResult.NotFound("Aucun abonnement actif trouvé");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'extension de l'abonnement");
                return ServiceResult.Error("Erreur lors de l'extension");
            }
        }

        // ============================================
        // POINTS
        // ============================================

        public async Task<UserPointsDto> GetUserPointsAsync(int userId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT up.""Id"", up.""UserId"", u.""Username"", up.""Balance"", up.""TotalEarned"", up.""TotalSpent"", up.""UpdatedAt""
                FROM ""UserPoints"" up
                JOIN ""Users"" u ON u.""Id"" = up.""UserId""
                WHERE up.""UserId"" = @UserId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            using var reader = await cmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return new UserPointsDto
                {
                    UserId = reader.GetInt32(1),
                    Username = reader.GetString(2),
                    Balance = reader.GetInt32(3),
                    TotalEarned = reader.GetInt32(4),
                    TotalSpent = reader.GetInt32(5),
                    LastUpdated = reader.GetDateTime(6)
                };
            }

            await reader.CloseAsync();

            // Créer un enregistrement si inexistant
            var insertSql = @"
                INSERT INTO ""UserPoints"" (""UserId"", ""Balance"", ""TotalEarned"", ""TotalSpent"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (@UserId, 0, 0, 0, NOW(), NOW())
                RETURNING ""Id""";
            using var insertCmd = new NpgsqlCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("@UserId", userId);
            await insertCmd.ExecuteScalarAsync();

            // Récupérer le username
            var usernameSql = @"SELECT ""Username"" FROM ""Users"" WHERE ""Id"" = @UserId";
            using var usernameCmd = new NpgsqlCommand(usernameSql, conn);
            usernameCmd.Parameters.AddWithValue("@UserId", userId);
            var username = (string?)await usernameCmd.ExecuteScalarAsync() ?? "";

            return new UserPointsDto
            {
                UserId = userId,
                Username = username,
                Balance = 0,
                TotalEarned = 0,
                TotalSpent = 0,
                LastUpdated = DateTime.UtcNow
            };
        }

        public async Task<ServiceResult> GrantPointsAsync(int userId, int amount, string description, int adminId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = await conn.BeginTransactionAsync();

                // S'assurer que l'utilisateur a un enregistrement de points
                var ensureSql = @"
                    INSERT INTO ""UserPoints"" (""UserId"", ""Balance"", ""TotalEarned"", ""TotalSpent"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES (@UserId, 0, 0, 0, NOW(), NOW())
                    ON CONFLICT (""UserId"") DO NOTHING";
                using var ensureCmd = new NpgsqlCommand(ensureSql, conn, transaction);
                ensureCmd.Parameters.AddWithValue("@UserId", userId);
                await ensureCmd.ExecuteNonQueryAsync();

                // Mettre à jour le solde
                var updateSql = @"
                    UPDATE ""UserPoints"" 
                    SET ""Balance"" = ""Balance"" + @Amount,
                        ""TotalEarned"" = ""TotalEarned"" + @Amount,
                        ""UpdatedAt"" = NOW()
                    WHERE ""UserId"" = @UserId";
                using var updateCmd = new NpgsqlCommand(updateSql, conn, transaction);
                updateCmd.Parameters.AddWithValue("@UserId", userId);
                updateCmd.Parameters.AddWithValue("@Amount", amount);
                await updateCmd.ExecuteNonQueryAsync();

                // Enregistrer la transaction
                var txSql = @"
                    INSERT INTO ""PointTransactions"" 
                    (""UserId"", ""Amount"", ""TransactionType"", ""Description"", ""AdminId"", ""CreatedAt"")
                    VALUES (@UserId, @Amount, 'AdminGrant', @Description, @AdminId, NOW())";
                using var txCmd = new NpgsqlCommand(txSql, conn, transaction);
                txCmd.Parameters.AddWithValue("@UserId", userId);
                txCmd.Parameters.AddWithValue("@Amount", amount);
                txCmd.Parameters.AddWithValue("@Description", description);
                txCmd.Parameters.AddWithValue("@AdminId", adminId);
                await txCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("{Amount} points accordés à l'utilisateur {UserId} par l'admin {AdminId}", 
                    amount, userId, adminId);

                return ServiceResult.Ok($"{amount} points accordés avec succès");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'attribution des points");
                return ServiceResult.Error("Erreur lors de l'attribution des points");
            }
        }

        public async Task<ServiceResult> DeductPointsAsync(int userId, int amount, string description, string transactionType, int? referenceId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = await conn.BeginTransactionAsync();

                // Vérifier le solde
                var balanceSql = @"SELECT ""Balance"" FROM ""UserPoints"" WHERE ""UserId"" = @UserId";
                using var balanceCmd = new NpgsqlCommand(balanceSql, conn, transaction);
                balanceCmd.Parameters.AddWithValue("@UserId", userId);
                var balance = (int?)await balanceCmd.ExecuteScalarAsync();

                if (balance == null || balance < amount)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.Error("Solde insuffisant");
                }

                // Déduire
                var updateSql = @"
                    UPDATE ""UserPoints"" 
                    SET ""Balance"" = ""Balance"" - @Amount,
                        ""TotalSpent"" = ""TotalSpent"" + @Amount,
                        ""UpdatedAt"" = NOW()
                    WHERE ""UserId"" = @UserId";
                using var updateCmd = new NpgsqlCommand(updateSql, conn, transaction);
                updateCmd.Parameters.AddWithValue("@UserId", userId);
                updateCmd.Parameters.AddWithValue("@Amount", amount);
                await updateCmd.ExecuteNonQueryAsync();

                // Enregistrer la transaction
                var txSql = @"
                    INSERT INTO ""PointTransactions"" 
                    (""UserId"", ""Amount"", ""TransactionType"", ""Description"", ""ReferenceId"", ""CreatedAt"")
                    VALUES (@UserId, -@Amount, @TransactionType, @Description, @ReferenceId, NOW())";
                using var txCmd = new NpgsqlCommand(txSql, conn, transaction);
                txCmd.Parameters.AddWithValue("@UserId", userId);
                txCmd.Parameters.AddWithValue("@Amount", amount);
                txCmd.Parameters.AddWithValue("@TransactionType", transactionType);
                txCmd.Parameters.AddWithValue("@Description", description);
                txCmd.Parameters.AddWithValue("@ReferenceId", (object?)referenceId ?? DBNull.Value);
                await txCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return ServiceResult.Ok($"{amount} points déduits avec succès");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la déduction des points");
                return ServiceResult.Error("Erreur lors de la déduction des points");
            }
        }

        public async Task<List<PointTransactionDto>> GetPointHistoryAsync(int userId, int page, int pageSize)
        {
            var transactions = new List<PointTransactionDto>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT pt.""Id"", pt.""UserId"", pt.""Amount"", pt.""TransactionType"", pt.""Description"", 
                       pt.""ReferenceId"", pt.""AdminId"", pt.""CreatedAt"",
                       a.""Username"" as AdminUsername
                FROM ""PointTransactions"" pt
                LEFT JOIN ""Users"" a ON a.""Id"" = pt.""AdminId""
                WHERE pt.""UserId"" = @UserId
                ORDER BY pt.""CreatedAt"" DESC
                LIMIT @PageSize OFFSET @Offset";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@PageSize", pageSize);
            cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                transactions.Add(new PointTransactionDto
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Amount = reader.GetInt32(2),
                    TransactionType = reader.GetString(3),
                    Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ReferenceId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    AdminId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    CreatedAt = reader.GetDateTime(7),
                    AdminUsername = reader.IsDBNull(8) ? null : reader.GetString(8)
                });
            }

            return transactions;
        }

        // ============================================
        // TRIAL
        // ============================================

        public async Task<bool> CanUseTrialAsync(int userId, int tierId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT COUNT(*) FROM ""UsedTrials"" WHERE ""UserId"" = @UserId AND ""TierId"" = @TierId";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@TierId", tierId);
            var count = (long?)await cmd.ExecuteScalarAsync();
            return count == 0;
        }

        public async Task<ServiceResult> ActivateTrialAsync(int userId, int tierId)
        {
            try
            {
                var canUse = await CanUseTrialAsync(userId, tierId);
                if (!canUse)
                {
                    return ServiceResult.Error("L'utilisateur a déjà utilisé la période d'essai pour ce tier");
                }

                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = await conn.BeginTransactionAsync();

                // Enregistrer l'utilisation de l'essai
                var trialSql = @"
                    INSERT INTO ""UsedTrials"" (""UserId"", ""TierId"", ""ActivatedAt"", ""ExpiresAt"")
                    VALUES (@UserId, @TierId, NOW(), NOW() + @Days * INTERVAL '1 day')";
                using var trialCmd = new NpgsqlCommand(trialSql, conn, transaction);
                trialCmd.Parameters.AddWithValue("@UserId", userId);
                trialCmd.Parameters.AddWithValue("@TierId", tierId);
                trialCmd.Parameters.AddWithValue("@Days", TRIAL_DAYS);
                await trialCmd.ExecuteNonQueryAsync();

                // Désactiver les abonnements existants
                var deactivateSql = @"
                    UPDATE ""UserSubscriptions"" 
                    SET ""IsActive"" = false, ""UpdatedAt"" = NOW()
                    WHERE ""UserId"" = @UserId AND ""IsActive"" = true";
                using var deactivateCmd = new NpgsqlCommand(deactivateSql, conn, transaction);
                deactivateCmd.Parameters.AddWithValue("@UserId", userId);
                await deactivateCmd.ExecuteNonQueryAsync();

                // Créer l'abonnement d'essai
                var insertSql = @"
                    INSERT INTO ""UserSubscriptions"" 
                    (""UserId"", ""TierId"", ""StartedAt"", ""ExpiresAt"", ""IsActive"", ""AutoRenew"", ""PaymentMethod"", ""IsTrial"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES (@UserId, @TierId, NOW(), NOW() + @Days * INTERVAL '1 day', true, false, 'Trial', true, NOW(), NOW())";
                using var insertCmd = new NpgsqlCommand(insertSql, conn, transaction);
                insertCmd.Parameters.AddWithValue("@UserId", userId);
                insertCmd.Parameters.AddWithValue("@TierId", tierId);
                insertCmd.Parameters.AddWithValue("@Days", TRIAL_DAYS);
                await insertCmd.ExecuteNonQueryAsync();

                // Mettre à jour le tier de l'utilisateur
                var updateUserSql = @"UPDATE ""Users"" SET ""TierId"" = @TierId WHERE ""Id"" = @UserId";
                using var updateUserCmd = new NpgsqlCommand(updateUserSql, conn, transaction);
                updateUserCmd.Parameters.AddWithValue("@TierId", tierId);
                updateUserCmd.Parameters.AddWithValue("@UserId", userId);
                await updateUserCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Période d'essai de {Days} jours activée pour l'utilisateur {UserId}, tier {TierId}", 
                    TRIAL_DAYS, userId, tierId);

                return ServiceResult.Ok($"Période d'essai de {TRIAL_DAYS} jours activée");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'activation de la période d'essai");
                return ServiceResult.Error("Erreur lors de l'activation de la période d'essai");
            }
        }

        // ============================================
        // STATS
        // ============================================

        public async Task<SubscriptionStatsDto> GetStatsAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var stats = new SubscriptionStatsDto();

            // Total des abonnés actifs (excluant les Member/Free)
            var totalSql = @"
                SELECT COUNT(*) FROM ""UserSubscriptions"" us
                JOIN ""SubscriptionTiers"" t ON t.""Id"" = us.""TierId""
                WHERE us.""IsActive"" = true 
                AND (us.""ExpiresAt"" IS NULL OR us.""ExpiresAt"" > NOW())
                AND t.""Tier"" > 0";
            using var totalCmd = new NpgsqlCommand(totalSql, conn);
            stats.TotalActiveSubscriptions = (int)(long)(await totalCmd.ExecuteScalarAsync() ?? 0L);

            // Par tier
            var byTierSql = @"
                SELECT t.""Id"", t.""Name"", t.""Color"", COUNT(us.""Id"") as Count
                FROM ""SubscriptionTiers"" t
                LEFT JOIN ""UserSubscriptions"" us ON us.""TierId"" = t.""Id"" 
                    AND us.""IsActive"" = true 
                    AND (us.""ExpiresAt"" IS NULL OR us.""ExpiresAt"" > NOW())
                WHERE t.""Tier"" > 0
                GROUP BY t.""Id"", t.""Name"", t.""Color""
                ORDER BY t.""Tier""";
            using var byTierCmd = new NpgsqlCommand(byTierSql, conn);
            using var reader = await byTierCmd.ExecuteReaderAsync();
            stats.ByTier = new List<TierStatDto>();
            while (await reader.ReadAsync())
            {
                stats.ByTier.Add(new TierStatDto
                {
                    TierId = reader.GetInt32(0),
                    TierName = reader.GetString(1),
                    Color = reader.GetString(2),
                    Count = (int)reader.GetInt64(3)
                });
            }
            await reader.CloseAsync();

            // Revenus du mois
            var revenueSql = @"
                SELECT COALESCE(SUM(""PricePaid""), 0) FROM ""UserSubscriptions"" 
                WHERE ""CreatedAt"" >= date_trunc('month', NOW())";
            using var revenueCmd = new NpgsqlCommand(revenueSql, conn);
            stats.MonthlyRevenueCents = (int)(long)(await revenueCmd.ExecuteScalarAsync() ?? 0L);

            // Points dépensés ce mois
            var pointsSql = @"
                SELECT COALESCE(SUM(""PointsUsed""), 0) FROM ""UserSubscriptions"" 
                WHERE ""CreatedAt"" >= date_trunc('month', NOW()) AND ""PointsUsed"" > 0";
            using var pointsCmd = new NpgsqlCommand(pointsSql, conn);
            stats.MonthlyPointsSpent = (int)(long)(await pointsCmd.ExecuteScalarAsync() ?? 0L);

            // Expirent cette semaine
            var expiringSql = @"
                SELECT COUNT(*) FROM ""UserSubscriptions"" 
                WHERE ""IsActive"" = true 
                AND ""ExpiresAt"" BETWEEN NOW() AND NOW() + INTERVAL '7 days'";
            using var expiringCmd = new NpgsqlCommand(expiringSql, conn);
            stats.ExpiringThisWeek = (int)(long)(await expiringCmd.ExecuteScalarAsync() ?? 0L);

            // Nouveaux cette semaine
            var newSql = @"
                SELECT COUNT(*) FROM ""UserSubscriptions"" 
                WHERE ""CreatedAt"" >= NOW() - INTERVAL '7 days'";
            using var newCmd = new NpgsqlCommand(newSql, conn);
            stats.NewThisWeek = (int)(long)(await newCmd.ExecuteScalarAsync() ?? 0L);

            return stats;
        }
    }

    // ============================================
    // DTOs
    // ============================================

    public class SubscriptionTierDto
    {
        public int Id { get; set; }
        public int Tier { get; set; }
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Description { get; set; }
        public string Color { get; set; } = "#808080";
        public string Icon { get; set; } = "";
        public int MonthlyPriceCents { get; set; }
        public int YearlyPriceCents { get; set; }
        public int BasePricePerDayCents { get; set; }
        public int BasePointsPerDay { get; set; }
        public bool IsAvailable { get; set; } = true;
        public int ActiveUsersCount { get; set; }
    }

    public class UpdateTierDto
    {
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string? Color { get; set; }
        public int? MonthlyPriceCents { get; set; }
        public int? YearlyPriceCents { get; set; }
        public bool? IsAvailable { get; set; }
    }

    public class SubscriptionDurationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int BaseDays { get; set; }
        public int BonusDays { get; set; }
        public int TotalDays { get; set; }
        public int DiscountPercent { get; set; }
        public bool IsAvailable { get; set; } = true;
    }

    public class UpdateDurationDto
    {
        public string? DisplayName { get; set; }
        public int? BonusDays { get; set; }
        public int? DiscountPercent { get; set; }
        public bool? IsAvailable { get; set; }
    }

    public class SubscriptionPriceDto
    {
        public int TierId { get; set; }
        public string TierName { get; set; } = "";
        public string TierDisplayName { get; set; } = "";
        public string TierColor { get; set; } = "#808080";
        public int DurationId { get; set; }
        public string DurationName { get; set; } = "";
        public string DurationDisplayName { get; set; } = "";
        public int BaseDays { get; set; }
        public int BonusDays { get; set; }
        public int DiscountPercent { get; set; }
        public int PriceCents { get; set; }
        public int Points { get; set; }
        public bool IsCustomPrice { get; set; }
    }

    public class UserSubscriptionDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string UserFullName { get; set; } = "";
        public int TierId { get; set; }
        public string TierName { get; set; } = "";
        public string TierColor { get; set; } = "#808080";
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = "";
        public string? PaymentMethod { get; set; }
        public int? PaidAmountCents { get; set; }
        public int? PointsUsed { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsTrial { get; set; }
    }

    public class UserPointsDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public int Balance { get; set; }
        public int TotalEarned { get; set; }
        public int TotalSpent { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class PointTransactionDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int Amount { get; set; }
        public string TransactionType { get; set; } = "";
        public string? Description { get; set; }
        public int? ReferenceId { get; set; }
        public int? AdminId { get; set; }
        public string? AdminUsername { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SubscriptionStatsDto
    {
        public int TotalActiveSubscriptions { get; set; }
        public List<TierStatDto> ByTier { get; set; } = new();
        public int MonthlyRevenueCents { get; set; }
        public int MonthlyPointsSpent { get; set; }
        public int ExpiringThisWeek { get; set; }
        public int NewThisWeek { get; set; }
    }

    public class TierStatDto
    {
        public int TierId { get; set; }
        public string TierName { get; set; } = "";
        public string Color { get; set; } = "#808080";
        public int Count { get; set; }
    }
}
