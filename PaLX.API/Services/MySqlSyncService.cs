using MySqlConnector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PaLX.API.Services
{
    /// <summary>
    /// Service de synchronisation avec la base MySQL locale (Laragon/phpMyAdmin)
    /// Permet de répliquer les comptes utilisateurs vers le script PHP
    /// </summary>
    public interface IMySqlSyncService
    {
        /// <summary>
        /// Synchronise un nouvel utilisateur vers MySQL
        /// </summary>
        Task<bool> SyncNewUserAsync(int userId, string username, string passwordHash, string? email = null);
        
        /// <summary>
        /// Met à jour le profil d'un utilisateur dans MySQL
        /// </summary>
        Task<bool> SyncUserProfileAsync(string username, string? firstName, string? lastName, string? email, string? avatarPath);
        
        /// <summary>
        /// Supprime un utilisateur de MySQL
        /// </summary>
        Task<bool> DeleteUserAsync(string username);
        
        /// <summary>
        /// Vérifie la connexion MySQL
        /// </summary>
        Task<bool> TestConnectionAsync();
    }

    public class MySqlSyncService : IMySqlSyncService
    {
        private readonly string _connectionString;
        private readonly ILogger<MySqlSyncService> _logger;
        private readonly bool _isEnabled;

        public MySqlSyncService(IConfiguration configuration, ILogger<MySqlSyncService> logger)
        {
            _logger = logger;
            
            // Configuration MySQL depuis appsettings.json
            var mysqlConfig = configuration.GetSection("MySqlSync");
            _isEnabled = mysqlConfig.GetValue<bool>("Enabled", false);
            
            if (_isEnabled)
            {
                var host = mysqlConfig.GetValue<string>("Host") ?? "localhost";
                var port = mysqlConfig.GetValue<int>("Port", 3306);
                var database = mysqlConfig.GetValue<string>("Database") ?? "pal_xtreme";
                var user = mysqlConfig.GetValue<string>("User") ?? "root";
                var password = mysqlConfig.GetValue<string>("Password") ?? "";
                
                _connectionString = $"Server={host};Port={port};Database={database};User={user};Password={password};";
                
                _logger.LogInformation("🔗 MySqlSyncService initialized - Database: {Database}@{Host}:{Port}", database, host, port);
            }
            else
            {
                _connectionString = "";
                _logger.LogInformation("⚠️ MySqlSyncService is DISABLED - Set MySqlSync:Enabled=true in appsettings.json to enable");
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (!_isEnabled) return false;
            
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();
                _logger.LogInformation("✅ MySQL connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ MySQL connection test failed");
                return false;
            }
        }

        public async Task<bool> SyncNewUserAsync(int userId, string username, string passwordHash, string? email = null)
        {
            if (!_isEnabled)
            {
                _logger.LogDebug("MySqlSync disabled - skipping user sync for {Username}", username);
                return true; // Return true so registration doesn't fail
            }

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                // Vérifier si la table existe, sinon la créer
                await EnsureTableExistsAsync(conn);

                // Insérer l'utilisateur
                var sql = @"
                    INSERT INTO users (palx_user_id, username, password_hash, email, created_at, synced_at)
                    VALUES (@userId, @username, @passwordHash, @email, NOW(), NOW())
                    ON DUPLICATE KEY UPDATE 
                        password_hash = @passwordHash,
                        email = @email,
                        synced_at = NOW()";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                cmd.Parameters.AddWithValue("@email", email ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation("✅ User {Username} (ID: {UserId}) synced to MySQL", username, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to sync user {Username} to MySQL", username);
                return false; // Don't fail registration, just log the error
            }
        }

        public async Task<bool> SyncUserProfileAsync(string username, string? firstName, string? lastName, string? email, string? avatarPath)
        {
            if (!_isEnabled) return true;

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = @"
                    UPDATE users 
                    SET first_name = @firstName,
                        last_name = @lastName,
                        email = @email,
                        avatar_path = @avatarPath,
                        synced_at = NOW()
                    WHERE username = @username";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@firstName", firstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lastName", lastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@email", email ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@avatarPath", avatarPath ?? (object)DBNull.Value);

                var affected = await cmd.ExecuteNonQueryAsync();

                if (affected > 0)
                {
                    _logger.LogInformation("✅ Profile for {Username} synced to MySQL", username);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to sync profile for {Username} to MySQL", username);
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(string username)
        {
            if (!_isEnabled) return true;

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = "DELETE FROM users WHERE username = @username";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@username", username);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation("✅ User {Username} deleted from MySQL", username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to delete user {Username} from MySQL", username);
                return false;
            }
        }

        /// <summary>
        /// Crée la table users si elle n'existe pas
        /// </summary>
        private async Task EnsureTableExistsAsync(MySqlConnection conn)
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS users (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    palx_user_id INT NOT NULL UNIQUE,
                    username VARCHAR(100) NOT NULL UNIQUE,
                    password_hash VARCHAR(255) NOT NULL,
                    email VARCHAR(255) NULL,
                    first_name VARCHAR(100) NULL,
                    last_name VARCHAR(100) NULL,
                    avatar_path VARCHAR(500) NULL,
                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    synced_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    INDEX idx_username (username),
                    INDEX idx_palx_user_id (palx_user_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

            using var cmd = new MySqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
